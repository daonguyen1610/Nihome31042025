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

    [Theory]
    [InlineData("1.00001", "100", "Quantity")]
    [InlineData("1", "100.001", "UnitPrice")]
    public async Task ImportAsync_BoqRejectsPrecisionThatQuoteItemsCannotPersist(
        string quantity,
        string unitPrice,
        string field)
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);

        var result = await ImportAsync(catalog.Id, revision.Id,
            $"ItemCode,ItemName,Unit,Quantity,UnitPrice\nBOQ-01,Hạng mục,m2,{quantity},{unitPrice}");

        var error = Assert.Single(result!.Errors);
        Assert.Equal("materialRates.csvError.scale", error.MessageKey);
        Assert.NotNull(error.MessageArgs);
        Assert.Equal(field, error.MessageArgs!["field"]);
        Assert.Empty(_db.MaterialRateLines);
    }

    [Fact]
    public async Task CreateBoqLineAsync_TrimsValuesAndReturnsAuthoritativeAmountAndTotal()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);

        var result = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, new UpsertBoqMaterialRateLineRequest
        {
            ItemCode = "  BT-MONG-M300  ",
            ItemName = "  Bê tông móng M300  ",
            Unit = "  m3  ",
            Quantity = 12.3456m,
            UnitPrice = 1_500_000.25m,
        }, 7);

        var line = Assert.Single(result!.Lines);
        Assert.Equal("BT-MONG-M300", line.MaterialCode);
        Assert.Equal("Bê tông móng M300", line.MaterialName);
        Assert.Equal("m3", line.Unit);
        Assert.Equal(18_518_403.0864m, line.AmountPerSqm);
        Assert.Equal(line.AmountPerSqm, result.TotalAmount);
        Assert.Equal(1, line.SortOrder);
        Assert.Equal(7, (await _db.MaterialRateRevisions.FindAsync(revision.Id))!.UpdatedByUserId);
    }

    [Fact]
    public async Task UpdateBoqLineAsync_ChangesValuesAndPreservesSortOrder()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        var first = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("CT-DAT", "Công tác đất"), 1);
        await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("BT-MONG", "Bê tông móng"), 1);
        var firstLine = first!.Lines.Single();

        var result = await _sut.UpdateBoqLineAsync(catalog.Id, revision.Id, firstLine.Id, new UpsertBoqMaterialRateLineRequest
        {
            ItemCode = "CT-DAT",
            ItemName = "Đào đất móng bằng máy",
            Unit = "m3",
            Quantity = 25.5m,
            UnitPrice = 85_000m,
        }, 2);

        var updated = result!.Lines.Single(item => item.Id == firstLine.Id);
        Assert.Equal("Đào đất móng bằng máy", updated.MaterialName);
        Assert.Equal(1, updated.SortOrder);
        Assert.Equal(2_167_500m, updated.AmountPerSqm);
        Assert.Equal([1, 2], result.Lines.Select(item => item.SortOrder));
    }

    [Fact]
    public async Task DeleteBoqLineAsync_RemovesLineAndCompactsSortOrder()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("CT-DAT", "Công tác đất"), 1);
        var second = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("BT-MONG", "Bê tông móng"), 1);
        await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("CT-THEP", "Cốt thép móng"), 1);
        var deletedLineId = second!.Lines.Single(item => item.MaterialCode == "BT-MONG").Id;

        var result = await _sut.DeleteBoqLineAsync(catalog.Id, revision.Id, deletedLineId, 3);

        Assert.Equal(["CT-DAT", "CT-THEP"], result!.Lines.Select(item => item.MaterialCode));
        Assert.Equal([1, 2], result.Lines.Select(item => item.SortOrder));
        Assert.Null(await _db.MaterialRateLines.FindAsync(deletedLineId));
    }

    [Fact]
    public async Task CreateAndUpdateBoqLineAsync_RejectDuplicateCodeCaseInsensitively()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("BT-MONG", "Bê tông móng"), 1);
        var second = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("CT-THEP", "Cốt thép móng"), 1);
        var secondLineId = second!.Lines.Single(item => item.MaterialCode == "CT-THEP").Id;

        var createException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(catalog.Id, revision.Id, BoqLine("bt-mong", "Trùng mã"), 1));
        var updateException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.UpdateBoqLineAsync(catalog.Id, revision.Id, secondLineId, BoqLine("BT-MONG", "Trùng mã"), 1));

        Assert.Equal("materialRates.line.validation.duplicateCode", createException.MessageKey);
        Assert.Equal("materialRates.line.validation.duplicateCode", updateException.MessageKey);
        Assert.Equal(2, await _db.MaterialRateLines.CountAsync());
    }

    [Theory]
    [InlineData(1.00001, 100, "materialRates.line.validation.quantityScale")]
    [InlineData(1, 100.001, "materialRates.line.validation.priceScale")]
    [InlineData(0, 100, "materialRates.line.validation.quantityPositive")]
    [InlineData(1, -0.01, "materialRates.line.validation.priceNonNegative")]
    public async Task CreateBoqLineAsync_RejectsInvalidNumbersWithoutMutation(
        decimal quantity,
        decimal unitPrice,
        string expectedMessageKey)
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        var request = BoqLine("BT-MONG", "Bê tông móng");
        request.Quantity = quantity;
        request.UnitPrice = unitPrice;

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(catalog.Id, revision.Id, request, 1));

        Assert.Equal(expectedMessageKey, exception.MessageKey);
        Assert.Empty(_db.MaterialRateLines);
    }

    [Fact]
    public async Task CreateBoqLineAsync_AcceptsFieldMaximumsWhenAmountFits()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);

        var maximumQuantity = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, new UpsertBoqMaterialRateLineRequest
        {
            ItemCode = "MAX-QTY",
            ItemName = "Khối lượng tối đa",
            Unit = "m3",
            Quantity = 999_999_999_999.9999m,
            UnitPrice = 0m,
        }, 1);
        var maximumPrice = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, new UpsertBoqMaterialRateLineRequest
        {
            ItemCode = "MAX-PRICE",
            ItemName = "Đơn giá tối đa",
            Unit = "kg",
            Quantity = 0.0001m,
            UnitPrice = 99_999_999_999_999.99m,
        }, 1);

        Assert.Equal(999_999_999_999.9999m, maximumQuantity!.Lines.Single().Quantity);
        Assert.Equal(10_000_000_000m,
            maximumPrice!.Lines.Single(item => item.MaterialCode == "MAX-PRICE").AmountPerSqm);
    }

    [Theory]
    [InlineData(1_000_000_000_000, 0, "materialRates.line.validation.quantityMaximum")]
    [InlineData(0.0001, 100_000_000_000_000, "materialRates.line.validation.priceMaximum")]
    [InlineData(2, 50_000_000_000_000, "materialRates.line.validation.amountMaximum")]
    public async Task CreateBoqLineAsync_RejectsStorageOverflow(
        decimal quantity,
        decimal unitPrice,
        string expectedMessageKey)
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        var request = BoqLine("LIMIT", "Kiểm tra giới hạn");
        request.Quantity = quantity;
        request.UnitPrice = unitPrice;

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(catalog.Id, revision.Id, request, 1));

        Assert.Equal(expectedMessageKey, exception.MessageKey);
        Assert.Empty(_db.MaterialRateLines);
    }

    [Fact]
    public async Task CreateBoqLineAsync_RoundsMidpointAwayFromZeroToFourDecimals()
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2026, 9, 1), catalogType: MaterialRateCatalogType.Boq);
        var request = BoqLine("ROUND", "Kiểm tra làm tròn");
        request.Quantity = 0.0001m;
        request.UnitPrice = 0.50m;

        var result = await _sut.CreateBoqLineAsync(catalog.Id, revision.Id, request, 1);

        Assert.Equal(0.0001m, Assert.Single(result!.Lines).AmountPerSqm);
    }

    [Fact]
    public async Task ManualBoqLineMutations_RejectInvestmentRateForEveryVerb()
    {
        var (investmentCatalog, investmentRevision) = await CreateDraftAsync(new DateOnly(2026, 9, 1));
        await ImportValidAsync(investmentCatalog.Id, investmentRevision.Id, "VL-01");
        var investmentLine = await _db.MaterialRateLines.SingleAsync();

        var createException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(investmentCatalog.Id, investmentRevision.Id, BoqLine("BT-MONG", "Bê tông móng"), 1));
        var updateException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.UpdateBoqLineAsync(investmentCatalog.Id, investmentRevision.Id, investmentLine.Id, BoqLine("BT-MONG", "Bê tông móng"), 1));
        var deleteException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.DeleteBoqLineAsync(investmentCatalog.Id, investmentRevision.Id, investmentLine.Id, 1));

        Assert.All([createException, updateException, deleteException], exception =>
            Assert.Equal("materialRates.line.boqOnly", exception.MessageKey));
        Assert.Single(_db.MaterialRateLines);
    }

    [Theory]
    [InlineData(MaterialRateRevisionStatus.Approved)]
    [InlineData(MaterialRateRevisionStatus.Rejected)]
    [InlineData(MaterialRateRevisionStatus.Retired)]
    public async Task ManualBoqLineMutations_RejectEveryImmutableStatusForEveryVerb(
        MaterialRateRevisionStatus status)
    {
        var (boqCatalog, boqRevision) = await CreateDraftAsync(
            new DateOnly(2027, 1, 1), catalogType: MaterialRateCatalogType.Boq);
        var draft = await _sut.CreateBoqLineAsync(boqCatalog.Id, boqRevision.Id, BoqLine("BT-MONG", "Bê tông móng"), 1);
        var lineId = draft!.Lines.Single().Id;
        boqRevision.Status = status;
        await _db.SaveChangesAsync();

        var createException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(boqCatalog.Id, boqRevision.Id, BoqLine("CT-THEP", "Cốt thép móng"), 1));
        var updateException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.UpdateBoqLineAsync(boqCatalog.Id, boqRevision.Id, lineId, BoqLine("BT-MONG", "Bê tông móng sửa"), 1));
        var deleteException = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.DeleteBoqLineAsync(boqCatalog.Id, boqRevision.Id, lineId, 1));

        Assert.All([createException, updateException, deleteException], exception =>
            Assert.Equal("materialRates.line.draftOnly", exception.MessageKey));
        Assert.Single(await _db.MaterialRateLines.Where(item => item.RevisionId == boqRevision.Id).ToListAsync());
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("unit")]
    [InlineData("quantity")]
    [InlineData("unitPrice")]
    public async Task CreateBoqLineAsync_MissingFieldReturnsLocalizedBusinessKey(string field)
    {
        var (catalog, revision) = await CreateDraftAsync(
            new DateOnly(2027, 1, 1), catalogType: MaterialRateCatalogType.Boq);
        var request = BoqLine("BT-MONG", "Bê tông móng");
        if (field == "code") request.ItemCode = null;
        if (field == "name") request.ItemName = null;
        if (field == "unit") request.Unit = null;
        if (field == "quantity") request.Quantity = null;
        if (field == "unitPrice") request.UnitPrice = null;

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.CreateBoqLineAsync(catalog.Id, revision.Id, request, 1));

        Assert.StartsWith("materialRates.line.validation.", exception.MessageKey);
        Assert.Empty(_db.MaterialRateLines);
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

    [Fact]
    public async Task DeleteCatalogAsync_RemovesCatalogRevisionsAndLines()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1));
        await ImportValidAsync(catalog.Id, revision.Id, "DELETE-ME");

        var deleted = await _sut.DeleteCatalogAsync(catalog.Id);

        Assert.Equal(catalog.Id, deleted!.Id);
        Assert.Empty(_db.MaterialRateCatalogs);
        Assert.Empty(_db.MaterialRateRevisions);
        Assert.Empty(_db.MaterialRateLines);
    }

    [Fact]
    public async Task DeleteCatalogAsync_WhenQuoteReferencesRevision_IsRejectedWithoutDeletingData()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1));
        _db.Quotes.Add(new Quote
        {
            Code = "QT-RATE-REFERENCE",
            OpportunityId = 1,
            OwnerUserId = 1,
            MaterialRateRevisionId = revision.Id,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.DeleteCatalogAsync(catalog.Id));

        Assert.Equal("materialRates.catalog.deleteBlocked", exception.MessageKey);
        Assert.Single(_db.MaterialRateCatalogs);
        Assert.Single(_db.MaterialRateRevisions);
    }

    [Fact]
    public async Task DeleteCatalogAsync_WhenQuoteSnapshotReferencesRevision_IsRejected()
    {
        var (catalog, revision) = await CreateDraftAsync(new DateOnly(2026, 1, 1));
        _db.QuoteVersionSnapshots.Add(new QuoteVersionSnapshot
        {
            QuoteId = 1,
            VersionNumber = 1,
            MaterialRateRevisionId = revision.Id,
        });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<MaterialRateOperationException>(() =>
            _sut.DeleteCatalogAsync(catalog.Id));

        Assert.Equal("materialRates.catalog.deleteBlocked", exception.MessageKey);
        Assert.Single(_db.MaterialRateCatalogs);
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

    private static UpsertBoqMaterialRateLineRequest BoqLine(string code, string name) => new()
    {
        ItemCode = code,
        ItemName = name,
        Unit = "m3",
        Quantity = 10m,
        UnitPrice = 100_000m,
    };
}
