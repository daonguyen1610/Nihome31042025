using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class TenderEstimateServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly TenderEstimateService _sut;
    private readonly Tender _tender;

    public TenderEstimateServiceTests()
    {
        _sut = new TenderEstimateService(_db, new Utf8CsvParser());
        _tender = new Tender
        {
            Code = "TD-TEST-001",
            Name = "Gói thầu kiểm thử",
            CustomerId = 1,
            SubmissionDeadline = DateTime.UtcNow.AddDays(10),
            Status = TenderStatus.Preparing,
        };
        _db.Tenders.Add(_tender);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ImportAsync_CreatesDraftWithCalculatedTotalsAndProvenance()
    {
        const string csv = "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\nA-1,Hạng mục A,m2,2,100,150,10,Ghi chú";

        var result = await ImportAsync(csv, "estimate.csv");

        Assert.Empty(result!.Errors);
        var revision = Assert.IsType<NihomeBackend.Models.DTOs.Responses.TenderEstimateRevisionResponse>(result.Revision);
        Assert.Equal(1, revision.VersionNumber);
        Assert.Equal("Draft", revision.Status);
        Assert.Equal("VND", revision.Currency);
        Assert.Equal(200m, revision.CostSubtotal);
        Assert.Equal(300m, revision.BidSubtotal);
        Assert.Equal(30m, revision.VatAmount);
        Assert.Equal(330m, revision.GrandBidTotal);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(csv))).ToLowerInvariant(), revision.SourceSha256);
        var line = Assert.Single(revision.Lines);
        Assert.Equal(200m, line.CostAmount);
        Assert.Equal(300m, line.BidAmount);
    }

    [Fact]
    public async Task ImportAsync_InvalidRowsAreAtomic()
    {
        const string csv = "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\nA,Hạng mục,m2,1,10,20,10,\nA,Hạng mục trùng,m2,0,-1,20,8,";

        var result = await ImportAsync(csv);

        Assert.NotEmpty(result!.Errors);
        Assert.Contains(result.Errors, error => error.Message.Contains("bị trùng"));
        Assert.Contains(result.Errors, error => error.Message.Contains("Quantity"));
        Assert.Contains(result.Errors, error => error.Message.Contains("VatPercent phải giống nhau"));
        Assert.False(await _db.TenderEstimateRevisions.AnyAsync());
        Assert.False(await _db.TenderEstimateLines.AnyAsync());
    }

    [Theory]
    [InlineData("0.0000001", "10", "20", "10", "Quantity")]
    [InlineData("1", "10.00001", "20", "10", "UnitCost")]
    [InlineData("1", "10", "20.00001", "10", "BidUnitPrice")]
    [InlineData("1", "10", "20", "10.00001", "VatPercent")]
    public async Task ImportAsync_ExcessDecimalPrecisionIsRejectedAtomically(
        string quantity, string unitCost, string bidUnitPrice, string vatPercent, string field)
    {
        var csv = "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\n" +
            $"A,Hạng mục,m2,{quantity},{unitCost},{bidUnitPrice},{vatPercent},";

        var result = await ImportAsync(csv);

        Assert.Contains(result!.Errors, error => error.Message.Contains(field));
        Assert.False(await _db.TenderEstimateRevisions.AnyAsync());
        Assert.False(await _db.TenderEstimateLines.AnyAsync());
    }

    [Fact]
    public async Task ImportAsync_StreamOverMaximumIsRejectedWithoutPersistence()
    {
        using var stream = new MemoryStream(new byte[TenderEstimateService.MaxCsvBytes + 1]);

        var result = await _sut.ImportAsync(_tender.Id, stream, "oversized.csv", 20);

        Assert.Contains(result!.Errors, error => error.Message.Contains("2 MB"));
        Assert.False(await _db.TenderEstimateRevisions.AnyAsync());
    }

    [Fact]
    public async Task ImportAsync_CreatesNextDraftVersionWithoutReplacingHistory()
    {
        await ImportAsync(ValidCsv("A"));
        await ImportAsync(ValidCsv("B"));

        var revisions = await _db.TenderEstimateRevisions.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal([1, 2], revisions.Select(item => item.VersionNumber));
        Assert.All(revisions, item => Assert.Equal(TenderEstimateRevisionStatus.Draft, item.Status));
    }

    [Fact]
    public async Task SubmitAndApproveAsync_RecordLifecycleMetadata()
    {
        var imported = await ImportAsync(ValidCsv("A"));
        var revisionId = imported!.Revision!.Id;

        var submitted = await _sut.SubmitAsync(_tender.Id, revisionId, "Gửi quản lý", 21);
        var approved = await _sut.ApproveAsync(_tender.Id, revisionId, "Đồng ý", 22);

        Assert.Equal("Submitted", submitted!.Status);
        Assert.Equal(21, submitted.SubmittedByUserId);
        Assert.NotNull(submitted.SubmittedAt);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal(22, approved.ApprovedByUserId);
        Assert.NotNull(approved.ApprovedAt);
        Assert.Equal("Đồng ý", approved.Note);
    }

    [Fact]
    public async Task RejectAsync_RequiresNoteAndPreservesSubmittedState()
    {
        var imported = await ImportAsync(ValidCsv("A"));
        var revisionId = imported!.Revision!.Id;
        await _sut.SubmitAsync(_tender.Id, revisionId, null, 21);

        await Assert.ThrowsAsync<TenderEstimateOperationException>(() =>
            _sut.RejectAsync(_tender.Id, revisionId, " ", 22));

        Assert.Equal(TenderEstimateRevisionStatus.Submitted,
            (await _db.TenderEstimateRevisions.FindAsync(revisionId))!.Status);
    }

    [Fact]
    public async Task ImportAsync_NonPreparingTenderIsRejected()
    {
        _tender.Status = TenderStatus.Submitted;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<TenderEstimateOperationException>(() => ImportAsync(ValidCsv("A")));

        Assert.False(await _db.TenderEstimateRevisions.AnyAsync());
    }

    private async Task<NihomeBackend.Models.DTOs.Responses.TenderEstimateImportResponse?> ImportAsync(
        string csv,
        string fileName = "estimate.csv")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _sut.ImportAsync(_tender.Id, stream, fileName, 20);
    }

    private static string ValidCsv(string itemCode) =>
        $"ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\n{itemCode},Hạng mục,m2,1,100,120,10,";
}
