using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class QuoteDocumentService(
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<QuoteDocumentService> logger) : IQuoteDocumentService
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
    };

    public async Task<List<QuoteDocumentResponse>?> ListAsync(
        int quoteId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await QuoteExistsForCallerAsync(quoteId, callerUserId, canSeeAll, ct)) return null;

        return await db.QuoteDocuments
            .AsNoTracking()
            .Where(document => document.QuoteId == quoteId)
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.Id)
            .Select(document => new QuoteDocumentResponse
            {
                Id = document.Id,
                QuoteId = document.QuoteId,
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

    public async Task<QuoteDocumentResponse?> UploadAsync(
        int quoteId,
        IFormFile? file,
        string? label,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await QuoteExistsForCallerAsync(quoteId, callerUserId, canSeeAll, ct)) return null;
        ValidateFile(file, label);

        var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
        var storageRoot = GetQuoteDirectory(quoteId);
        Directory.CreateDirectory(storageRoot);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(storageRoot, storedName);

        QuoteDocument entity;
        try
        {
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream, ct);
            }

            entity = new QuoteDocument
            {
                QuoteId = quoteId,
                FilePath = $"/files/quotes/{quoteId}/{storedName}",
                OriginalFileName = Path.GetFileName(file.FileName),
                FileSize = file.Length,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType.Trim(),
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                UploadedByUserId = callerUserId,
            };
            db.QuoteDocuments.Add(entity);
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
        logger.LogInformation("Uploaded quote document {DocumentId} for quote {QuoteId}", entity.Id, quoteId);
        return Map(entity, uploaderName);
    }

    public async Task<QuoteDocumentContent?> GetContentAsync(
        int quoteId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await QuoteExistsForCallerAsync(quoteId, callerUserId, canSeeAll, ct)) return null;

        var document = await db.QuoteDocuments.AsNoTracking()
            .Where(item => item.Id == documentId && item.QuoteId == quoteId)
            .Select(item => new { item.FilePath, item.OriginalFileName, item.ContentType })
            .SingleOrDefaultAsync(ct);
        if (document is null) return null;

        var fullPath = ToManagedFullPath(document.FilePath, quoteId);
        return fullPath is not null && File.Exists(fullPath)
            ? new QuoteDocumentContent(fullPath, document.OriginalFileName, document.ContentType)
            : null;
    }

    public async Task<bool> DeleteAsync(
        int quoteId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!await QuoteExistsForCallerAsync(quoteId, callerUserId, canSeeAll, ct)) return false;

        var entity = await db.QuoteDocuments
            .FirstOrDefaultAsync(document => document.Id == documentId && document.QuoteId == quoteId, ct);
        if (entity is null) return false;

        db.QuoteDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
        DeleteManagedFile(ToManagedFullPath(entity.FilePath, quoteId));
        logger.LogInformation("Deleted quote document {DocumentId} for quote {QuoteId}", documentId, quoteId);
        return true;
    }

    public void DeleteQuoteFiles(int quoteId)
    {
        var quoteDirectory = GetQuoteDirectory(quoteId);
        try
        {
            if (Directory.Exists(quoteDirectory)) Directory.Delete(quoteDirectory, recursive: true);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete document storage for quote {QuoteId}", quoteId);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Could not delete document storage for quote {QuoteId}", quoteId);
        }
    }

    private async Task<bool> QuoteExistsForCallerAsync(
        int quoteId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var quote = await db.Quotes.AsNoTracking()
            .Where(item => item.Id == quoteId)
            .Select(item => new { item.OwnerUserId })
            .SingleOrDefaultAsync(ct);
        return quote is not null && (canSeeAll || quote.OwnerUserId == callerUserId);
    }

    private static void ValidateFile(IFormFile? file, string? label)
    {
        if (file is null || file.Length == 0) throw new QuoteDocumentException("Chưa chọn file.");
        if (file.Length > MaxFileSizeBytes) throw new QuoteDocumentException("File quá lớn (tối đa 20MB).");
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new QuoteDocumentException("Định dạng file không được hỗ trợ (PDF/DOC/DOCX/XLS/XLSX/PNG/JPG).");
        }
        if (Path.GetFileName(file.FileName).Length > 300) throw new QuoteDocumentException("Tên file quá dài.");
        if (label?.Trim().Length > 300) throw new QuoteDocumentException("Nhãn tài liệu quá dài.");
    }

    private string? ToManagedFullPath(string filePath, int quoteId)
    {
        var expectedPrefix = $"/files/quotes/{quoteId}/";
        if (!filePath.StartsWith(expectedPrefix, StringComparison.Ordinal)) return null;
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        return Path.Combine(GetQuoteDirectory(quoteId), fileName);
    }

    private string GetQuoteDirectory(int quoteId) =>
        Path.Combine(env.ContentRootPath, "storage", "quotes", quoteId.ToString());

    private static void DeleteManagedFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return;
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

    private static QuoteDocumentResponse Map(QuoteDocument document, string? uploaderName) => new()
    {
        Id = document.Id,
        QuoteId = document.QuoteId,
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
