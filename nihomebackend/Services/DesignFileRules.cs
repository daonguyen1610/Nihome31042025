using System.Globalization;

namespace NihomeBackend.Services;

/// <summary>
/// Size and type rules for files attached to basic design documents and shop
/// drawings. The admin forms check the same rules before uploading.
/// </summary>
public static class DesignFileRules
{
    public const long MaxBytes = 20L * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Why the file cannot be attached, in Vietnamese; null when it can.</summary>
    public static string? Problem(IFormFile file)
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
        if (file.Length > MaxBytes)
        {
            var megabytes = (file.Length / 1024d / 1024d).ToString("0.0", Vietnamese);
            return $"Tệp “{file.FileName}” nặng {megabytes} MB, vượt giới hạn 20 MB. " +
                "Hãy nén hoặc tách tệp rồi tải lên lại.";
        }
        return null;
    }
}
