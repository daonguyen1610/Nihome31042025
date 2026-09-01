using System.Text;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class Utf8CsvParserTests
{
    private static readonly string[] Headers = ["Code", "Name", "Rate"];
    private readonly Utf8CsvParser _sut = new();

    [Fact]
    public async Task ParseAsync_HandlesBomQuotedFieldsEscapedQuotesAndCrLf()
    {
        var csv = "\uFEFFCode,Name,Rate\r\nA-1,\"Keo, loại \"\"tốt\"\"\",12.5\r\n";

        var result = await ParseAsync(csv);

        Assert.True(result.IsValid);
        var row = Assert.Single(result.Rows);
        Assert.Equal("A-1", row["Code"]);
        Assert.Equal("Keo, loại \"tốt\"", row["Name"]);
        Assert.Equal("12.5", row["Rate"]);
    }

    [Fact]
    public async Task ParseAsync_HandlesLfAndNewlineInsideQuotedField()
    {
        var result = await ParseAsync("Code,Name,Rate\nA,\"Dòng 1\nDòng 2\",1\n");

        Assert.True(result.IsValid);
        Assert.Equal("Dòng 1\nDòng 2", Assert.Single(result.Rows)["Name"]);
    }

    [Fact]
    public async Task ParseAsync_RejectsWrongHeaderOrder()
    {
        var result = await ParseAsync("Name,Code,Rate\nKeo,A,1");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Row == 1 && error.Message.Contains("Thứ tự bắt buộc"));
    }

    [Fact]
    public async Task ParseAsync_RejectsInvalidUtf8()
    {
        using var stream = new MemoryStream([0x43, 0x6f, 0x64, 0x65, 0xff]);

        var result = await _sut.ParseAsync(stream, Headers);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("UTF-8", error.Message);
        Assert.Equal("csv.error.utf8", error.MessageKey);
    }

    [Fact]
    public async Task ParseAsync_RejectsUnclosedQuoteAndLoneCarriageReturn()
    {
        var quoteResult = await ParseAsync("Code,Name,Rate\nA,\"Keo,1");
        var lineEndingResult = await ParseAsync("Code,Name,Rate\rA,Keo,1");

        Assert.Contains(quoteResult.Errors, error => error.Message.Contains("chưa đóng"));
        Assert.Contains(lineEndingResult.Errors, error => error.Message.Contains("LF hoặc CRLF"));
    }

    [Fact]
    public async Task ParseAsync_RejectsMoreThanConfiguredRows()
    {
        var result = await ParseAsync("Code,Name,Rate\nA,A,1\nB,B,2\nC,C,3", maxRows: 2);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("tối đa 2 dòng"));
    }

    [Fact]
    public async Task ParseAsync_RejectsMoreThanConfiguredBytes()
    {
        var result = await ParseAsync("Code,Name,Rate\nA,Keo,1", maxBytes: 10);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("dung lượng tối đa"));
    }

    private async Task<NihomeBackend.Models.DTOs.Responses.CsvImportResult> ParseAsync(
        string value,
        int maxBytes = 2 * 1024 * 1024,
        int maxRows = 2000)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return await _sut.ParseAsync(stream, Headers, maxBytes, maxRows);
    }
}
