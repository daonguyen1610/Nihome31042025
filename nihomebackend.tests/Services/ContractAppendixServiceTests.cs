using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContractAppendixServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContractAppendixService _sut;
    private readonly Contract _contract;

    public ContractAppendixServiceTests()
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

        _sut = new ContractAppendixService(_db, NullLogger<ContractAppendixService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private UpsertContractAppendixRequest Req(decimal delta = 50_000m, string title = "T", string reason = "R") =>
        new() { Title = title, Reason = reason, ValueDelta = delta };

    [Fact]
    public async Task Create_AllocatesVoNumberStartingAtOne()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), callerUserId: 100, canSeeAll: true);
        Assert.NotNull(vo);
        Assert.Equal(1, vo!.VoNumber);
        Assert.Equal(ContractAppendixStatus.Draft, vo.Status);
    }

    [Fact]
    public async Task Create_ThrowsOnZeroDelta()
    {
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(_contract.Id, Req(delta: 0m), 100, true));
    }

    [Fact]
    public async Task SubmitApprove_Workflow_Advances()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(delta: 25_000m), 100, true);
        var submitted = await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        Assert.Equal(ContractAppendixStatus.Submitted, submitted!.Status);
        Assert.NotNull(submitted.SubmittedAt);

        var approved = await _sut.ApproveAsync(_contract.Id, vo.Id, "ok", callerUserId: 200, canSeeAll: true);
        Assert.Equal(ContractAppendixStatus.Approved, approved!.Status);
        Assert.NotNull(approved.DecidedAt);
        Assert.Equal("ok", approved.DecisionNote);
    }

    [Fact]
    public async Task Reject_RequiresNote()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.RejectAsync(_contract.Id, vo.Id, note: null, 200, true));
        var rejected = await _sut.RejectAsync(_contract.Id, vo.Id, note: "bad", 200, true);
        Assert.Equal(ContractAppendixStatus.Rejected, rejected!.Status);
    }

    [Fact]
    public async Task Update_LockedOnceSubmitted()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.UpdateAsync(_contract.Id, vo.Id, Req(delta: 99_000m), 100, true));
    }

    [Fact]
    public async Task Update_RejectedRow_ResetsToDraft()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await _sut.RejectAsync(_contract.Id, vo.Id, "no", 200, true);
        var edited = await _sut.UpdateAsync(_contract.Id, vo.Id, Req(delta: 88_000m, title: "T2"), 100, true);
        Assert.NotNull(edited);
        Assert.Equal(ContractAppendixStatus.Draft, edited!.Status);
        Assert.Null(edited.SubmittedAt);
        Assert.Null(edited.DecidedAt);
    }

    [Fact]
    public async Task Delete_ApprovedRow_IsBlocked()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        await _sut.SubmitAsync(_contract.Id, vo!.Id, 100, true);
        await _sut.ApproveAsync(_contract.Id, vo.Id, null, 200, true);
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.DeleteAsync(_contract.Id, vo.Id, 100, true));
    }

    [Fact]
    public async Task Delete_DraftRow_Succeeds()
    {
        var vo = await _sut.CreateAsync(_contract.Id, Req(), 100, true);
        Assert.True(await _sut.DeleteAsync(_contract.Id, vo!.Id, 100, true));
        Assert.Empty(_db.ContractAppendices);
    }

    [Fact]
    public async Task List_ReturnsNullWhenSalesDoesNotOwn()
    {
        var rows = await _sut.ListAsync(_contract.Id, callerUserId: 999, canSeeAll: false);
        Assert.Null(rows);
    }
}
