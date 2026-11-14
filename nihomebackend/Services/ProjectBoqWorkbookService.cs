using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProjectBoqWorkbookPreview
{
    public string TemplateCode { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
    public List<BoqWorkbookSheetSummary> Sheets { get; init; } = [];
    public int ItemCount { get; init; }
    public int SkippedCount { get; init; }
    public int MissingPriceCount { get; init; }
    public int MissingUnitCount { get; init; }
    public decimal Total { get; init; }
}

public sealed class ProjectBoqWorkbookService(IProcurementService procurement, IBusinessDocumentStorageService storage)
{
    public async Task<ProjectBoqWorkbookPreview> PreviewAsync(int projectId, string? templateCode, IFormFile? file, CancellationToken ct)
    {
        await procurement.EnsureBoqUploadAllowedAsync(projectId, null, ct);
        var (template, parsed) = await ParseAsync(templateCode, file, ct);
        return new()
        {
            TemplateCode = template.Code,
            SourceHash = parsed.Sha256,
            Sheets = parsed.Sheets,
            ItemCount = parsed.Sheets.Sum(sheet => sheet.ItemCount),
            SkippedCount = parsed.Sheets.Sum(sheet => sheet.SkippedCount),
            MissingPriceCount = parsed.Sheets.Sum(sheet => sheet.MissingPriceCount),
            MissingUnitCount = parsed.Sheets.Sum(sheet => sheet.MissingUnitCount),
            Total = parsed.Sheets.Sum(sheet => sheet.Total),
        };
    }

    public async Task<ProjectBoqRevisionResponse> ImportAsync(
        int projectId, string? templateCode, IFormFile? file, string? sourceHash, int? upstreamContractId, int userId, CancellationToken ct,
        int? sourceWorkPackageId = null)
    {
        await procurement.EnsureBoqUploadAllowedAsync(projectId, upstreamContractId, ct);
        var (template, parsed) = await ParseAsync(templateCode, file, ct);
        if (!string.Equals(sourceHash, parsed.Sha256, StringComparison.OrdinalIgnoreCase)) throw new BoqWorkbookException("changedFile");
        var existing = await procurement.FindDraftWorkbookRevisionAsync(
            projectId, template.Code, parsed.Sha256, upstreamContractId, ct, sourceWorkPackageId);
        if (existing != null) return existing;
        var stored = await storage.StoreAsync(file, BusinessDocumentArea.Boq, ct);
        try
        {
            return await procurement.CreateBoqRevisionFromWorkbookAsync(projectId,
                new(template, Path.GetFileName(file!.FileName), stored.Path, upstreamContractId, parsed, sourceWorkPackageId), userId, ct);
        }
        catch
        {
            storage.Delete(stored.Path, BusinessDocumentArea.Boq);
            throw;
        }
    }

    public async Task<ManagedDocumentContent?> GetWorkbookAsync(int projectId, int revisionId, CancellationToken ct)
    {
        var path = await procurement.GetBoqWorkbookPathAsync(projectId, revisionId, ct);
        return path == null ? null : storage.GetContent(BusinessDocumentArea.Boq, Path.GetFileName(path));
    }

    private static async Task<(BoqTemplate Template, BoqWorkbookParseResult Parsed)> ParseAsync(string? templateCode, IFormFile? file, CancellationToken ct)
    {
        var template = ProjectBoqTemplateCatalog.Find(templateCode) ?? throw new BoqWorkbookException("template");
        if (file == null || file.Length is <= 0 or > ProjectBoqWorkbookParser.MaxFileBytes ||
            !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BoqWorkbookException("file");
        using var bytes = new MemoryStream();
        await file.CopyToAsync(bytes, ct);
        var parsed = ProjectBoqWorkbookParser.Parse(bytes.ToArray(), template);
        if (!parsed.IsValid) throw new BoqWorkbookException("invalidRows", parsed);
        return (template, parsed);
    }
}
