using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

// Reads, stores and serves the investment-rate forms uploaded in input-price work packages.
public sealed class InvestmentRateWorkbookService(AppDbContext db, IBusinessDocumentStorageService storage)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<InvestmentRateWorkbookPreviewResponse> PreviewAsync(string? templateCode, IFormFile? file, CancellationToken ct)
    {
        var (template, parsed) = await ParseAsync(templateCode, file, ct);
        return new()
        {
            TemplateCode = template.Code,
            SourceHash = parsed.Report.Sha256,
            ConvertedAreaSqm = parsed.ConvertedArea,
            RatePerSqm = parsed.RatePerSqm,
            Total = parsed.Total,
            Areas = parsed.Areas.Select(MapArea).ToList(),
            Groups = parsed.Groups.Select(MapGroup).ToList(),
        };
    }

    // Returns the new Draft's id, or the existing Draft's id when the same file is confirmed again.
    public async Task<int> ImportAsync(int projectId, int packageId, string? templateCode, IFormFile? file, string? sourceHash,
        int userId, CancellationToken ct)
    {
        var status = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => (OperationalProjectStatus?)item.Status).SingleOrDefaultAsync(ct);
        if (status is null or OperationalProjectStatus.Completed or OperationalProjectStatus.Cancelled)
            throw new DepartmentWorkPackageOperationException("Không thể upload suất đầu tư cho dự án đã hoàn thành hoặc đã hủy.");
        var (template, parsed) = await ParseAsync(templateCode, file, ct);
        if (!string.Equals(sourceHash, parsed.Report.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new BoqWorkbookException("changedFile");
        var existing = await db.ProjectInvestmentRateRevisions.AsNoTracking()
            .Where(item => item.SourceWorkPackageId == packageId && item.Status == ProjectInvestmentRateStatus.Draft &&
                item.TemplateCode == template.Code && item.SourceSha256 == parsed.Report.Sha256)
            .Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
        if (existing.HasValue) return existing.Value;

        var stored = await storage.StoreAsync(file, BusinessDocumentArea.Boq, ct);
        try
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;
            var revisions = db.Database.IsSqlServer()
                ? db.ProjectInvestmentRateRevisions.FromSqlRaw("SELECT * FROM [project_investment_rate_revisions] WITH (UPDLOCK)")
                : db.ProjectInvestmentRateRevisions;
            var now = DateTime.UtcNow;
            var entity = new ProjectInvestmentRateRevision
            {
                OperationalProjectId = projectId,
                SourceWorkPackageId = packageId,
                RevisionNumber = (await revisions.Where(item => item.OperationalProjectId == projectId)
                    .MaxAsync(item => (int?)item.RevisionNumber, ct) ?? 0) + 1,
                TemplateCode = template.Code,
                SourceFileName = Path.GetFileName(file!.FileName),
                SourceSha256 = parsed.Report.Sha256,
                SourceWorkbookPath = stored.Path,
                Currency = "VND",
                ConvertedAreaSqm = parsed.ConvertedArea,
                RatePerSqm = parsed.RatePerSqm,
                Total = parsed.Total,
                AreaLinesJson = JsonSerializer.Serialize(parsed.Areas.Select(MapArea), Json),
                GroupLinesJson = JsonSerializer.Serialize(parsed.Groups.Select(MapGroup), Json),
                PreparedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.ProjectInvestmentRateRevisions.Add(entity);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return entity.Id;
        }
        catch
        {
            storage.Delete(stored.Path, BusinessDocumentArea.Boq);
            throw;
        }
    }

    public async Task<ManagedDocumentContent?> GetWorkbookAsync(int packageId, int revisionId, CancellationToken ct)
    {
        var revision = await db.ProjectInvestmentRateRevisions.AsNoTracking()
            .Where(item => item.Id == revisionId && item.SourceWorkPackageId == packageId)
            .Select(item => new { item.SourceWorkbookPath, item.SourceFileName }).SingleOrDefaultAsync(ct);
        if (revision is null) return null;
        var content = storage.GetContent(BusinessDocumentArea.Boq, Path.GetFileName(revision.SourceWorkbookPath));
        return content is null ? null : content with { OriginalFileName = revision.SourceFileName };
    }

    public static List<InvestmentRateAreaLineResponse> ReadAreas(string json) =>
        JsonSerializer.Deserialize<List<InvestmentRateAreaLineResponse>>(json, Json) ?? [];

    public static List<InvestmentRateGroupLineResponse> ReadGroups(string json) =>
        JsonSerializer.Deserialize<List<InvestmentRateGroupLineResponse>>(json, Json) ?? [];

    private static async Task<(InvestmentRateTemplate Template, InvestmentRateParseResult Parsed)> ParseAsync(
        string? templateCode, IFormFile? file, CancellationToken ct)
    {
        var template = InvestmentRateTemplateCatalog.Find(templateCode) ?? throw new BoqWorkbookException("template");
        if (file == null || file.Length is <= 0 or > ProjectBoqWorkbookParser.MaxFileBytes ||
            !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BoqWorkbookException("file");
        using var bytes = new MemoryStream();
        await file.CopyToAsync(bytes, ct);
        var parsed = InvestmentRateWorkbookParser.Parse(bytes.ToArray(), template);
        if (!parsed.IsValid) throw new BoqWorkbookException("invalidRows", parsed.Report);
        return (template, parsed);
    }

    private static InvestmentRateAreaLineResponse MapArea(InvestmentRateAreaLine line) => new()
    {
        Name = line.Name,
        Description = line.Description,
        ActualArea = line.ActualArea,
        Factor = line.Factor,
        ConvertedArea = line.ConvertedArea,
        Note = line.Note,
    };

    private static InvestmentRateGroupLineResponse MapGroup(InvestmentRateGroupLine line) => new()
    {
        Code = line.Code,
        Name = line.Name,
        Description = line.Description,
        Material = line.Material,
        Labour = line.Labour,
        Equipment = line.Equipment,
        RatePerSqm = line.RatePerSqm,
        Specification = line.Specification,
    };
}
