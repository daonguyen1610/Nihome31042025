using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class CustomerDocumentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly string _contentRoot;
    private readonly CustomerDocumentService _sut;
    private readonly int _customerId;

    public CustomerDocumentServiceTests()
    {
        _db = DbContextFactory.Create();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-customer-documents-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _db.Customers.Add(new Customer { Name = "Customer", OwnerUserId = 100 });
        _db.SaveChanges();
        _customerId = _db.Customers.Single().Id;
        _sut = new CustomerDocumentService(
            _db,
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _contentRoot),
            NullLogger<CustomerDocumentService>.Instance);
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
            _customerId, CreateFile("customer.pdf"), "Contract brief", 100, canSeeAll: false);

        Assert.NotNull(result);
        Assert.Equal("customer.pdf", result!.OriginalFileName);
        Assert.Equal("Contract brief", result.Label);
        Assert.True(File.Exists(Path.Combine(_contentRoot, "wwwroot", result.FilePath.TrimStart('/'))));
        Assert.Single(_db.CustomerDocuments);
    }

    [Fact]
    public async Task Upload_UnsupportedFile_ThrowsWithoutWritingMetadata()
    {
        await Assert.ThrowsAsync<CustomerDocumentException>(() =>
            _sut.UploadAsync(_customerId, CreateFile("customer.exe"), null, 100, canSeeAll: false));

        Assert.Empty(_db.CustomerDocuments);
    }

    [Fact]
    public async Task List_UnassignedCustomer_IsAvailableToManager()
    {
        var customer = await _db.Customers.FindAsync(_customerId);
        customer!.OwnerUserId = null;
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(_customerId, callerUserId: 999, canSeeAll: true);

        Assert.NotNull(result);
        Assert.Empty(result!);
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