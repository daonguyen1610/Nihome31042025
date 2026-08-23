using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class SurveyMediaValidationException(string message) : Exception(message);

public sealed class SurveyMediaConflictException(string message) : Exception(message);

public sealed record StoredSurveyMedia(
    string OriginalFileName,
    string StoredFileName,
    string Extension,
    string ContentType,
    long Size,
    string RelativePath);

public interface ISurveyMediaStorageService
{
    Task<StoredSurveyMedia> StoreAsync(int surveyId, IFormFile? file, CancellationToken ct = default);
    ManagedDocumentContent? GetContent(int surveyId, string relativePath, string originalFileName, string contentType);
    Stream OpenRead(int surveyId, string relativePath);
    void Delete(int surveyId, string relativePath);
}

public sealed class SurveyMediaStorageService(IWebHostEnvironment environment) : ISurveyMediaStorageService
{
    public const long MaxFileSize = 100L * 1024 * 1024;
    public const long MaxSurveySize = 2L * 1024 * 1024 * 1024;
    public const string PublicPrefix = "/files/survey-media";

    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".heic"] = "image/heic",
            [".mp4"] = "video/mp4",
            [".mov"] = "video/quicktime",
            [".pdf"] = "application/pdf",
            [".dwg"] = "application/acad",
            [".rvt"] = "application/vnd.autodesk.revit",
        };

    public async Task<StoredSurveyMedia> StoreAsync(int surveyId, IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new SurveyMediaValidationException("Tệp khảo sát là bắt buộc và không được để trống.");
        }
        if (file.Length > MaxFileSize)
        {
            throw new SurveyMediaValidationException("Tệp khảo sát không được vượt quá 100 MiB. Vui lòng chọn tệp nhỏ hơn.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new SurveyMediaValidationException(
                "Định dạng tệp khảo sát không được hỗ trợ. Chỉ chấp nhận JPG, JPEG, PNG, HEIC, MP4, MOV, PDF, DWG hoặc RVT.");
        }
        if (!await HasExpectedSignatureAsync(file, extension, ct))
        {
            throw new SurveyMediaValidationException(
                "Nội dung tệp khảo sát không khớp với định dạng đã chọn. Vui lòng tải lên tệp JPG, PNG, HEIC, MP4, MOV, PDF, DWG hoặc RVT hợp lệ.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var directory = GetSurveyDirectory(surveyId);
        Directory.CreateDirectory(directory);
        var destination = Confine(directory, storedFileName);

        try
        {
            await using var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await file.CopyToAsync(output, ct);
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }

        return new StoredSurveyMedia(
            Path.GetFileName(file.FileName),
            storedFileName,
            extension,
            contentType,
            file.Length,
            $"{PublicPrefix}/{surveyId}/{storedFileName}");
    }

    public ManagedDocumentContent? GetContent(
        int surveyId, string relativePath, string originalFileName, string contentType)
    {
        var path = Resolve(surveyId, relativePath);
        return path is not null && File.Exists(path)
            ? new ManagedDocumentContent(path, Path.GetFileName(originalFileName), contentType)
            : null;
    }

    public Stream OpenRead(int surveyId, string relativePath)
    {
        var path = Resolve(surveyId, relativePath)
            ?? throw new FileNotFoundException("Không tìm thấy tệp khảo sát trong vùng lưu trữ riêng tư.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    }

    public void Delete(int surveyId, string relativePath)
    {
        var path = Resolve(surveyId, relativePath);
        if (path is null) return;
        if (File.Exists(path)) File.Delete(path);
    }

    private static async Task<bool> HasExpectedSignatureAsync(
        IFormFile file, string extension, CancellationToken ct)
    {
        var header = new byte[32];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(), ct);
        return HasExpectedSignature(header, bytesRead, extension);
    }

    private static bool HasExpectedSignature(byte[] header, int bytesRead, string extension)
    {
        var bytes = header.AsSpan(0, bytesRead);
        return extension switch
        {
            ".jpg" or ".jpeg" => StartsWith(bytes, [0xFF, 0xD8, 0xFF]),
            ".png" => StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ".pdf" => StartsWith(bytes, "%PDF-"u8),
            ".dwg" => StartsWith(bytes, "AC10"u8),
            ".rvt" => StartsWith(bytes, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
            ".heic" => HasIsoBrand(bytes, "heic", "heix", "hevc", "hevx", "mif1", "msf1"),
            ".mp4" => HasIsoBrand(bytes, "isom", "iso2", "mp41", "mp42", "avc1", "M4V "),
            ".mov" => HasIsoBrand(bytes, "qt  ") || HasAtom(bytes, "moov") || HasAtom(bytes, "mdat"),
            _ => false,
        };
    }

    private static bool HasIsoBrand(ReadOnlySpan<byte> bytes, params string[] brands)
    {
        if (!HasAtom(bytes, "ftyp") || bytes.Length < 12) return false;
        var majorBrand = System.Text.Encoding.ASCII.GetString(bytes.Slice(8, 4));
        if (brands.Contains(majorBrand, StringComparer.Ordinal)) return true;

        for (var offset = 16; offset + 4 <= bytes.Length; offset += 4)
        {
            if (brands.Contains(System.Text.Encoding.ASCII.GetString(bytes.Slice(offset, 4)), StringComparer.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAtom(ReadOnlySpan<byte> bytes, string atom) =>
        bytes.Length >= 8 && bytes.Slice(4, 4).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(atom));

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature) =>
        bytes.StartsWith(signature);

    private string? Resolve(int surveyId, string relativePath)
    {
        var prefix = $"{PublicPrefix}/{surveyId}/";
        if (!relativePath.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var fileName = Path.GetFileName(relativePath);
        if (!string.Equals(relativePath, prefix + fileName, StringComparison.Ordinal)) return null;
        return Confine(GetSurveyDirectory(surveyId), fileName);
    }

    private string GetSurveyDirectory(int surveyId) => Path.GetFullPath(Path.Combine(
        environment.ContentRootPath, "wwwroot", "files", "survey-media", surveyId.ToString()));

    private static string Confine(string directory, string fileName)
    {
        var directoryPrefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!fullPath.StartsWith(directoryPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Đường dẫn tệp khảo sát nằm ngoài vùng lưu trữ được quản lý.");
        }
        return fullPath;
    }
}