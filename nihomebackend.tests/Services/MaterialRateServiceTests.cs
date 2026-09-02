using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class MaterialRateServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MaterialRateService _sut;

    public MaterialRateServiceTests()
    {
        _sut = new MaterialRateService(_db, new Utf8CsvParser(), new MaterialRateSpreadsheetService());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ListCatalogsAsync_FiltersByCatalogTypeAndReturnsPersistedType()
    {
        await _sut.CreateCatalogAsync(new UpsertMaterialRateCatalogRequest
        {
            CatalogType = MaterialRateCatalogType.InvestmentRate,
            Code = "INVESTMENT-TEST",
            Name = "Danh mục suất đầu tư",
            Currency = "VND",
            IsActive = true,
        }, 1);
        var boq = await _sut.CreateCatalogAsync(new UpsertMaterialRateCatalogRequest
        {
            CatalogType = MaterialRateCatalogType.Boq,
            Code = "BOQ-TEST",
            Name = "Danh mục BOQ",
            Currency = "VND",
            IsActive = true,
        }, 1);

        var result = await _sut.ListCatalogsAsync(null, includeInactive: false, MaterialRateCatalogType.Boq);

        var catalog = Assert.Single(result);
        Assert.Equal(boq.Id, catalog.Id);
        Assert.Equal(MaterialRateCatalogType.Boq, catalog.CatalogType);
        Assert.Equal(MaterialRateCatalogType.Boq,
            (await _db.MaterialRateCatalogs.SingleAsync(item => item.Id == boq.Id)).CatalogType);
    }

    [Fact]
    public async Task ImportAsync_ComputesAmountPerSqmUsingInvariantDecimals()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 9, 1));

        var result = await ImportAsync(catalog.Id, revision.Id,
            "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nVL-1,Keo,kg,2.5,15000.25,5");

        Assert.Empty(result!.Errors);
        Assert.Equal(1, result.ImportedCount);
        var line = Assert.Single(_db.MaterialRateLines);
        Assert.Equal(39375.6563m, line.AmountPerSqm);
    }

    [Fact]
    public async Task ImportAsync_BoqCsvPersistsQuantityUnitPriceAndTotalAmount()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);

        var result = await ImportAsync(catalog.Id, revision.Id,
            "ItemCode,ItemName,Unit,Quantity,UnitPrice\nCV-BT-01,Bê tông móng,m3,12.5,1500000.25");

        Assert.Empty(result!.Errors);
        Assert.Equal(1, result.ImportedCount);
        var line = Assert.Single(_db.MaterialRateLines);
        Assert.Equal(12.5m, line.Quantity);
        Assert.Equal(1_500_000.25m, line.UnitRate);
        Assert.Equal(18_750_003.125m, line.AmountPerSqm);
        var detail = await _sut.GetRevisionAsync(catalog.Id, revision.Id);
        Assert.Equal(18_750_003.125m, detail!.TotalAmount);
    }

    [Fact]
    public async Task ImportAsync_InvalidRowIsAtomicAndPreservesExistingLines()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 9, 1));
        await ImportAsync(catalog.Id, revision.Id,
            "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nOLD,Vật liệu cũ,kg,1,100,0");

        var result = await ImportAsync(catalog.Id, revision.Id,
            "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nNEW,Vật liệu mới,kg,2,100,0\nBAD,Dòng lỗi,kg,1,12,5,0");

        Assert.NotEmpty(result!.Errors);
        var saved = Assert.Single(_db.MaterialRateLines);
        Assert.Equal("OLD", saved.MaterialCode);
    }

    [Theory]
    [InlineData("1.0000001", "100", "0", "NormPerSqm")]
    [InlineData("1", "100.00001", "0", "UnitRate")]
    [InlineData("1", "100", "0.00001", "WastePercent")]
    public async Task ImportAsync_ExcessDecimalPrecisionIsRejectedWithoutReplacingLines(
        string norm, string rate, string waste, string field)
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 9, 1));
        await ImportValidAsync(catalog.Id, revision.Id, "OLD");

        var result = await ImportAsync(catalog.Id, revision.Id,
            $"MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nNEW,Vật liệu,kg,{norm},{rate},{waste}");

        var error = Assert.Single(result!.Errors);
        Assert.Contains(field, error.Message);
        Assert.Equal("materialRates.csvError.scale", error.MessageKey);
        Assert.Equal(field, error.MessageArgs!["field"]);
        Assert.Equal("OLD", Assert.Single(_db.MaterialRateLines).MaterialCode);
    }

    [Fact]
    public async Task ImportAsync_InvalidExcelRowReportsWorksheetRowAndPreservesExistingLines()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 9, 1));
        await ImportValidAsync(catalog.Id, revision.Id, "OLD");
        var spreadsheet = new MaterialRateSpreadsheetService();
        var bytes = spreadsheet.CreateTemplate(new Dictionary<string, string>());
        using var source = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(source);
        var entry = workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible);
        entry.Cell("A12").Value = "NEW";
        entry.Cell("B12").Value = "Vật liệu lỗi";
        entry.Cell("C12").Value = "kg";
        entry.Cell("D12").Value = "abc";
        entry.Cell("E12").Value = 100;
        entry.Cell("F12").Value = 0;
        using var invalidForm = new MemoryStream();
        workbook.SaveAs(invalidForm);
        invalidForm.Position = 0;

        var result = await _sut.ImportAsync(catalog.Id, revision.Id, invalidForm, 1, "customer-form.xlsx");

        var error = Assert.Single(result!.Errors);
        Assert.Equal(12, error.Row);
        Assert.Equal("materialRates.csvError.decimal", error.MessageKey);
        Assert.Equal("OLD", Assert.Single(_db.MaterialRateLines).MaterialCode);
    }

    [Fact]
    public async Task ApproveAsync_RejectsOverlappingApprovedRange()
    {
        var (catalog, first) = await CreateDraftAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await ImportValidAsync(catalog.Id, first.Id, "A");
        await _sut.ApproveAsync(catalog.Id, first.Id, null, 1);
        var second = await _sut.CreateRevisionAsync(catalog.Id, new CreateMaterialRateRevisionRequest
        {
            EffectiveFrom = new DateOnly(2026, 6, 30),
            EffectiveTo = new DateOnly(2026, 12, 31),
        }, 1);
        await ImportValidAsync(catalog.Id, second!.Id, "B");

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.ApproveAsync(catalog.Id, second.Id, null, 1));

        Assert.Contains("bị trùng", exception.Message);
        Assert.Equal(MaterialRateRevisionStatus.Draft, (await _db.MaterialRateRevisions.FindAsync(second.Id))!.Status);
    }

    [Fact]
    public async Task ApprovedRevision_IsImmutableToCsvImport()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1));
        await ImportValidAsync(catalog.Id, revision.Id, "A");
        await _sut.ApproveAsync(catalog.Id, revision.Id, null, 1);

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            ImportAsync(catalog.Id, revision.Id,
                "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nB,B,kg,1,1,0"));

        Assert.Contains("Nháp", exception.Message);
        Assert.Equal("A", Assert.Single(_db.MaterialRateLines).MaterialCode);
    }

    [Fact]
    public async Task GetEffectiveAsync_ReturnsApprovedRevisionAtInclusiveBoundary()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await ImportValidAsync(catalog.Id, revision.Id, "A");
        await _sut.ApproveAsync(catalog.Id, revision.Id, null, 1);

        var effective = await _sut.GetEffectiveAsync(catalog.Id, new DateOnly(2026, 6, 30));
        var expired = await _sut.GetEffectiveAsync(catalog.Id, new DateOnly(2026, 7, 1));

        Assert.Equal(revision.Id, effective!.Id);
        Assert.Null(expired);
    }

    [Fact]
    public async Task RejectAsync_RequiresVietnameseReasonAndDoesNotChangeState()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1));

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.RejectAsync(catalog.Id, revision.Id, " ", 1));

        Assert.Contains("lý do từ chối", exception.Message);
        Assert.Equal(MaterialRateRevisionStatus.Draft, (await _db.MaterialRateRevisions.FindAsync(revision.Id))!.Status);
    }

    private async Task<(MaterialRateCatalog Catalog, MaterialRateRevision Revision)> CreateDraftAsync(
        DateOnly from,
        DateOnly? to = null,
        MaterialRateCatalogType catalogType = MaterialRateCatalogType.InvestmentRate)
    {
        var catalog = new MaterialRateCatalog
        {
            CatalogType = catalogType,
            Code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name = "Danh mục thử nghiệm",
            Currency = "VND",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        _db.MaterialRateCatalogs.Add(catalog);
        await _db.SaveChangesAsync();
        var response = await _sut.CreateRevisionAsync(catalog.Id, new CreateMaterialRateRevisionRequest
        {
            EffectiveFrom = from,
            EffectiveTo = to,
        }, 1);
        return (catalog, (await _db.MaterialRateRevisions.FindAsync(response!.Id))!);
    }

    private Task<NihomeBackend.Models.DTOs.Responses.MaterialRateImportResponse?> ImportValidAsync(
        int catalogId,
        int revisionId,
        string code) => ImportAsync(catalogId, revisionId,
            $"MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\n{code},Vật liệu,kg,1,100,0");

    private async Task<NihomeBackend.Models.DTOs.Responses.MaterialRateImportResponse?> ImportAsync(
        int catalogId,
        int revisionId,
        string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _sut.ImportAsync(catalogId, revisionId, stream, 1);
    }
}
