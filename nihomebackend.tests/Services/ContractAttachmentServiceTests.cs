using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContractAttachmentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContractAttachmentService _sut;
    private readonly Contract _contract;

    public ContractAttachmentServiceTests()
    {
        _db = DbContextFactory.Create();
        _db.Customers.Add(new Customer { Name = "C", Type = CustomerType.Company });
        _db.SaveChanges();
        var customerId = _db.Customers.Single().Id;

        _contract = new Contract
        {
            ContractNumber = "HD-TEST-0001",
            CustomerId = customerId,
            OwnerUserId = 100,
            Value = 1_000_000m,
        };
        _db.Contracts.Add(_contract);
        _db.SaveChanges();

        _sut = new ContractAttachmentService(_db, NullLogger<ContractAttachmentService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private CreateContractAttachmentRequest Req(ContractAttachmentKind kind = ContractAttachmentKind.Supporting) =>
        new()
        {
            Kind = kind,
            FilePath = "/files/contracts/x.pdf",
            OriginalFileName = "x.pdf",
            FileSize = 1234,
            ContentType = "application/pdf",
            Label = "test",
        };

    [Fact]
    public async Task Create_PersistsAndReturnsMetadata()
    {
        var created = await _sut.CreateAsync(_contract.Id, Req(ContractAttachmentKind.SignedScan), 100, true);
        Assert.NotNull(created);
        Assert.Equal(ContractAttachmentKind.SignedScan, created!.Kind);
        Assert.Single(_db.ContractAttachments);
    }

    [Fact]
    public async Task List_OrdersSignedScanFirst()
    {
        await _sut.CreateAsync(_contract.Id, Req(ContractAttachmentKind.Supporting), 100, true);
        await _sut.CreateAsync(_contract.Id, Req(ContractAttachmentKind.SignedScan), 100, true);
        var rows = await _sut.ListAsync(_contract.Id, 100, true);
        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.Equal(ContractAttachmentKind.SignedScan, rows[0].Kind);
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        var created = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        Assert.True(await _sut.DeleteAsync(_contract.Id, created!.Id, 100, true));
        Assert.Empty(_db.ContractAttachments);
    }

    [Fact]
    public async Task Delete_ReturnsFalseWhenSalesDoesNotOwn()
    {
        var created = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        Assert.False(await _sut.DeleteAsync(_contract.Id, created!.Id, callerUserId: 999, canSeeAll: false));
    }
}
