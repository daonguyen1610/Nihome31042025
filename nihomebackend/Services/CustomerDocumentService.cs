using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class CustomerDocumentService(
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<CustomerDocumentService> logger) : ICustomerDocumentService
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
    };

    public async Task<List<CustomerDocumentResponse>?> ListAsync(
        int customerId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CustomerExistsForCallerAsync(customerId, callerUserId, canSeeAll, ct)) return null;

        return await db.CustomerDocuments
            .AsNoTracking()
            .Where(document => document.CustomerId == customerId)
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.Id)
            .Select(document => new CustomerDocumentResponse
            {
                Id = document.Id,
                CustomerId = document.CustomerId,
                FilePath = document.FilePath,
                OriginalFileName = document.OriginalFileName,
                FileSize = document.FileSize,
                ContentType = document.ContentType,
                Label = document.Label,
                CreatedAt = document.CreatedAt,
                UploadedByUserId = document.UploadedByUserId,
                UploadedByName = document.UploadedBy != null ? document.UploadedBy.FullName : null,
            })
            .ToListAsync(ct);
    }

    public async Task<CustomerDocumentResponse?> UploadAsync(
        int customerId,
        IFormFile? file,
        string? label,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await CustomerExistsForCallerAsync(customerId, callerUserId, canSeeAll, ct)) return null;
        ValidateFile(file, label);

        var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
        var storageRoot = Path.Combine(env.ContentRootPath, "wwwroot", "files", "customers", customerId.ToString());
        Directory.CreateDirectory(storageRoot);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(storageRoot, storedName);

        CustomerDocument entity;
        try
        {
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream, ct);
            }

            entity = new CustomerDocument
            {
                CustomerId = customerId,
                FilePath = $"/files/customers/{customerId}/{storedName}",
                OriginalFileName = Path.GetFileName(file.FileName),
                FileSize = file.Length,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType.Trim(),
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                UploadedByUserId = callerUserId,
            };
            db.CustomerDocuments.Add(entity);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            DeleteManagedFile(fullPath);
            throw;
        }

        var uploaderName = await db.Users.AsNoTracking()
            .Where(user => user.Id == callerUserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(ct);
        logger.LogInformation("Uploaded customer document {DocumentId} for customer {CustomerId}", entity.Id, customerId);
        return Map(entity, uploaderName);
    }

    public async Task<ManagedDocumentContent?> GetContentAsync(
        int customerId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await CustomerExistsForCallerAsync(customerId, callerUserId, canSeeAll, ct)) return null;

        var document = await db.CustomerDocuments.AsNoTracking()
            .Where(item => item.Id == documentId && item.CustomerId == customerId)
            .Select(item => new { item.FilePath, item.OriginalFileName, item.ContentType })
            .SingleOrDefaultAsync(ct);
        if (document is null) return null;

        var fullPath = ToManagedFullPath(document.FilePath, customerId);
        return fullPath is not null && File.Exists(fullPath)
            ? new ManagedDocumentContent(fullPath, document.OriginalFileName, document.ContentType)
            : null;
    }

    public async Task<bool> DeleteAsync(
        int customerId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await CustomerExistsForCallerAsync(customerId, callerUserId, canSeeAll, ct)) return false;

        var entity = await db.CustomerDocuments
            .FirstOrDefaultAsync(document => document.Id == documentId && document.CustomerId == customerId, ct);
        if (entity is null) return false;

        db.CustomerDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
        DeleteManagedFile(ToManagedFullPath(entity.FilePath, customerId));
        logger.LogInformation("Deleted customer document {DocumentId} for customer {CustomerId}", documentId, customerId);
        return true;
    }

    public void DeleteCustomerFiles(int customerId)
    {
        var customerDirectory = Path.Combine(
            env.ContentRootPath, "wwwroot", "files", "customers", customerId.ToString());
        try
        {
            if (Directory.Exists(customerDirectory)) Directory.Delete(customerDirectory, recursive: true);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete document storage for customer {CustomerId}", customerId);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Could not delete document storage for customer {CustomerId}", customerId);
        }
    }

    private async Task<bool> CustomerExistsForCallerAsync(
        int customerId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => new { customer.OwnerUserId })
            .SingleOrDefaultAsync(ct);
        return customer is not null && (canSeeAll || customer.OwnerUserId == callerUserId);
    }

    private static void ValidateFile(IFormFile? file, string? label)
    {
        if (file is null || file.Length == 0) throw new CustomerDocumentException("Chưa chọn file.");
        if (file.Length > MaxFileSizeBytes) throw new CustomerDocumentException("File quá lớn (tối đa 20MB).");
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new CustomerDocumentException("Định dạng file không được hỗ trợ (PDF/DOC/DOCX/XLS/XLSX/PNG/JPG).");
        }
        if (Path.GetFileName(file.FileName).Length > 300) throw new CustomerDocumentException("Tên file quá dài.");
        if (label?.Trim().Length > 300) throw new CustomerDocumentException("Nhãn tài liệu quá dài.");
    }

    private string? ToManagedFullPath(string filePath, int customerId)
    {
        var expectedPrefix = $"/files/customers/{customerId}/";
        if (!filePath.StartsWith(expectedPrefix, StringComparison.Ordinal)) return null;
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        return Path.Combine(env.ContentRootPath, "wwwroot", "files", "customers", customerId.ToString(), fileName);
    }

    private static void DeleteManagedFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return;
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException)
        {
            // Metadata deletion remains authoritative; a later storage sweep can remove an orphan.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the API operation successful when storage cleanup is temporarily unavailable.
        }
    }

    private static CustomerDocumentResponse Map(CustomerDocument document, string? uploaderName) => new()
    {
        Id = document.Id,
        CustomerId = document.CustomerId,
        FilePath = document.FilePath,
        OriginalFileName = document.OriginalFileName,
        FileSize = document.FileSize,
        ContentType = document.ContentType,
        Label = document.Label,
        CreatedAt = document.CreatedAt,
        UploadedByUserId = document.UploadedByUserId,
        UploadedByName = uploaderName,
    };
}