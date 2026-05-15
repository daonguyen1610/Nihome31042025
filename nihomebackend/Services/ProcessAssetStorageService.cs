using NihomeBackend.Models;

namespace NihomeBackend.Services;

public record StoredProcessAsset(
    string Url,
    string OriginalFileName,
    string? ContentType,
    long FileSizeBytes);

public class ProcessAssetStorageService(IWebHostEnvironment env)
{
    private const string ManagedAssetPrefix = "/process-assets/";
    private const long MaxImageSizeBytes = 15 * 1024 * 1024;
    private const long MaxDocumentSizeBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private static readonly HashSet<string> AllowedDocumentExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx"];

    public async Task<StoredProcessAsset> SaveUploadAsync(
        IFormFile file,
        ProcessAssetType type,
        CancellationToken cancellationToken)
    {
        ValidateUpload(file, type);

        await using var stream = file.OpenReadStream();
        return await SaveAsync(stream, file.FileName, file.ContentType, file.Length, type, cancellationToken);
    }

    public async Task<StoredProcessAsset> SaveLegacyAsync(
        Stream stream,
        string originalFileName,
        string? contentType,
        long? contentLength,
        ProcessAssetType type,
        CancellationToken cancellationToken)
    {
        var size = contentLength ?? 0;
        if (size > GetMaxSizeBytes(type))
        {
            throw new InvalidOperationException("Legacy process asset is too large.");
        }

        return await SaveAsync(stream, originalFileName, contentType, size, type, cancellationToken);
    }

    public void DeleteIfManagedAsset(string? url)
    {
        url = NormalizeAssetUrl(url);
        if (string.IsNullOrWhiteSpace(url) ||
            !url.StartsWith(ManagedAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var managedRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot", "process-assets"));
        var managedRootPrefix = $"{managedRoot.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}";
        var relativePath = url[ManagedAssetPrefix.Length..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            managedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(managedRootPrefix, comparison))
        {
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string? NormalizeAssetUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (url.StartsWith(ManagedAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.StartsWith(ManagedAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath;
        }

        return url;
    }

    private async Task<StoredProcessAsset> SaveAsync(
        Stream stream,
        string originalFileName,
        string? contentType,
        long contentLength,
        ProcessAssetType type,
        CancellationToken cancellationToken)
    {
        var safeOriginalName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeOriginalName))
        {
            safeOriginalName = type == ProcessAssetType.Image ? "process-image" : "process-file";
        }

        var extension = Path.GetExtension(safeOriginalName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var typeFolder = type == ProcessAssetType.Image ? "images" : "files";
        var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "process-assets", typeFolder);
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDir, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.CreateNew);
        await stream.CopyToAsync(fileStream, cancellationToken);

        var actualSize = contentLength > 0 ? contentLength : fileStream.Length;
        return new StoredProcessAsset(
            $"/process-assets/{typeFolder}/{fileName}",
            safeOriginalName,
            string.IsNullOrWhiteSpace(contentType) ? null : contentType,
            actualSize);
    }

    private static void ValidateUpload(IFormFile file, ProcessAssetType type)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("No file uploaded.");
        }

        if (file.Length > GetMaxSizeBytes(type))
        {
            throw new InvalidOperationException(type == ProcessAssetType.Image
                ? "File quá lớn (tối đa 15MB)"
                : "File quá lớn (tối đa 25MB)");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = type == ProcessAssetType.Image
            ? AllowedImageExtensions
            : AllowedDocumentExtensions;

        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(type == ProcessAssetType.Image
                ? "Invalid image format"
                : "Chỉ chấp nhận file PDF, DOC, DOCX, XLS, XLSX");
        }
    }

    private static long GetMaxSizeBytes(ProcessAssetType type) =>
        type == ProcessAssetType.Image ? MaxImageSizeBytes : MaxDocumentSizeBytes;
}
