using System.Globalization;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Type and size rules for business documents uploaded to design records,
/// tenders, capability documents and contracts. Each module's size limit is an
/// administrator setting (Cài đặt → Giới hạn tệp …, see <see cref="UploadLimitKind"/>).
/// </summary>
public static class BusinessFileRules
{
    /// <summary>Highest limit an administrator may set, as for other project documents.</summary>
    public const int MaxAllowedMb = (int)(ProjectDocumentStorageService.MaxFileSize / (1024 * 1024));

    /// <summary>Highest limit an administrator may set for a module; contract files go up to 5 GB.</summary>
    public static int MaxAllowedMbFor(UploadLimitKind kind) =>
        kind == UploadLimitKind.Contracts ? ProjectDocumentStorageService.ContractMaxFileSizeMb : MaxAllowedMb;

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Why the file cannot be attached, in Vietnamese; null when it can.</summary>
    public static string? Problem(IFormFile file, int maxMb) => Problem(file.FileName, file.Length, maxMb);

    /// <summary>
    /// Why a file with this name and size cannot be attached, in Vietnamese; null
    /// when it can. Used before a chunked upload starts, when only those are known.
    /// </summary>
    public static string? Problem(string fileName, long length, int maxMb)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            var shown = string.IsNullOrWhiteSpace(extension) ? "không có đuôi" : extension.ToLowerInvariant();
            return $"Tệp “{fileName}” ({shown}) chưa được nhận. " +
                "Chỉ nhận PDF, Word (.doc, .docx), Excel (.xls, .xlsx) và ảnh PNG, JPG.";
        }
        if (length == 0)
            return $"Tệp “{fileName}” trống. Hãy kiểm tra lại tệp rồi tải lên.";
        if (length > (long)maxMb * 1024 * 1024)
        {
            var megabytes = (length / 1024d / 1024d).ToString("0.0", Vietnamese);
            return $"Tệp “{fileName}” nặng {megabytes} MB, vượt giới hạn {maxMb} MB. " +
                "Hãy nén hoặc tách tệp rồi tải lên lại.";
        }
        return null;
    }
}
