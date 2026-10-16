using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed partial class MaterialRateService
{
    public async Task<MaterialRateRevisionResponse?> SaveConfiguredRowsAsync(int catalogId, int revisionId,
        SaveBoqRowsRequest request, int userId, string? sourceFile = null, string? sourceHash = null, CancellationToken ct = default)
    {
        await using var transaction = await BeginRevisionMutationAsync(revisionId, ct);
        var revision = await db.MaterialRateRevisions.Include(item => item.Catalog).Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.CatalogId == catalogId, ct);
        if (revision is null) return null;
        var config = BoqConfigurationRules.Read(revision.Catalog.BoqConfigurationJson);
        if (revision.Catalog.CatalogType != MaterialRateCatalogType.Boq || config is null) throw BoqConfigurationRules.Error("configuration");
        if (revision.Status != MaterialRateRevisionStatus.Draft) throw BoqConfigurationRules.Error("draftOnly");
        if (request.ExpectedUpdatedAt != revision.UpdatedAt) throw new BoqConfigurationConflictException();
        if (request.ConfigurationHash != BoqConfigurationRules.Hash(revision.Catalog.BoqConfigurationJson)) throw BoqConfigurationRules.Error("wrongCategory");
        var rows = BoqConfigurationRules.ValidateRows(config, request.Rows);
        var lines = rows.Where(row => row.Priced != null).Select((row, index) =>
        {
            var values = ValidateBoqLine(row.Priced!);
            return new MaterialRateLine
            {
                MaterialCode = values.Code,
                MaterialName = values.Name,
                Unit = values.Unit,
                Quantity = values.Quantity,
                UnitRate = values.UnitPrice,
                AmountPerSqm = values.Amount,
                SortOrder = index + 1
            };
        }).ToList();
        // Validate everything before touching the graph. Reference rows never become quote prices.
        db.MaterialRateLines.RemoveRange(revision.Lines);
        revision.Lines = lines;
        revision.BoqRowsJson = JsonSerializer.Serialize(rows.Select(row => row.Row), BoqConfigurationRules.Json);
        revision.ImportSourceFileName = sourceFile;
        revision.ImportSourceHash = sourceHash;
        revision.ImportMappingJson = null;
        revision.ImportedAt = sourceFile == null ? null : DateTime.UtcNow;
        revision.ImportedByUserId = sourceFile == null ? null : userId;
        TouchRevision(revision, userId);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return await GetRevisionAsync(catalogId, revisionId, ct);
    }
}

public sealed class BoqConfigurationConflictException : Exception;
