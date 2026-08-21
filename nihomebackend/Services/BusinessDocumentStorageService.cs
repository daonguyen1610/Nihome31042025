using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public enum BusinessDocumentArea
{
    Vendors,
    Acceptance,
    AsBuilt,
    Handover,
    Permits,
}

public class BusinessDocumentStorageException(string message) : Exception(message)
{
}

public interface IBusinessDocumentStorageService
{
    Task<BusinessDocumentUploadResponse> StoreAsync(
        IFormFile? file,
        BusinessDocumentArea area,
        CancellationToken ct = default);

    ManagedDocumentContent? GetContent(BusinessDocumentArea area, string fileName);

    void Delete(string? path, BusinessDocumentArea area);
}

public class BusinessDocumentStorageService(IWebHostEnvironment env) : IBusinessDocumentStorageService
{
    public const long MaxFileSize = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
    };

    private static readonly IReadOnlyDictionary<BusinessDocumentArea, string> AreaFolders =
        new Dictionary<BusinessDocumentArea, string>
        {
            [BusinessDocumentArea.Vendors] = "vendors",
            [BusinessDocumentArea.Acceptance] = "acceptance",
            [BusinessDocumentArea.AsBuilt] = "as-built",
            [BusinessDocumentArea.Handover] = "handover",
            [BusinessDocumentArea.Permits] = "permits",
        };

    public async Task<BusinessDocumentUploadResponse> StoreAsync(
        IFormFile? file,
        BusinessDocumentArea area,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessDocumentStorageException("File is required and must not be empty.");
        }
        if (file.Length > MaxFileSize)
        {
            throw new BusinessDocumentStorageException("File size must not exceed 20 MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessDocumentStorageException(
                "Unsupported file type. Allowed types: PDF, Word, Excel, PNG, JPG, JPEG.");
        }
        if (!AreaFolders.TryGetValue(area, out var areaFolder))
        {
            throw new BusinessDocumentStorageException("Invalid business-document area.");
        }

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(
            env.ContentRootPath,
            "wwwroot",
            "files",
            "business-documents",
            areaFolder);
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, storedName);

        try
        {
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await file.CopyToAsync(output, ct);
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }

        return new BusinessDocumentUploadResponse
        {
            Path = $"/files/business-documents/{areaFolder}/{storedName}",
            OriginalFileName = Path.GetFileName(file.FileName),
            FileSize = file.Length,
            ContentType = file.ContentType,
        };
    }

    public ManagedDocumentContent? GetContent(BusinessDocumentArea area, string fileName)
    {
        if (!AreaFolders.TryGetValue(area, out var areaFolder)) return null;
        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal)) return null;
        var extension = Path.GetExtension(safeFileName);
        if (!AllowedExtensions.Contains(extension)) return null;

        var fullPath = Path.Combine(
            env.ContentRootPath, "wwwroot", "files", "business-documents", areaFolder, safeFileName);
        if (!File.Exists(fullPath)) return null;
        return new ManagedDocumentContent(fullPath, safeFileName, GetContentType(extension));
    }

    public void Delete(string? path, BusinessDocumentArea area)
    {
        if (string.IsNullOrWhiteSpace(path) || !AreaFolders.TryGetValue(area, out var areaFolder)) return;
        var expectedPrefix = $"/files/business-documents/{areaFolder}/";
        if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal)) return;
        var fileName = Path.GetFileName(path);
        var fullPath = Path.Combine(
            env.ContentRootPath, "wwwroot", "files", "business-documents", areaFolder, fileName);
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}