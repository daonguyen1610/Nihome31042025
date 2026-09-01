using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface ITenderEstimateService
{
    Task<List<TenderEstimateRevisionResponse>?> ListAsync(int tenderId, CancellationToken ct = default);
    Task<TenderEstimateRevisionResponse?> GetAsync(int tenderId, int revisionId, CancellationToken ct = default);
    Task<TenderEstimateImportResponse?> ImportAsync(int tenderId, Stream stream, string sourceFileName, int userId, CancellationToken ct = default);
    Task<TenderEstimateRevisionResponse?> SubmitAsync(int tenderId, int revisionId, string? note, int userId, CancellationToken ct = default);
    Task<TenderEstimateRevisionResponse?> ApproveAsync(int tenderId, int revisionId, string? note, int userId, CancellationToken ct = default);
    Task<TenderEstimateRevisionResponse?> RejectAsync(int tenderId, int revisionId, string? note, int userId, CancellationToken ct = default);
}

public sealed class TenderEstimateService(AppDbContext db, IUtf8CsvParser csvParser) : ITenderEstimateService
{
    public const int MaxCsvBytes = 2 * 1024 * 1024;
    public const int MaxCsvRows = 2000;
    public static readonly IReadOnlyList<string> CsvHeaders =
    [
        "ItemCode",
        "Description",
        "Unit",
        "Quantity",
        "UnitCost",
        "BidUnitPrice",
        "VatPercent",
        "Note",
    ];

    public async Task<List<TenderEstimateRevisionResponse>?> ListAsync(int tenderId, CancellationToken ct = default)
    {
        if (!await db.Tenders.AsNoTracking().AnyAsync(tender => tender.Id == tenderId, ct)) return null;
        var revisions = await RevisionQuery()
            .Where(revision => revision.TenderId == tenderId)
            .OrderByDescending(revision => revision.VersionNumber)
            .ToListAsync(ct);
        return revisions.Select(Map).ToList();
    }

    public async Task<TenderEstimateRevisionResponse?> GetAsync(
        int tenderId,
        int revisionId,
        CancellationToken ct = default)
    {
        var revision = await RevisionQuery().FirstOrDefaultAsync(
            item => item.TenderId == tenderId && item.Id == revisionId,
            ct);
        return revision is null ? null : Map(revision);
    }

    public async Task<TenderEstimateImportResponse?> ImportAsync(
        int tenderId,
        Stream stream,
        string sourceFileName,
        int userId,
        CancellationToken ct = default)
    {
        var tender = await db.Tenders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null) return null;
        EnsurePreparing(tender, "nhập dự toán");

        var bytes = await ReadLimitedAsync(stream, MaxCsvBytes, ct);
        if (bytes is null)
        {
            return new TenderEstimateImportResponse
            {
                Errors = [new CsvImportError { Message = "Tệp dự toán CSV vượt quá dung lượng tối đa 2 MB." }],
            };
        }
        await using var buffer = new MemoryStream(bytes, writable: false);
        var parsed = await csvParser.ParseAsync(buffer, CsvHeaders, MaxCsvBytes, MaxCsvRows, ct);
        if (!parsed.IsValid) return new TenderEstimateImportResponse { Errors = parsed.Errors };

        var errors = new List<CsvImportError>();
        var lines = new List<TenderEstimateLine>();
        var itemCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal? commonVat = null;

        for (var index = 0; index < parsed.Rows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = parsed.Rows[index];
            var itemCode = row["ItemCode"].Trim();
            var description = row["Description"].Trim();
            var unit = row["Unit"].Trim();
            var note = Clean(row["Note"]);

            ValidateText(itemCode, 80, "ItemCode", rowNumber, 1, errors);
            ValidateText(description, 500, "Description", rowNumber, 2, errors);
            ValidateText(unit, 50, "Unit", rowNumber, 3, errors);
            if (note?.Length > 1000)
            {
                errors.Add(Error(rowNumber, 8, "Note không được vượt quá 1000 ký tự."));
            }
            if (itemCode.Length > 0 && !itemCodes.Add(itemCode))
            {
                errors.Add(Error(rowNumber, 1, $"ItemCode '{itemCode}' bị trùng trong tệp CSV."));
            }

            var quantity = ParseDecimal(row["Quantity"], "Quantity", rowNumber, 4, 6, errors);
            var unitCost = ParseDecimal(row["UnitCost"], "UnitCost", rowNumber, 5, 4, errors);
            var bidUnitPrice = ParseDecimal(row["BidUnitPrice"], "BidUnitPrice", rowNumber, 6, 4, errors);
            var vatPercent = ParseDecimal(row["VatPercent"], "VatPercent", rowNumber, 7, 4, errors);

            if (quantity is <= 0) errors.Add(Error(rowNumber, 4, "Quantity phải lớn hơn 0."));
            if (quantity is > 999999999999.999999m) errors.Add(Error(rowNumber, 4, "Quantity vượt quá giới hạn lưu trữ."));
            if (unitCost is < 0) errors.Add(Error(rowNumber, 5, "UnitCost không được nhỏ hơn 0."));
            if (unitCost is > 99999999999999.9999m) errors.Add(Error(rowNumber, 5, "UnitCost vượt quá giới hạn lưu trữ."));
            if (bidUnitPrice is < 0) errors.Add(Error(rowNumber, 6, "BidUnitPrice không được nhỏ hơn 0."));
            if (bidUnitPrice is > 99999999999999.9999m) errors.Add(Error(rowNumber, 6, "BidUnitPrice vượt quá giới hạn lưu trữ."));
            if (vatPercent is < 0 or > 100) errors.Add(Error(rowNumber, 7, "VatPercent phải nằm trong khoảng từ 0 đến 100."));
            if (vatPercent.HasValue)
            {
                commonVat ??= vatPercent.Value;
                if (commonVat.Value != vatPercent.Value)
                {
                    errors.Add(Error(rowNumber, 7, "VatPercent phải giống nhau trên tất cả các dòng."));
                }
            }

            if (itemCode.Length == 0 || description.Length == 0 || unit.Length == 0 ||
                quantity is null || unitCost is null || bidUnitPrice is null || vatPercent is null)
            {
                continue;
            }

            try
            {
                var costAmount = decimal.Round(quantity.Value * unitCost.Value, 4, MidpointRounding.AwayFromZero);
                var bidAmount = decimal.Round(quantity.Value * bidUnitPrice.Value, 4, MidpointRounding.AwayFromZero);
                if (costAmount > 99999999999999.9999m || bidAmount > 99999999999999.9999m)
                {
                    errors.Add(Error(rowNumber, null, "Thành tiền của dòng vượt quá giới hạn lưu trữ."));
                    continue;
                }
                lines.Add(new TenderEstimateLine
                {
                    ItemCode = itemCode,
                    Description = description,
                    Unit = unit,
                    Quantity = quantity.Value,
                    UnitCost = unitCost.Value,
                    BidUnitPrice = bidUnitPrice.Value,
                    CostAmount = costAmount,
                    BidAmount = bidAmount,
                    Note = note,
                    SortOrder = index + 1,
                });
            }
            catch (OverflowException)
            {
                errors.Add(Error(rowNumber, null, "Giá trị dòng vượt quá giới hạn tính toán."));
            }
        }

        if (parsed.Rows.Count == 0)
        {
            errors.Add(new CsvImportError { Row = 2, Message = "Tệp CSV phải có ít nhất một dòng dữ liệu." });
        }
        if (errors.Count > 0) return new TenderEstimateImportResponse { Errors = errors };

        decimal costSubtotal;
        decimal bidSubtotal;
        decimal vatAmount;
        decimal grandBidTotal;
        try
        {
            costSubtotal = decimal.Round(lines.Sum(line => line.CostAmount), 4, MidpointRounding.AwayFromZero);
            bidSubtotal = decimal.Round(lines.Sum(line => line.BidAmount), 4, MidpointRounding.AwayFromZero);
            vatAmount = decimal.Round(bidSubtotal * commonVat!.Value / 100m, 4, MidpointRounding.AwayFromZero);
            grandBidTotal = decimal.Round(bidSubtotal + vatAmount, 4, MidpointRounding.AwayFromZero);
            if (costSubtotal > 99999999999999.9999m || bidSubtotal > 99999999999999.9999m ||
                vatAmount > 99999999999999.9999m || grandBidTotal > 99999999999999.9999m)
            {
                return InvalidTotal();
            }
        }
        catch (OverflowException)
        {
            return InvalidTotal();
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var versionNumber = 1 + (await db.TenderEstimateRevisions
            .Where(item => item.TenderId == tenderId)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(ct) ?? 0);
        var now = DateTime.UtcNow;
        var revision = new TenderEstimateRevision
        {
            TenderId = tenderId,
            VersionNumber = versionNumber,
            Currency = "VND",
            VatPercent = commonVat.Value,
            CostSubtotal = costSubtotal,
            BidSubtotal = bidSubtotal,
            VatAmount = vatAmount,
            GrandBidTotal = grandBidTotal,
            SourceFileName = NormalizeFileName(sourceFileName),
            SourceSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            ImportedByUserId = userId,
            ImportedAt = now,
            Lines = lines,
        };
        db.TenderEstimateRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new TenderEstimateImportResponse { Revision = Map(revision) };
    }

    public async Task<TenderEstimateRevisionResponse?> SubmitAsync(
        int tenderId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default)
    {
        var revision = await MutableRevisionAsync(tenderId, revisionId, ct);
        if (revision is null) return null;
        EnsurePreparing(revision.Tender, "gửi duyệt dự toán");
        if (revision.Status != TenderEstimateRevisionStatus.Draft)
        {
            throw new TenderEstimateOperationException("Chỉ phiên bản Nháp mới có thể gửi duyệt.");
        }
        if (revision.Lines.Count == 0)
        {
            throw new TenderEstimateOperationException("Dự toán phải có ít nhất một dòng trước khi gửi duyệt.");
        }
        revision.Status = TenderEstimateRevisionStatus.Submitted;
        revision.SubmittedByUserId = userId;
        revision.SubmittedAt = DateTime.UtcNow;
        revision.Note = Clean(note);
        await db.SaveChangesAsync(ct);
        return Map(revision);
    }

    public Task<TenderEstimateRevisionResponse?> ApproveAsync(
        int tenderId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default) => DecideAsync(tenderId, revisionId, true, note, userId, ct);

    public Task<TenderEstimateRevisionResponse?> RejectAsync(
        int tenderId,
        int revisionId,
        string? note,
        int userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new TenderEstimateOperationException("Vui lòng nhập lý do từ chối dự toán, ví dụ: Cần cập nhật đơn giá vật tư.");
        }
        return DecideAsync(tenderId, revisionId, false, note, userId, ct);
    }

    private async Task<TenderEstimateRevisionResponse?> DecideAsync(
        int tenderId,
        int revisionId,
        bool approve,
        string? note,
        int userId,
        CancellationToken ct)
    {
        var revision = await MutableRevisionAsync(tenderId, revisionId, ct);
        if (revision is null) return null;
        EnsurePreparing(revision.Tender, approve ? "phê duyệt dự toán" : "từ chối dự toán");
        if (revision.Status != TenderEstimateRevisionStatus.Submitted)
        {
            throw new TenderEstimateOperationException("Chỉ phiên bản Đã gửi duyệt mới có thể được phê duyệt hoặc từ chối.");
        }

        var now = DateTime.UtcNow;
        revision.Status = approve ? TenderEstimateRevisionStatus.Approved : TenderEstimateRevisionStatus.Rejected;
        revision.Note = Clean(note);
        if (approve)
        {
            revision.ApprovedByUserId = userId;
            revision.ApprovedAt = now;
        }
        else
        {
            revision.RejectedByUserId = userId;
            revision.RejectedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return Map(revision);
    }

    private Task<TenderEstimateRevision?> MutableRevisionAsync(int tenderId, int revisionId, CancellationToken ct) =>
        db.TenderEstimateRevisions
            .Include(item => item.Tender)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.TenderId == tenderId && item.Id == revisionId, ct);

    private IQueryable<TenderEstimateRevision> RevisionQuery() => db.TenderEstimateRevisions
        .AsNoTracking()
        .Include(item => item.Lines);

    private static void EnsurePreparing(Tender tender, string action)
    {
        if (tender.Status != TenderStatus.Preparing)
        {
            throw new TenderEstimateOperationException($"Chỉ gói thầu đang Chuẩn bị mới được phép {action}.");
        }
    }

    private static TenderEstimateImportResponse InvalidTotal() => new()
    {
        Errors = [new CsvImportError { Message = "Tổng giá trị dự toán vượt quá giới hạn lưu trữ." }],
    };

    private static decimal? ParseDecimal(
        string value,
        string field,
        int row,
        int column,
        int maxScale,
        List<CsvImportError> errors)
    {
        if (!decimal.TryParse(value.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsed))
        {
            errors.Add(Error(row, column, $"{field} phải là số thập phân theo định dạng invariant, ví dụ: 12.5."));
            return null;
        }
        if (decimal.Round(parsed, maxScale) != parsed)
        {
            errors.Add(Error(row, column,
                $"{field} chỉ được có tối đa {maxScale} chữ số thập phân, ví dụ: 12.5."));
            return null;
        }
        return parsed;
    }

    private static async Task<byte[]?> ReadLimitedAsync(
        Stream stream, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var chunk = new byte[81920];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, maxBytes + 1 - total)), ct);
            if (read == 0) return buffer.ToArray();
            total += read;
            if (total > maxBytes) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    private static void ValidateText(
        string value,
        int maxLength,
        string field,
        int row,
        int column,
        List<CsvImportError> errors)
    {
        if (value.Length == 0)
        {
            errors.Add(Error(row, column, $"{field} không được để trống."));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(Error(row, column, $"{field} không được vượt quá {maxLength} ký tự."));
        }
    }

    private static string NormalizeFileName(string value)
    {
        var fileName = Path.GetFileName(value).Trim();
        if (string.IsNullOrWhiteSpace(fileName)) return "estimate.csv";
        return fileName.Length <= 300 ? fileName : fileName[..300];
    }

    private static CsvImportError Error(int row, int? column, string message) =>
        new() { Row = row, Column = column, Message = message };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TenderEstimateRevisionResponse Map(TenderEstimateRevision revision) => new()
    {
        Id = revision.Id,
        TenderId = revision.TenderId,
        VersionNumber = revision.VersionNumber,
        Status = revision.Status.ToString(),
        Currency = revision.Currency,
        VatPercent = revision.VatPercent,
        CostSubtotal = revision.CostSubtotal,
        BidSubtotal = revision.BidSubtotal,
        VatAmount = revision.VatAmount,
        GrandBidTotal = revision.GrandBidTotal,
        SourceFileName = revision.SourceFileName,
        SourceSha256 = revision.SourceSha256,
        ImportedByUserId = revision.ImportedByUserId,
        ImportedAt = revision.ImportedAt,
        SubmittedByUserId = revision.SubmittedByUserId,
        SubmittedAt = revision.SubmittedAt,
        ApprovedByUserId = revision.ApprovedByUserId,
        ApprovedAt = revision.ApprovedAt,
        RejectedByUserId = revision.RejectedByUserId,
        RejectedAt = revision.RejectedAt,
        Note = revision.Note,
        Lines = revision.Lines.OrderBy(line => line.SortOrder).Select(line => new TenderEstimateLineResponse
        {
            Id = line.Id,
            ItemCode = line.ItemCode,
            Description = line.Description,
            Unit = line.Unit,
            Quantity = line.Quantity,
            UnitCost = line.UnitCost,
            BidUnitPrice = line.BidUnitPrice,
            CostAmount = line.CostAmount,
            BidAmount = line.BidAmount,
            Note = line.Note,
            SortOrder = line.SortOrder,
        }).ToList(),
    };
}

public sealed class TenderEstimateOperationException(string message) : InvalidOperationException(message);
