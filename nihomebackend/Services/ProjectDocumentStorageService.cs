using System.Security.Cryptography;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record StoredProjectDocument(
    string LocalPath,
    string OriginalFileName,
    string ContentType,
    long Size,
    string Sha256);

public interface IProjectDocumentStorageService
{
    Task<StoredProjectDocument> InspectUploadAsync(IFormFile? file, CancellationToken ct = default);
    Task<StoredProjectDocument> StoreAsync(int projectId, IFormFile? file, CancellationToken ct = default);
    Task<StoredProjectDocument> StoreDriveImportAsync(int projectId, string fileName, string contentType,
        long? expectedLength, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct = default);
    Task<StoredProjectDocument> InspectExistingAsync(ProjectDocumentSourceModule sourceModule, string localPath, string originalFileName, CancellationToken ct = default);
    ManagedDocumentContent? GetContent(int projectId, string localPath, string originalFileName, string contentType);
    Stream OpenRead(int projectId, string localPath);
    void DeleteOwned(int projectId, string localPath);
}

public sealed class ProjectDocumentStorageService(IWebHostEnvironment environment) : IProjectDocumentStorageService
{
    public const long MaxFileSize = 100L * 1024 * 1024;
    public const long MultipartBodyLengthLimit = 106L * 1024 * 1024;
    public const string ManagedPrefix = "/files/project-documents";
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".bat", ".cmd", ".sh", ".ps1", ".js", ".html", ".htm", ".svg"
    };
    private static readonly IReadOnlyDictionary<ProjectDocumentSourceModule, string[]> ExistingPrefixes =
        new Dictionary<ProjectDocumentSourceModule, string[]>
        {
            [ProjectDocumentSourceModule.Crm] = ["/files/quotes/", "/files/customers/", "/files/contracts/", "/files/capability/", "/files/tenders/"],
            [ProjectDocumentSourceModule.Survey] = ["/files/survey-media/"],
            [ProjectDocumentSourceModule.Design] = ["/files/design/", "/files/business-documents/permits/"],
            [ProjectDocumentSourceModule.Construction] = ["/files/business-documents/construction/"],
            [ProjectDocumentSourceModule.Acceptance] = ["/files/business-documents/acceptance/", "/files/business-documents/as-built/"],
            [ProjectDocumentSourceModule.Handover] = ["/files/business-documents/handover/"],
        };

    public async Task<StoredProjectDocument> InspectUploadAsync(IFormFile? file, CancellationToken ct = default)
    {
        ValidateFile(file, "Tệp dự án");
        await using var content = file!.OpenReadStream();
        return new StoredProjectDocument(
            string.Empty,
            Path.GetFileName(file.FileName),
            NormalizeContentType(file.ContentType),
            file.Length,
            await HashAsync(content, ct));
    }

    public async Task<StoredProjectDocument> StoreAsync(int projectId, IFormFile? file, CancellationToken ct = default)
    {
        ValidateFile(file, "Tệp dự án");
        var safeName = Path.GetFileName(file!.FileName);
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}";
        var path = $"{ManagedPrefix}/{projectId}/{storedName}";
        await using var input = file.OpenReadStream();
        return await StoreStreamAsync(projectId, path, safeName, NormalizeContentType(file.ContentType), file.Length, input, ct);
    }

    public async Task<StoredProjectDocument> StoreDriveImportAsync(int projectId, string fileName, string contentType,
        long? expectedLength, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct = default)
    {
        if (expectedLength > MaxFileSize)
            throw new ProjectDocumentValidationException("Tệp Google Drive không được vượt quá 100 MiB.");
        var safeName = ValidateFileName(fileName);
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}";
        var path = $"{ManagedPrefix}/{projectId}/{storedName}";
        var destination = ResolveForProject(projectId, path)
            ?? throw new InvalidOperationException("Đường dẫn lưu tệp dự án không hợp lệ.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            await using (var limited = new SizeLimitedWriteStream(output, MaxFileSize))
            {
                await writeContent(limited, ct);
            }
            var info = new FileInfo(destination);
            if (info.Length == 0 || expectedLength.HasValue && info.Length != expectedLength.Value)
                throw new ProjectDocumentValidationException("Tệp Google Drive bị trống hoặc dữ liệu tải xuống không đầy đủ.");
            await using var content = File.OpenRead(destination);
            return new StoredProjectDocument(path, safeName, NormalizeContentType(contentType), info.Length,
                await HashAsync(content, ct));
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    public async Task<StoredProjectDocument> InspectExistingAsync(
        ProjectDocumentSourceModule sourceModule, string localPath, string originalFileName, CancellationToken ct = default)
    {
        if (!ExistingPrefixes.TryGetValue(sourceModule, out var prefixes) ||
            !prefixes.Any(prefix => localPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProjectDocumentValidationException("Nguồn tệp hiện có không thuộc vùng lưu trữ được phép của phân hệ đã chọn.");
        }
        var fullPath = ResolveUnderWebRoot(localPath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            throw new ProjectDocumentValidationException("Không tìm thấy tệp nguồn trong vùng lưu trữ được quản lý.");
        }
        var info = new FileInfo(fullPath);
        if (info.Length == 0 || info.Length > MaxFileSize)
        {
            throw new ProjectDocumentValidationException("Tệp nguồn phải có dữ liệu và không được vượt quá 100 MiB.");
        }
        await using var stream = File.OpenRead(fullPath);
        return new StoredProjectDocument(localPath, ValidateFileName(originalFileName),
            "application/octet-stream", info.Length, await HashAsync(stream, ct));
    }

    public ManagedDocumentContent? GetContent(int projectId, string localPath, string originalFileName, string contentType)
    {
        var fullPath = ResolveForProject(projectId, localPath) ?? ResolveUnderWebRoot(localPath);
        return fullPath is not null && File.Exists(fullPath)
            ? new ManagedDocumentContent(fullPath, Path.GetFileName(originalFileName), contentType)
            : null;
    }

    public Stream OpenRead(int projectId, string localPath)
    {
        var fullPath = ResolveForProject(projectId, localPath) ?? ResolveUnderWebRoot(localPath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("Không tìm thấy tệp dự án trong vùng lưu trữ riêng tư.");
        }
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    }

    public void DeleteOwned(int projectId, string localPath)
    {
        var fullPath = ResolveForProject(projectId, localPath);
        if (fullPath is not null && File.Exists(fullPath)) File.Delete(fullPath);
    }

    private async Task<StoredProjectDocument> StoreStreamAsync(
        int projectId, string localPath, string originalName, string contentType, long? expectedLength,
        Stream input, CancellationToken ct)
    {
        var destination = ResolveForProject(projectId, localPath)
            ?? throw new InvalidOperationException("Đường dẫn lưu tệp dự án không hợp lệ.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                size += read;
                if (size > MaxFileSize) throw new ProjectDocumentValidationException("Tệp dự án không được vượt quá 100 MiB.");
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (size == 0 || expectedLength.HasValue && size != expectedLength.Value)
                throw new ProjectDocumentValidationException("Tệp dự án bị trống hoặc dữ liệu tải lên không đầy đủ.");
            return new StoredProjectDocument(localPath, originalName, contentType, size,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    private static void ValidateFile(IFormFile? file, string fieldName)
    {
        if (file is null || file.Length == 0)
            throw new ProjectDocumentValidationException($"{fieldName} là bắt buộc và không được để trống.");
        if (file.Length > MaxFileSize)
            throw new ProjectDocumentValidationException($"{fieldName} không được vượt quá 100 MiB.");
        ValidateFileName(file.FileName);
    }

    private static string ValidateFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 260 || safeName != fileName?.Trim())
            throw new ProjectDocumentValidationException("Tên tệp không hợp lệ; ví dụ hợp lệ: ban-ve-tang-1.pdf.");
        if (BlockedExtensions.Contains(Path.GetExtension(safeName)))
            throw new ProjectDocumentValidationException("Định dạng tệp không an toàn và không được phép tải lên.");
        return safeName;
    }

    private string? ResolveForProject(int projectId, string path)
    {
        var prefix = $"{ManagedPrefix}/{projectId}/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || path != prefix + Path.GetFileName(path)) return null;
        return ResolveUnderWebRoot(path);
    }

    private string? ResolveUnderWebRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/files/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains('\\') || path.Split('/').Any(segment => segment is "." or "..")) return null;
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var fullPath = Path.GetFullPath(Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.Ordinal) ? fullPath : null;
    }

    private static async Task<string> HashAsync(Stream stream, CancellationToken ct)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeContentType(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 150 ? "application/octet-stream" : value.Trim();

    private sealed class SizeLimitedWriteStream(Stream inner, long maximumLength) : Stream
    {
        private long length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(buffer.Length);
            inner.Write(buffer);
            length += buffer.Length;
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureCapacity(buffer.Length);
            await inner.WriteAsync(buffer, cancellationToken);
            length += buffer.Length;
        }
        private void EnsureCapacity(int count)
        {
            if (length + count > maximumLength)
                throw new ProjectDocumentValidationException("Tệp Google Drive không được vượt quá 100 MiB.");
        }
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
