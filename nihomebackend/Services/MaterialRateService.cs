using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IMaterialRateService
{
    Task<List<MaterialRateCatalogResponse>> ListCatalogsAsync(string? search, bool includeInactive, MaterialRateCatalogType? catalogType = null, CancellationToken ct = default);
    Task<MaterialRateCatalogResponse?> GetCatalogAsync(int id, CancellationToken ct = default);
    Task<MaterialRateCatalogResponse> CreateCatalogAsync(UpsertMaterialRateCatalogRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRateCatalogResponse?> UpdateCatalogAsync(int id, UpsertMaterialRateCatalogRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRateCatalogResponse?> DeleteCatalogAsync(int id, CancellationToken ct = default);
    Task<List<MaterialRateRevisionResponse>?> ListRevisionsAsync(int catalogId, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> GetRevisionAsync(int catalogId, int revisionId, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> CreateRevisionAsync(int catalogId, CreateMaterialRateRevisionRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRateImportResponse?> ImportAsync(
        int catalogId,
        int revisionId,
        Stream stream,
        int userId,
        string fileName = "upload.csv",
        CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> ApproveAsync(int catalogId, int revisionId, string? note, int userId, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> RejectAsync(int catalogId, int revisionId, string? note, int userId, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> RetireAsync(int catalogId, int revisionId, string? note, int userId, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> GetEffectiveAsync(int catalogId, DateOnly effectiveDate, CancellationToken ct = default);
}

public sealed class MaterialRateService(
    AppDbContext db,
    IUtf8CsvParser csvParser,
    IMaterialRateSpreadsheetService spreadsheetService) : IMaterialRateService
{
    public static readonly IReadOnlyList<string> CsvHeaders =
    [
        "MaterialCode",
        "MaterialName",
        "Unit",
        "NormPerSqm",
        "UnitRate",
        "WastePercent",
    ];

    public static readonly IReadOnlyList<string> BoqCsvHeaders =
    [
        "ItemCode",
        "ItemName",
        "Unit",
        "Quantity",
        "UnitPrice",
    ];

    public Task<List<MaterialRateCatalogResponse>> ListCatalogsAsync(
        string? search,
        bool includeInactive,
        MaterialRateCatalogType? catalogType = null,
        CancellationToken ct = default)
    {
        var query = db.MaterialRateCatalogs.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(catalog => catalog.IsActive);
        if (catalogType.HasValue) query = query.Where(catalog => catalog.CatalogType == catalogType.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(catalog =>
                EF.Functions.Like(catalog.Code, pattern) || EF.Functions.Like(catalog.Name, pattern));
        }

        return query
            .OrderBy(catalog => catalog.Code)
            .Select(catalog => new MaterialRateCatalogResponse
            {
                Id = catalog.Id,
                CatalogType = catalog.CatalogType,
                Code = catalog.Code,
                Name = catalog.Name,
                Description = catalog.Description,
                Currency = catalog.Currency,
                IsActive = catalog.IsActive,
                RevisionCount = catalog.Revisions.Count,
                CreatedAt = catalog.CreatedAt,
                UpdatedAt = catalog.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<MaterialRateCatalogResponse?> GetCatalogAsync(int id, CancellationToken ct = default)
    {
        var catalog = await db.MaterialRateCatalogs
            .AsNoTracking()
            .Include(item => item.Revisions)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        return catalog is null ? null : MapCatalog(catalog);
    }

    public async Task<MaterialRateCatalogResponse> CreateCatalogAsync(
        UpsertMaterialRateCatalogRequest request,
        int userId,
        CancellationToken ct = default)
    {
        ValidateCatalogName(request.Name);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MaterialRateCatalogs.AnyAsync(item => item.Code == code, ct))
        {
            throw new MaterialRateOperationException($"Mã danh mục đơn giá '{code}' đã tồn tại.");
        }

        var now = DateTime.UtcNow;
        var catalog = new MaterialRateCatalog
        {
            CatalogType = request.CatalogType,
            Code = code,
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        };
        db.MaterialRateCatalogs.Add(catalog);
        await db.SaveChangesAsync(ct);
        return MapCatalog(catalog);
    }

    public async Task<MaterialRateCatalogResponse?> UpdateCatalogAsync(
        int id,
        UpsertMaterialRateCatalogRequest request,
        int userId,
        CancellationToken ct = default)
    {
        var catalog = await db.MaterialRateCatalogs.Include(item => item.Revisions).FirstOrDefaultAsync(item => item.Id == id, ct);
        if (catalog is null) return null;

        ValidateCatalogName(request.Name);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.MaterialRateCatalogs.AnyAsync(item => item.Id != id && item.Code == code, ct))
        {
            throw new MaterialRateOperationException($"Mã danh mục đơn giá '{code}' đã tồn tại.");
        }
        var currency = request.Currency.Trim().ToUpperInvariant();
        if (catalog.Revisions.Any(revision => revision.Status is MaterialRateRevisionStatus.Approved or MaterialRateRevisionStatus.Retired) &&
            (!string.Equals(catalog.Code, code, StringComparison.Ordinal) ||
             !string.Equals(catalog.Currency, currency, StringComparison.Ordinal) ||
             catalog.CatalogType != request.CatalogType))
        {
            throw new MaterialRateOperationException("Không được đổi loại, mã danh mục hoặc tiền tệ sau khi đã có phiên bản được duyệt.");
        }

        catalog.CatalogType = request.CatalogType;
        catalog.Code = code;
        catalog.Name = request.Name.Trim();
        catalog.Description = Clean(request.Description);
        catalog.Currency = currency;
        catalog.IsActive = request.IsActive;
        catalog.UpdatedAt = DateTime.UtcNow;
        catalog.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return MapCatalog(catalog);
    }

    public async Task<MaterialRateCatalogResponse?> DeleteCatalogAsync(int id, CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var catalog = await db.MaterialRateCatalogs
            .Include(item => item.Revisions)
            .ThenInclude(revision => revision.Lines)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (catalog is null) return null;

        var revisionIds = catalog.Revisions.Select(revision => revision.Id).ToList();
        if (revisionIds.Count > 0 &&
            (await db.Quotes.AnyAsync(quote =>
                 quote.MaterialRateRevisionId.HasValue && revisionIds.Contains(quote.MaterialRateRevisionId.Value), ct) ||
             await db.QuoteVersionSnapshots.AnyAsync(snapshot =>
                 snapshot.MaterialRateRevisionId.HasValue && revisionIds.Contains(snapshot.MaterialRateRevisionId.Value), ct)))
        {
            throw DeleteBlocked();
        }

        var response = MapCatalog(catalog);
        db.MaterialRateCatalogs.Remove(catalog);
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw DeleteBlocked();
        }
        return response;
    }

    public async Task<List<MaterialRateRevisionResponse>?> ListRevisionsAsync(int catalogId, CancellationToken ct = default)
    {
        if (!await db.MaterialRateCatalogs.AnyAsync(item => item.Id == catalogId, ct)) return null;
        var revisions = await db.MaterialRateRevisions
            .AsNoTracking()
            .Include(item => item.Catalog)
            .Include(item => item.Lines)
            .Where(item => item.CatalogId == catalogId)
            .OrderByDescending(item => item.Version)
            .ToListAsync(ct);
        return revisions.Select(MapRevision).ToList();
    }

    public async Task<MaterialRateRevisionResponse?> GetRevisionAsync(
        int catalogId,
        int revisionId,
        CancellationToken ct = default)
    {
        var revision = await RevisionQuery()
            .FirstOrDefaultAsync(item => item.Id == revisionId && item.CatalogId == catalogId, ct);
        return revision is null ? null : MapRevision(revision);
    }

    public async Task<MaterialRateRevisionResponse?> CreateRevisionAsync(
        int catalogId,
        CreateMaterialRateRevisionRequest request,
        int userId,
        CancellationToken ct = default)
    {
        if (!await db.MaterialRateCatalogs.AnyAsync(item => item.Id == catalogId, ct)) return null;
        ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo);

        var version = 1 + (await db.MaterialRateRevisions
            .Where(item => item.CatalogId == catalogId)
            .Select(item => (int?)item.Version)
            .MaxAsync(ct) ?? 0);
        var now = DateTime.UtcNow;
        var revision = new MaterialRateRevision
        {
            CatalogId = catalogId,
            Version = version,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Note = Clean(request.Note),
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        };
        db.MaterialRateRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        return await GetRevisionAsync(catalogId, revision.Id, ct);
    }

    public async Task<MaterialRateImportResponse?> ImportAsync(
        int catalogId,
        int revisionId,
        Stream stream,
        int userId,
        string fileName = "upload.csv",
        CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var revision = await db.MaterialRateRevisions
            .Include(item => item.Catalog)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == revisionId && item.CatalogId == catalogId, ct);
        if (revision is null) return null;
        if (revision.Status != MaterialRateRevisionStatus.Draft)
        {
            throw new MaterialRateOperationException("Chỉ phiên bản Nháp mới được phép nhập dữ liệu đơn giá.");
        }

        var isExcel = Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        var headers = revision.Catalog.CatalogType == MaterialRateCatalogType.Boq ? BoqCsvHeaders : CsvHeaders;
        var parsed = isExcel
            ? spreadsheetService.Parse(stream, revision.Catalog.CatalogType)
            : await csvParser.ParseAsync(stream, headers, maxBytes: 5 * 1024 * 1024, ct: ct);
        if (!parsed.IsValid) return new MaterialRateImportResponse { Errors = parsed.Errors };

        var errors = new List<CsvImportError>();
        var lines = new List<MaterialRateLine>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < parsed.Rows.Count; index++)
        {
            var rowNumber = index < parsed.SourceRowNumbers.Count ? parsed.SourceRowNumbers[index] : index + 2;
            var row = parsed.Rows[index];
            var isBoq = revision.Catalog.CatalogType == MaterialRateCatalogType.Boq;
            var codeField = isBoq ? "ItemCode" : "MaterialCode";
            var nameField = isBoq ? "ItemName" : "MaterialName";
            var code = row[codeField].Trim();
            var name = row[nameField].Trim();
            var unit = row["Unit"].Trim();

            ValidateText(code, isBoq ? 60 : 50, codeField, rowNumber, errors);
            ValidateText(name, isBoq ? 300 : 200, nameField, rowNumber, errors);
            ValidateText(unit, 30, "Unit", rowNumber, errors);
            if (code.Length > 0 && !codes.Add(code))
            {
                errors.Add(Error(rowNumber, 1, $"{codeField} '{code}' bị trùng trong tệp.",
                    "materialRates.csvError.duplicateCode", new() { ["code"] = code }));
            }

            var quantity = isBoq ? ParseDecimal(row["Quantity"], "Quantity", rowNumber, 4, 4, errors) : 0m;
            var norm = isBoq ? 0m : ParseDecimal(row["NormPerSqm"], "NormPerSqm", rowNumber, 4, 6, errors);
            var rateField = isBoq ? "UnitPrice" : "UnitRate";
            var rate = ParseDecimal(row[rateField], rateField, rowNumber, 5, isBoq ? 2 : 4, errors);
            var waste = isBoq ? 0m : ParseDecimal(row["WastePercent"], "WastePercent", rowNumber, 6, 4, errors);
            if (isBoq && quantity is <= 0) errors.Add(Error(rowNumber, 4, "Quantity phải lớn hơn 0.", "materialRates.csvError.quantityPositive", new() { ["field"] = "Quantity" }));
            if (isBoq && quantity is > 999999999999.999999m) errors.Add(Error(rowNumber, 4, "Quantity vượt quá giới hạn lưu trữ.", "materialRates.csvError.quantityMaximum", new() { ["field"] = "Quantity" }));
            if (!isBoq && norm is <= 0) errors.Add(Error(rowNumber, 4, "NormPerSqm phải lớn hơn 0.", "materialRates.csvError.normPositive", new() { ["field"] = "NormPerSqm" }));
            if (!isBoq && norm is > 999999999999.999999m) errors.Add(Error(rowNumber, 4, "NormPerSqm vượt quá giới hạn lưu trữ.", "materialRates.csvError.normMaximum", new() { ["field"] = "NormPerSqm" }));
            if (rate is < 0) errors.Add(Error(rowNumber, 5, $"{rateField} không được nhỏ hơn 0.", "materialRates.csvError.rateNonNegative", new() { ["field"] = rateField }));
            if (rate is > 99999999999999.9999m) errors.Add(Error(rowNumber, 5, $"{rateField} vượt quá giới hạn lưu trữ.", "materialRates.csvError.rateMaximum", new() { ["field"] = rateField }));
            if (!isBoq && waste is (< 0 or > 100)) errors.Add(Error(rowNumber, 6, "WastePercent phải nằm trong khoảng từ 0 đến 100.", "materialRates.csvError.wasteRange", new() { ["field"] = "WastePercent" }));

            if (code.Length == 0 || name.Length == 0 || unit.Length == 0 || quantity is null || norm is null || rate is null || waste is null)
            {
                continue;
            }

            try
            {
                var amount = decimal.Round(
                    isBoq ? quantity.Value * rate.Value : norm.Value * rate.Value * (1m + waste.Value / 100m),
                    4,
                    MidpointRounding.AwayFromZero);
                if (amount > 99999999999999.9999m)
                {
                    var amountField = isBoq ? "TotalAmount" : "AmountPerSqm";
                    errors.Add(Error(rowNumber, null, $"{amountField} vượt quá giới hạn lưu trữ.", "materialRates.csvError.amountMaximum", new() { ["field"] = amountField }));
                    continue;
                }
                lines.Add(new MaterialRateLine
                {
                    MaterialCode = code,
                    MaterialName = name,
                    Unit = unit,
                    Quantity = quantity.Value,
                    NormPerSqm = norm.Value,
                    UnitRate = rate.Value,
                    WastePercent = waste.Value,
                    AmountPerSqm = amount,
                    SortOrder = index + 1,
                });
            }
            catch (OverflowException)
            {
                errors.Add(Error(rowNumber, null, "Giá trị dòng vượt quá giới hạn tính toán AmountPerSqm.", "materialRates.csvError.calculationOverflow"));
            }
        }

        if (parsed.Rows.Count == 0)
        {
            var firstDataRow = isExcel ? 5 : 2;
            errors.Add(Error(firstDataRow, null, "Tệp phải có ít nhất một dòng dữ liệu.", "materialRates.csvError.dataRequired"));
        }
        if (errors.Count > 0) return new MaterialRateImportResponse { Errors = errors };

        db.MaterialRateLines.RemoveRange(revision.Lines);
        revision.Lines = lines;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new MaterialRateImportResponse { ImportedCount = lines.Count };
    }

    public Task<MaterialRateRevisionResponse?> ApproveAsync(
        int catalogId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default) => DecideAsync(catalogId, revisionId, MaterialRateRevisionStatus.Approved, note, userId, ct);

    public Task<MaterialRateRevisionResponse?> RejectAsync(
        int catalogId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new MaterialRateOperationException("Vui lòng nhập lý do từ chối phiên bản đơn giá.");
        }
        return DecideAsync(catalogId, revisionId, MaterialRateRevisionStatus.Rejected, note, userId, ct);
    }

    public async Task<MaterialRateRevisionResponse?> RetireAsync(
        int catalogId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default)
    {
        var revision = await db.MaterialRateRevisions
            .FirstOrDefaultAsync(item => item.Id == revisionId && item.CatalogId == catalogId, ct);
        if (revision is null) return null;
        if (revision.Status != MaterialRateRevisionStatus.Approved)
        {
            throw new MaterialRateOperationException("Chỉ phiên bản Đã duyệt mới có thể chuyển sang Ngừng áp dụng.");
        }
        ApplyDecision(revision, MaterialRateRevisionStatus.Retired, note, userId);
        await db.SaveChangesAsync(ct);
        return await GetRevisionAsync(catalogId, revisionId, ct);
    }

    public async Task<MaterialRateRevisionResponse?> GetEffectiveAsync(
        int catalogId,
        DateOnly effectiveDate,
        CancellationToken ct = default)
    {
        var revision = await RevisionQuery()
            .Where(item => item.CatalogId == catalogId &&
                item.Catalog.IsActive &&
                item.Status == MaterialRateRevisionStatus.Approved &&
                item.EffectiveFrom <= effectiveDate &&
                (item.EffectiveTo == null || item.EffectiveTo >= effectiveDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        return revision is null ? null : MapRevision(revision);
    }

    private async Task<MaterialRateRevisionResponse?> DecideAsync(
        int catalogId,
        int revisionId,
        MaterialRateRevisionStatus status,
        string? note,
        int userId,
        CancellationToken ct)
    {
        await using var transaction = status == MaterialRateRevisionStatus.Approved && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var revision = await db.MaterialRateRevisions
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == revisionId && item.CatalogId == catalogId, ct);
        if (revision is null) return null;
        if (revision.Status != MaterialRateRevisionStatus.Draft)
        {
            throw new MaterialRateOperationException("Chỉ phiên bản Nháp mới có thể được phê duyệt hoặc từ chối.");
        }
        if (status == MaterialRateRevisionStatus.Approved)
        {
            if (revision.Lines.Count == 0)
            {
                throw new MaterialRateOperationException("Phiên bản phải có ít nhất một dòng đơn giá trước khi phê duyệt.");
            }
            var overlaps = await db.MaterialRateRevisions.AsNoTracking().AnyAsync(item =>
                item.Id != revisionId &&
                item.CatalogId == catalogId &&
                item.Status == MaterialRateRevisionStatus.Approved &&
                (item.EffectiveTo == null || item.EffectiveTo >= revision.EffectiveFrom) &&
                (revision.EffectiveTo == null || item.EffectiveFrom <= revision.EffectiveTo), ct);
            if (overlaps)
            {
                throw new MaterialRateOperationException("Khoảng thời gian hiệu lực bị trùng với một phiên bản đã duyệt của cùng danh mục.");
            }
        }

        ApplyDecision(revision, status, note, userId);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetRevisionAsync(catalogId, revisionId, ct);
    }

    private static void ApplyDecision(MaterialRateRevision revision, MaterialRateRevisionStatus status, string? note, int userId)
    {
        var now = DateTime.UtcNow;
        revision.Status = status;
        revision.DecisionNote = Clean(note);
        revision.DecidedAt = now;
        revision.DecidedByUserId = userId;
        revision.UpdatedAt = now;
        revision.UpdatedByUserId = userId;
    }

    private IQueryable<MaterialRateRevision> RevisionQuery() => db.MaterialRateRevisions
        .AsNoTracking()
        .Include(item => item.Catalog)
        .Include(item => item.Lines);

    private static void ValidateEffectiveRange(DateOnly from, DateOnly? to)
    {
        if (from == default)
        {
            throw new MaterialRateOperationException("Vui lòng nhập ngày bắt đầu hiệu lực, ví dụ: 2026-09-01.");
        }
        if (to < from)
        {
            throw new MaterialRateOperationException("Ngày kết thúc hiệu lực không được trước ngày bắt đầu.");
        }
    }

    private static void ValidateCatalogName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MaterialRateOperationException("Vui lòng nhập tên danh mục đơn giá, ví dụ: Đơn giá nội thất 2026.");
        }
    }

    private static decimal? ParseDecimal(
        string value,
        string field,
        int row,
        int column,
        int maxScale,
        List<CsvImportError> errors)
    {
        if (!decimal.TryParse(value.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            errors.Add(Error(row, column, $"{field} phải là số thập phân theo định dạng invariant, ví dụ: 12.5.",
                "materialRates.csvError.decimal", new() { ["field"] = field }));
            return null;
        }
        if (decimal.Round(parsed, maxScale) != parsed)
        {
            errors.Add(Error(row, column,
                $"{field} chỉ được có tối đa {maxScale} chữ số thập phân, ví dụ: 12.5.",
                "materialRates.csvError.scale", new() { ["field"] = field, ["max"] = maxScale }));
            return null;
        }
        return parsed;
    }

    private static void ValidateText(string value, int maxLength, string field, int row, List<CsvImportError> errors)
    {
        if (value.Length == 0)
        {
            errors.Add(Error(row, null, $"{field} không được để trống.",
                "materialRates.csvError.required", new() { ["field"] = field }));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(Error(row, null, $"{field} không được vượt quá {maxLength} ký tự.",
                "materialRates.csvError.maxLength", new() { ["field"] = field, ["max"] = maxLength }));
        }
    }

    private static CsvImportError Error(
        int row,
        int? column,
        string message,
        string messageKey,
        Dictionary<string, object>? messageArgs = null) => new()
        {
            Row = row,
            Column = column,
            Message = message,
            MessageKey = messageKey,
            MessageArgs = messageArgs,
        };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MaterialRateOperationException DeleteBlocked() => new(
        "Không thể xoá danh mục đơn giá đã được sử dụng trong Báo giá hoặc lịch sử phiên bản Báo giá.",
        "materialRates.catalog.deleteBlocked");

    private static MaterialRateCatalogResponse MapCatalog(MaterialRateCatalog catalog) => new()
    {
        Id = catalog.Id,
        CatalogType = catalog.CatalogType,
        Code = catalog.Code,
        Name = catalog.Name,
        Description = catalog.Description,
        Currency = catalog.Currency,
        IsActive = catalog.IsActive,
        RevisionCount = catalog.Revisions.Count,
        CreatedAt = catalog.CreatedAt,
        UpdatedAt = catalog.UpdatedAt,
    };

    private static MaterialRateRevisionResponse MapRevision(MaterialRateRevision revision) => new()
    {
        Id = revision.Id,
        CatalogId = revision.CatalogId,
        CatalogCode = revision.Catalog.Code,
        CatalogName = revision.Catalog.Name,
        CatalogType = revision.Catalog.CatalogType,
        Currency = revision.Catalog.Currency,
        Version = revision.Version,
        Status = revision.Status,
        EffectiveFrom = revision.EffectiveFrom,
        EffectiveTo = revision.EffectiveTo,
        Note = revision.Note,
        DecisionNote = revision.DecisionNote,
        DecidedAt = revision.DecidedAt,
        CreatedAt = revision.CreatedAt,
        UpdatedAt = revision.UpdatedAt,
        TotalRatePerSqm = revision.TotalRatePerSqm,
        TotalAmount = revision.Lines.Sum(line => line.AmountPerSqm),
        Lines = revision.Lines
            .OrderBy(line => line.SortOrder)
            .Select(line => new MaterialRateLineResponse
            {
                Id = line.Id,
                MaterialCode = line.MaterialCode,
                MaterialName = line.MaterialName,
                Unit = line.Unit,
                Quantity = line.Quantity,
                NormPerSqm = line.NormPerSqm,
                UnitRate = line.UnitRate,
                WastePercent = line.WastePercent,
                AmountPerSqm = line.AmountPerSqm,
                SortOrder = line.SortOrder,
            })
            .ToList(),
    };
}

public class MaterialRateOperationException(string message, string? messageKey = null) : InvalidOperationException(message)
{
    public string? MessageKey { get; } = messageKey;
}
