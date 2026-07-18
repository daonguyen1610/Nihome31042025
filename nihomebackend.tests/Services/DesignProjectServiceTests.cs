using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-113 DesignProject overview slice.
/// Uses InMemory EF (no HTTP) — controller / RBAC coverage lives in
/// the integration suite.
/// </summary>
public class DesignProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DesignProjectService _sut;
    private readonly int _userId;
    private readonly int _customerId;
    private readonly int _contractId;

    public DesignProjectServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new DesignProjectService(_db, NullLogger<DesignProjectService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000020",
            FullName = "PM Tester",
            Email = "pm.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;

        var customer = new Customer
        {
            Name = "Alpha Corp",
            SourceCode = "referral",
            RelationshipStatus = CustomerRelationshipStatus.InProgress,
            Type = CustomerType.Company,
        };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;

        var contract = new Contract
        {
            ContractNumber = "HD-2026-9999",
            CustomerId = _customerId,
            Value = 1000000,
            Status = ContractStatus.InProgress,
        };
        _db.Contracts.Add(contract);
        _db.SaveChanges();
        _contractId = contract.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateDesignProjectRequest ValidCreate(
        string? name = null,
        int? customerId = null,
        int? contractId = null,
        DateTime? start = null,
        DateTime? deadline = null) => new()
        {
            Name = name ?? "Nhà máy Alpha - Giai đoạn 1",
            CustomerId = customerId ?? _customerId,
            ContractId = contractId,
            StartDate = start,
            Deadline = deadline,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesCode()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith($"DP-{DateTime.UtcNow.Year}-", resp.ProjectCode);
        Assert.EndsWith("-0001", resp.ProjectCode);
        Assert.Equal("Concept", resp.CurrentStage);
        Assert.Equal("Active", resp.Status);
    }

    [Fact]
    public async Task CreateAsync_MissingName_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(name: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownCustomer_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(customerId: 99999), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownContract_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(contractId: 99999), _userId));
    }

    [Fact]
    public async Task CreateAsync_DeadlineBeforeStart_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(
                ValidCreate(start: DateTime.UtcNow, deadline: DateTime.UtcNow.AddDays(-1)),
                _userId));
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerYear()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(name: "Villa Bãi Dài"), _userId);
        Assert.EndsWith("-0001", a.ProjectCode);
        Assert.EndsWith("-0002", b.ProjectCode);
    }

    // ---------------- Get / List ----------------

    [Fact]
    public async Task GetAsync_UnknownReturnsNull()
    {
        Assert.Null(await _sut.GetAsync(99999));
    }

    [Fact]
    public async Task GetAsync_HydratesCustomerAndContractNames()
    {
        var created = await _sut.CreateAsync(ValidCreate(contractId: _contractId), _userId);
        var got = await _sut.GetAsync(created.Id);
        Assert.NotNull(got);
        Assert.Equal("Alpha Corp", got!.CustomerName);
        Assert.Equal("HD-2026-9999", got.ContractNumber);
    }

    [Fact]
    public async Task ListAsync_FiltersByStage()
    {
        var a = await _sut.CreateAsync(ValidCreate(name: "Row A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(name: "Row B"), _userId);
        await _sut.UpdateAsync(b.Id, new UpdateDesignProjectRequest
        {
            Name = b.Name,
            CustomerId = _customerId,
            CurrentStage = "BasicDesign",
        }, _userId);

        var basic = await _sut.ListAsync(new DesignProjectListParams { Stage = "BasicDesign" });
        Assert.Single(basic.Items);
        Assert.Equal(b.Id, basic.Items[0].Id);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_UnknownReturnsNull()
    {
        var req = new UpdateDesignProjectRequest
        {
            Name = "X",
            CustomerId = _customerId,
        };
        Assert.Null(await _sut.UpdateAsync(99999, req, _userId));
    }

    [Fact]
    public async Task UpdateAsync_InvalidStage_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var req = new UpdateDesignProjectRequest
        {
            Name = created.Name,
            CustomerId = _customerId,
            CurrentStage = "NotAStage",
        };
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.UpdateAsync(created.Id, req, _userId));
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_ConceptStage_Deletes()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var removed = await _sut.DeleteAsync(created.Id);
        Assert.True(removed);
        Assert.Null(await _sut.GetAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_BeyondConcept_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.UpdateAsync(created.Id, new UpdateDesignProjectRequest
        {
            Name = created.Name,
            CustomerId = _customerId,
            CurrentStage = "BasicDesign",
        }, _userId);
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.DeleteAsync(created.Id));
    }

    // ---------------- Auto-create hook ----------------

    [Fact]
    public async Task EnsureForContractAsync_CreatesRowFirstTime()
    {
        var contract = await _db.Contracts.FirstAsync(c => c.Id == _contractId);
        var dp = await _sut.EnsureForContractAsync(contract, _userId);
        Assert.NotNull(dp);
        Assert.Equal(_contractId, dp.ContractId);
        Assert.StartsWith("DP-", dp.ProjectCode);
    }

    [Fact]
    public async Task EnsureForContractAsync_Idempotent()
    {
        var contract = await _db.Contracts.FirstAsync(c => c.Id == _contractId);
        var first = await _sut.EnsureForContractAsync(contract, _userId);
        var second = await _sut.EnsureForContractAsync(contract, _userId);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _db.DesignProjects.CountAsync(dp => dp.ContractId == _contractId));
    }
}
