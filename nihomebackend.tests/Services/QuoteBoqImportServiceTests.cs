using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class QuoteBoqImportServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task PreviewAsync_DiscoversSheetOrderVisibilityAndVietnameseColumns()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Tổng hợp").Cell("A1").Value = "TỔNG GIÁ TRỊ";
        var detail = workbook.Worksheets.Add("Nhà xưởng");
        detail.Cell("A3").Value = "Mã hạng mục";
        detail.Cell("B3").Value = "Mô tả";
        detail.Cell("C3").Value = "Đơn vị";
        detail.Cell("D3").Value = "Khối lượng";
        detail.Cell("E3").Value = "Đơn giá";
        detail.Cell("A4").Value = "NX-01";
        detail.Cell("B4").Value = "Bê tông móng";
        detail.Cell("C4").Value = "m3";
        detail.Cell("D4").Value = 10;
        detail.Cell("E4").Value = 1500000;
        var hidden = workbook.Worksheets.Add("Danh mục vật tư");
        hidden.Visibility = XLWorksheetVisibility.Hidden;

        var result = await CreateService().PreviewAsync(10, 20, ToFile(workbook, "multi-sheet.xlsx"));

        Assert.Equal(3, result.Sheets.Count);
        Assert.Equal("Summary", result.Sheets[0].SuggestedClassification);
        Assert.Equal("Detail", result.Sheets[1].SuggestedClassification);
        Assert.Equal(3, result.Sheets[1].HeaderRow);
        Assert.Equal(1, result.Sheets[1].SuggestedItemCount);
        Assert.True(result.Sheets[2].IsHidden);
    }

    [Fact]
    public async Task PreviewAsync_RejectsMacroWorkbookBeforeParsing()
    {
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "unsafe.xlsm");
        var exception = await Assert.ThrowsAsync<QuoteOperationException>(() => CreateService().PreviewAsync(10, 20, file));
        Assert.Contains(".xlsx", exception.Message);
    }

    private QuoteBoqImportService CreateService() => new(
        _db, Mock.Of<IQuoteService>(), new EphemeralDataProtectionProvider());

    private static IFormFile ToFile(XLWorkbook workbook, string name)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", name);
    }
}
