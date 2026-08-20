using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class QuoteDocumentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly string _contentRoot;
    private readonly QuoteDocumentService _sut;
    private readonly int _quoteId;

    public QuoteDocumentServiceTests()
    {
        _db = DbContextFactory.Create();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-quote-documents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);

        var customer = new Customer { Name = "Customer", OwnerUserId = 100 };
        var opportunity = new Opportunity { Name = "Opportunity", Customer = customer, OwnerUserId = 100 };
        var quote = new Quote { Code = "QT-TEST", Opportunity = opportunity, OwnerUserId = 100 };
        _db.Quotes.Add(quote);
        _db.SaveChanges();
        _quoteId = quote.Id;

        _sut = new QuoteDocumentService(
            _db,
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _contentRoot),
            NullLogger<QuoteDocumentService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    [Fact]
    public async Task Upload_ValidFile_PersistsMetadataAndManagedFile()
    {
        var result = await _sut.UploadAsync(
            _quoteId, CreateFile("quote.pdf"), "Signed quote", 100, canSeeAll: false);

        Assert.NotNull(result);
        Assert.Equal("quote.pdf", result!.OriginalFileName);
        Assert.Equal("Signed quote", result.Label);
        Assert.True(File.Exists(Path.Combine(
            _contentRoot,
            "wwwroot",
            "files",
            "quotes",
            _quoteId.ToString(),
            Path.GetFileName(result.FilePath))));
        Assert.Single(_db.QuoteDocuments);
    }

    [Fact]
    public async Task GetContent_OtherOwnersQuote_ReturnsNull()
    {
        var uploaded = await _sut.UploadAsync(
            _quoteId, CreateFile("quote.pdf"), null, 100, canSeeAll: false);

        var result = await _sut.GetContentAsync(
            _quoteId, uploaded!.Id, callerUserId: 999, canSeeAll: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task Upload_UnsupportedFile_ThrowsWithoutWritingMetadata()
    {
        await Assert.ThrowsAsync<QuoteDocumentException>(() =>
            _sut.UploadAsync(_quoteId, CreateFile("quote.exe"), null, 100, canSeeAll: false));

        Assert.Empty(_db.QuoteDocuments);
    }

    [Fact]
    public async Task List_OtherOwnersQuote_ReturnsNullWithoutViewAll()
    {
        var result = await _sut.ListAsync(_quoteId, callerUserId: 999, canSeeAll: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesMetadataAndManagedFile()
    {
        var uploaded = await _sut.UploadAsync(
            _quoteId, CreateFile("quote.pdf"), null, 100, canSeeAll: false);
        var fullPath = Path.Combine(
            _contentRoot,
            "wwwroot",
            "files",
            "quotes",
            _quoteId.ToString(),
            Path.GetFileName(uploaded!.FilePath));

        var removed = await _sut.DeleteAsync(
            _quoteId, uploaded.Id, 100, canSeeAll: false);

        Assert.True(removed);
        Assert.Empty(_db.QuoteDocuments);
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteQuoteFiles_RemovesQuoteDirectory()
    {
        var quoteDirectory = Path.Combine(
            _contentRoot, "wwwroot", "files", "quotes", _quoteId.ToString());
        Directory.CreateDirectory(quoteDirectory);
        await File.WriteAllTextAsync(Path.Combine(quoteDirectory, "quote.pdf"), "quote");

        _sut.DeleteQuoteFiles(_quoteId);

        Assert.False(Directory.Exists(quoteDirectory));
    }

    private static FormFile CreateFile(string fileName)
    {
        var stream = new MemoryStream("document"u8.ToArray());
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream",
        };
    }
}
