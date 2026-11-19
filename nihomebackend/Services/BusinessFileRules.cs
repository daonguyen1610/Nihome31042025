using System.Globalization;

namespace NihomeBackend.Services;

/// <summary>
/// Type and size rules for business documents uploaded to design records,
/// tenders and capability documents. Each module's size limit is an
/// administrator setting (Cài đặt → Giới hạn tệp …, see <see cref="UploadLimitKind"/>).
/// </summary>
public static class BusinessFileRules
{
    /// <summary>Highest limit an administrator may set, as for other project documents.</summary>
    public const int MaxAllowedMb = (int)(ProjectDocumentStorageService.MaxFileSize / (1024 * 1024));

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Why the file cannot be attached, in Vietnamese; null when it can.</summary>
    public static string? Problem(IFormFile file, int maxMb)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            var shown = string.IsNullOrWhiteSpace(extension) ? "không có đuôi" : extension.ToLowerInvariant();
            return $"Tệp “{file.FileName}” ({shown}) chưa được nhận. " +
                "Chỉ nhận PDF, Word (.doc, .docx), Excel (.xls, .xlsx) và ảnh PNG, JPG.";
        }
        if (file.Length == 0)
            return $"Tệp “{file.FileName}” trống. Hãy kiểm tra lại tệp rồi tải lên.";
        if (file.Length > (long)maxMb * 1024 * 1024)
        {
            var megabytes = (file.Length / 1024d / 1024d).ToString("0.0", Vietnamese);
            return $"Tệp “{file.FileName}” nặng {megabytes} MB, vượt giới hạn {maxMb} MB. " +
                "Hãy nén hoặc tách tệp rồi tải lên lại.";
        }
        return null;
    }
}
