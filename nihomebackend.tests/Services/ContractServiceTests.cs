using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContractServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContractService _sut;
    private int _customerA;
    private int _customerB;

    public ContractServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ContractService(_db, NullLogger<ContractService>.Instance);

        _db.Customers.AddRange(
            new Customer { Name = "Customer A", Type = CustomerType.Company },
            new Customer { Name = "Customer B", Type = CustomerType.Individual });
        _db.SaveChanges();
        _customerA = _db.Customers.Single(c => c.Name == "Customer A").Id;
        _customerB = _db.Customers.Single(c => c.Name == "Customer B").Id;
    }

    public void Dispose() => _db.Dispose();

    private UpsertContractRequest Req(
        int? customerId = null,
        string? number = null,
        ContractStatus status = ContractStatus.Draft,
        decimal value = 1_000_000m,
        DateTime? signed = null,
        DateTime? start = null,
        DateTime? end = null,
        int? owner = null) =>
        new()
        {
            ContractNumber = number,
            CustomerId = customerId ?? _customerA,
            Status = status,
            Value = value,
            SignedDate = signed,
            StartDate = start,
            EndDate = end,
            OwnerUserId = owner,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task Create_AutoGeneratesContractNumber()
    {
        var result = await _sut.CreateAsync(Req(), callerUserId: 42, canReassignOwner: true);
        Assert.StartsWith("HD-", result.ContractNumber);
        Assert.EndsWith("-0001", result.ContractNumber);
        Assert.Equal(42, result.OwnerUserId);
    }

    [Fact]
    public async Task Create_IncrementsNumberSequenceInSameYear()
    {
        await _sut.CreateAsync(Req(), 1, canReassignOwner: true);
        var second = await _sut.CreateAsync(Req(customerId: _customerB), 1, canReassignOwner: true);
        Assert.EndsWith("-0002", second.ContractNumber);
    }

    [Fact]
    public async Task Create_WithExplicitNumberIsHonoured()
    {
        var result = await _sut.CreateAsync(Req(number: "HD-CUSTOM-001"), 1, canReassignOwner: true);
        Assert.Equal("HD-CUSTOM-001", result.ContractNumber);
    }

    [Fact]
    public async Task Create_DuplicateExplicitNumberThrows()
    {
        await _sut.CreateAsync(Req(number: "HD-DUP"), 1, canReassignOwner: true);
        await Assert.ThrowsAsync<ContractDuplicateNumberException>(
            () => _sut.CreateAsync(Req(customerId: _customerB, number: "HD-DUP"), 1, canReassignOwner: true));
    }

    [Fact]
    public async Task Create_UnknownCustomerThrowsValidation()
    {
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(Req(customerId: 9999), 1, canReassignOwner: true));
    }

    [Fact]
    public async Task Create_EndDateBeforeStartDateThrows()
    {
        var req = Req(start: new DateTime(2026, 3, 1), end: new DateTime(2026, 2, 1));
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(req, 1, canReassignOwner: true));
    }

    [Fact]
    public async Task Create_SalesCallerCannotReassignOwner()
    {
        // Sales user (canReassignOwner=false) tries to pin the contract to
        // a different owner. The service must force ownership to the caller
        // so the record stays inside their own scope.
        var result = await _sut.CreateAsync(Req(owner: 999), callerUserId: 100, canReassignOwner: false);
        Assert.Equal(100, result.OwnerUserId);
    }

    [Fact]
    public async Task Create_ManagerCallerCanReassignOwner()
    {
        var result = await _sut.CreateAsync(Req(owner: 999), callerUserId: 1, canReassignOwner: true);
        Assert.Equal(999, result.OwnerUserId);
    }

    // ---------------- List / RBAC scoping ----------------

    [Fact]
    public async Task List_SalesOnlySeesOwnRows_UnlessCanSeeAll()
    {
        await _sut.CreateAsync(Req(owner: 100), 100, canReassignOwner: true);
        await _sut.CreateAsync(Req(customerId: _customerB, owner: 200), 200, canReassignOwner: true);

        var salesView = await _sut.ListAsync(callerUserId: 100, canSeeAll: false);
        Assert.Equal(1, salesView.Total);
        Assert.All(salesView.Items, i => Assert.Equal(100, i.OwnerUserId));

        var managerView = await _sut.ListAsync(callerUserId: 100, canSeeAll: true);
        Assert.Equal(2, managerView.Total);
    }

    [Fact]
    public async Task List_SortsBySignedDateDesc_ThenCreatedAtDesc()
    {
        var older = await _sut.CreateAsync(Req(signed: new DateTime(2026, 1, 1)), 1, canReassignOwner: true);
        var newer = await _sut.CreateAsync(Req(customerId: _customerB, signed: new DateTime(2026, 6, 1)), 1, canReassignOwner: true);
        var unsigned = await _sut.CreateAsync(Req(customerId: _customerA, number: "HD-DRAFT"), 1, canReassignOwner: true);

        var view = await _sut.ListAsync(callerUserId: 1, canSeeAll: true);

        // Signed rows first (desc by signed date), unsigned trails.
        Assert.Equal(newer.Id, view.Items[0].Id);
        Assert.Equal(older.Id, view.Items[1].Id);
        Assert.Equal(unsigned.Id, view.Items[2].Id);
    }

    [Fact]
    public async Task List_AppliesStatusAndValueFilters()
    {
        await _sut.CreateAsync(Req(status: ContractStatus.Draft, value: 100), 1, canReassignOwner: true);
        await _sut.CreateAsync(Req(customerId: _customerB, status: ContractStatus.Signed, value: 1000), 1, canReassignOwner: true);

        var draft = await _sut.ListAsync(1, true, status: ContractStatus.Draft);
        Assert.Single(draft.Items);

        var expensive = await _sut.ListAsync(1, true, valueMin: 500);
        Assert.Single(expensive.Items);
        Assert.Equal(1000m, expensive.Items[0].Value);
    }

    [Fact]
    public async Task List_SignedToFilter_IsEndOfDayInclusive()
    {
        // Row signed later on the same UTC day the caller filtered up to.
        await _sut.CreateAsync(Req(signed: new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc)), 1, canReassignOwner: true);
        // Row signed the next day — should be excluded.
        await _sut.CreateAsync(Req(customerId: _customerB, signed: new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc)), 1, canReassignOwner: true);

        var upToJune15 = await _sut.ListAsync(1, true, signedTo: new DateTime(2026, 6, 15));
        Assert.Single(upToJune15.Items);
        Assert.Equal(new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc), upToJune15.Items[0].SignedDate!.Value);
    }

    [Fact]
    public async Task List_SearchMatchesContractNumberOrCustomerName()
    {
        await _sut.CreateAsync(Req(number: "HD-FIND-001"), 1, canReassignOwner: true);
        await _sut.CreateAsync(Req(customerId: _customerB, number: "HD-OTHER"), 1, canReassignOwner: true);

        var byNumber = await _sut.ListAsync(1, true, search: "FIND");
        Assert.Single(byNumber.Items);

        var byCustomer = await _sut.ListAsync(1, true, search: "Customer B");
        Assert.Single(byCustomer.Items);
        Assert.Equal("Customer B", byCustomer.Items[0].CustomerName);
    }

    // ---------------- Update / Delete ----------------

    [Fact]
    public async Task Update_SalesCannotEditOtherOwnersRow()
    {
        var owned = await _sut.CreateAsync(Req(owner: 100), 100, canReassignOwner: true);
        var result = await _sut.UpdateAsync(owned.Id, Req(owner: 100, status: ContractStatus.Signed), callerUserId: 200, canSeeAll: false, canReassignOwner: false);
        Assert.Null(result);

        var forbidden = await _sut.GetAsync(owned.Id, callerUserId: 200, canSeeAll: false);
        Assert.Null(forbidden);
    }

    [Fact]
    public async Task Update_SalesCannotReassignOwner()
    {
        var owned = await _sut.CreateAsync(Req(owner: 100), 100, canReassignOwner: true);
        var req = Req(owner: 999);
        var result = await _sut.UpdateAsync(owned.Id, req, callerUserId: 100, canSeeAll: false, canReassignOwner: false);
        Assert.NotNull(result);
        // Owner unchanged — the request-supplied owner is ignored for sales.
        Assert.Equal(100, result!.OwnerUserId);
    }

    [Fact]
    public async Task Update_ManagerCanReassignOwner()
    {
        var owned = await _sut.CreateAsync(Req(owner: 100), 100, canReassignOwner: true);
        var req = Req(owner: 999);
        var result = await _sut.UpdateAsync(owned.Id, req, callerUserId: 1, canSeeAll: true, canReassignOwner: true);
        Assert.NotNull(result);
        Assert.Equal(999, result!.OwnerUserId);
    }

    [Fact]
    public async Task Update_DuplicateNumberThrows()
    {
        await _sut.CreateAsync(Req(number: "HD-A"), 1, canReassignOwner: true);
        var b = await _sut.CreateAsync(Req(customerId: _customerB, number: "HD-B"), 1, canReassignOwner: true);

        var req = Req(customerId: _customerB, number: "HD-A");
        await Assert.ThrowsAsync<ContractDuplicateNumberException>(
            () => _sut.UpdateAsync(b.Id, req, 1, canSeeAll: true, canReassignOwner: true));
    }

    [Fact]
    public async Task Delete_SalesCanOnlyDeleteOwnRows()
    {
        var owned = await _sut.CreateAsync(Req(owner: 100), 100, canReassignOwner: true);
        Assert.False(await _sut.DeleteAsync(owned.Id, callerUserId: 200, canSeeAll: false));
        Assert.True(await _sut.DeleteAsync(owned.Id, callerUserId: 100, canSeeAll: false));
    }

    // ---------------- Payment milestones (NIH-103) ----------------

    private static ContractPaymentMilestoneRequest Milestone(int order, decimal percent, string? name = null) =>
        new()
        {
            Order = order,
            Name = name ?? $"Đợt {order}",
            PercentValue = percent,
            Status = PaymentMilestoneStatus.Pending,
        };

    [Fact]
    public async Task Create_WithMilestonesSumming100_PersistsAndComputesAmount()
    {
        var req = Req(value: 1_000_000_000m);
        req.PaymentMilestones = new()
        {
            Milestone(1, 30m, "Tạm ứng"),
            Milestone(2, 60m, "Nghiệm thu giai đoạn"),
            Milestone(3, 10m, "Quyết toán"),
        };

        var result = await _sut.CreateAsync(req, 1, canReassignOwner: true);

        Assert.Equal(3, result.PaymentMilestones.Count);
        Assert.Equal(300_000_000m, result.PaymentMilestones[0].Amount);
        Assert.Equal(600_000_000m, result.PaymentMilestones[1].Amount);
        Assert.Equal(100_000_000m, result.PaymentMilestones[2].Amount);
        Assert.Equal(new[] { 1, 2, 3 }, result.PaymentMilestones.Select(m => m.Order));
    }

    [Fact]
    public async Task Create_MilestonePercentsMustSumTo100()
    {
        var req = Req();
        req.PaymentMilestones = new() { Milestone(1, 40m), Milestone(2, 40m) };
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(req, 1, canReassignOwner: true));
    }

    [Fact]
    public async Task Create_MilestoneDuplicateOrderThrows()
    {
        var req = Req();
        req.PaymentMilestones = new() { Milestone(1, 50m), Milestone(1, 50m) };
        await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.CreateAsync(req, 1, canReassignOwner: true));
    }

    [Fact]
    public async Task Create_EmptyMilestonesListIsAllowed_AndPersistsNothing()
    {
        var req = Req();
        req.PaymentMilestones = new List<ContractPaymentMilestoneRequest>();
        var result = await _sut.CreateAsync(req, 1, canReassignOwner: true);
        Assert.Empty(result.PaymentMilestones);
    }

    [Fact]
    public async Task Update_NullMilestonesLeavesExistingScheduleUntouched()
    {
        var initial = Req(value: 100m);
        initial.PaymentMilestones = new() { Milestone(1, 100m, "Full") };
        var contract = await _sut.CreateAsync(initial, 1, canReassignOwner: true);
        Assert.Single(contract.PaymentMilestones);

        var update = Req(value: 100m);
        update.PaymentMilestones = null; // null → preserve
        var refreshed = await _sut.UpdateAsync(contract.Id, update, 1, canSeeAll: true, canReassignOwner: true);
        Assert.NotNull(refreshed);
        Assert.Single(refreshed!.PaymentMilestones);
    }

    [Fact]
    public async Task Update_EmptyMilestonesListWipesTheSchedule()
    {
        var initial = Req();
        initial.PaymentMilestones = new() { Milestone(1, 100m, "Full") };
        var contract = await _sut.CreateAsync(initial, 1, canReassignOwner: true);

        var update = Req();
        update.PaymentMilestones = new List<ContractPaymentMilestoneRequest>();
        var refreshed = await _sut.UpdateAsync(contract.Id, update, 1, canSeeAll: true, canReassignOwner: true);
        Assert.NotNull(refreshed);
        Assert.Empty(refreshed!.PaymentMilestones);
    }

    [Fact]
    public async Task Update_ReplacementMilestoneListSwapsRows()
    {
        var initial = Req();
        initial.PaymentMilestones = new() { Milestone(1, 100m, "Full") };
        var contract = await _sut.CreateAsync(initial, 1, canReassignOwner: true);

        var update = Req();
        update.PaymentMilestones = new()
        {
            Milestone(1, 50m, "First"),
            Milestone(2, 50m, "Second"),
        };
        var refreshed = await _sut.UpdateAsync(contract.Id, update, 1, canSeeAll: true, canReassignOwner: true);
        Assert.NotNull(refreshed);
        Assert.Equal(2, refreshed!.PaymentMilestones.Count);
        Assert.Equal("First", refreshed.PaymentMilestones[0].Name);
        Assert.Equal("Second", refreshed.PaymentMilestones[1].Name);
    }

    [Fact]
    public async Task Delete_ContractCascadesToMilestones()
    {
        var req = Req();
        req.PaymentMilestones = new() { Milestone(1, 100m, "Full") };
        var contract = await _sut.CreateAsync(req, 1, canReassignOwner: true);
        Assert.Single(_db.ContractPaymentMilestones);

        Assert.True(await _sut.DeleteAsync(contract.Id, 1, canSeeAll: true));
        Assert.Empty(_db.ContractPaymentMilestones);
    }

    // ---------------- State transitions (NIH-104) ----------------

    [Fact]
    public async Task Transition_DraftToSigned_StampsSignedDateWhenMissing()
    {
        var contract = await _sut.CreateAsync(Req(), 1, canReassignOwner: true);
        Assert.Null(contract.SignedDate);

        var updated = await _sut.TransitionStatusAsync(contract.Id, ContractStatus.Signed, 1, canSeeAll: true);
        Assert.NotNull(updated);
        Assert.Equal(ContractStatus.Signed, updated!.Status);
        Assert.NotNull(updated.SignedDate);
    }

    [Fact]
    public async Task Transition_DraftToInProgress_IsRejected()
    {
        var contract = await _sut.CreateAsync(Req(), 1, canReassignOwner: true);
        var ex = await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.TransitionStatusAsync(contract.Id, ContractStatus.InProgress, 1, canSeeAll: true));
        Assert.Contains("Illegal", ex.Message);
    }

    [Fact]
    public async Task Transition_SignedToInProgress_RequiresSignedScan()
    {
        var contract = await _sut.CreateAsync(Req(status: ContractStatus.Signed), 1, canReassignOwner: true);
        var ex = await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.TransitionStatusAsync(contract.Id, ContractStatus.InProgress, 1, canSeeAll: true));
        Assert.Contains("scan", ex.Message);

        _db.ContractAttachments.Add(new ContractAttachment
        {
            ContractId = contract.Id,
            Kind = ContractAttachmentKind.SignedScan,
            FilePath = "/files/contracts/scan.pdf",
            OriginalFileName = "scan.pdf",
            FileSize = 1,
            ContentType = "application/pdf",
        });
        _db.SaveChanges();

        var updated = await _sut.TransitionStatusAsync(contract.Id, ContractStatus.InProgress, 1, canSeeAll: true);
        Assert.Equal(ContractStatus.InProgress, updated!.Status);
    }

    [Fact]
    public async Task Transition_InProgressToCompleted_RequiresAllMilestonesPaid()
    {
        var initial = Req(status: ContractStatus.InProgress, value: 100m);
        initial.PaymentMilestones = new()
        {
            Milestone(1, 50m, "A"),
            Milestone(2, 50m, "B"),
        };
        var contract = await _sut.CreateAsync(initial, 1, canReassignOwner: true);

        // Not all paid yet
        var ex = await Assert.ThrowsAsync<ContractValidationException>(
            () => _sut.TransitionStatusAsync(contract.Id, ContractStatus.Completed, 1, canSeeAll: true));
        Assert.Contains("Đã thanh toán", ex.Message);

        foreach (var m in _db.ContractPaymentMilestones)
        {
            m.Status = PaymentMilestoneStatus.Paid;
        }
        _db.SaveChanges();

        var updated = await _sut.TransitionStatusAsync(contract.Id, ContractStatus.Completed, 1, canSeeAll: true);
        Assert.Equal(ContractStatus.Completed, updated!.Status);
    }

    [Fact]
    public async Task Transition_NoOp_ReturnsCurrentState()
    {
        var contract = await _sut.CreateAsync(Req(status: ContractStatus.Signed, signed: DateTime.UtcNow), 1, canReassignOwner: true);
        var updated = await _sut.TransitionStatusAsync(contract.Id, ContractStatus.Signed, 1, canSeeAll: true);
        Assert.NotNull(updated);
        Assert.Equal(ContractStatus.Signed, updated!.Status);
    }

    [Fact]
    public async Task UpdateMilestoneStatus_MutatesTheSpecifiedRow()
    {
        var initial = Req(value: 100m);
        initial.PaymentMilestones = new() { Milestone(1, 100m, "Full") };
        var contract = await _sut.CreateAsync(initial, 1, canReassignOwner: true);
        var milestoneId = contract.PaymentMilestones[0].Id;

        var updated = await _sut.UpdateMilestoneStatusAsync(
            contract.Id, milestoneId, PaymentMilestoneStatus.Paid, 1, canSeeAll: true);
        Assert.NotNull(updated);
        Assert.Equal(PaymentMilestoneStatus.Paid, updated!.PaymentMilestones.Single().Status);
    }

    [Fact]
    public async Task UpdateMilestoneStatus_ReturnsNullWhenSalesDoesNotOwn()
    {
        var contract = await _sut.CreateAsync(Req(owner: 100), callerUserId: 100, canReassignOwner: true);
        var milestone = new ContractPaymentMilestone
        {
            ContractId = contract.Id,
            Order = 1,
            Name = "x",
            PercentValue = 100m,
        };
        _db.ContractPaymentMilestones.Add(milestone);
        _db.SaveChanges();

        var result = await _sut.UpdateMilestoneStatusAsync(
            contract.Id, milestone.Id, PaymentMilestoneStatus.Paid,
            callerUserId: 999, canSeeAll: false);
        Assert.Null(result);
    }

    // ---------------- CurrentValue derivation ----------------

    [Fact]
    public async Task Get_CurrentValue_IncludesApprovedVoTotal()
    {
        var contract = await _sut.CreateAsync(Req(value: 1_000_000m), 1, canReassignOwner: true);

        _db.ContractAppendices.AddRange(
            new ContractAppendix
            {
                ContractId = contract.Id,
                VoNumber = 1,
                Title = "Approved +100k",
                Reason = "test",
                ValueDelta = 100_000m,
                Status = ContractAppendixStatus.Approved,
            },
            new ContractAppendix
            {
                ContractId = contract.Id,
                VoNumber = 2,
                Title = "Submitted +200k",
                Reason = "test",
                ValueDelta = 200_000m,
                Status = ContractAppendixStatus.Submitted,
            });
        _db.SaveChanges();

        var refreshed = await _sut.GetAsync(contract.Id, 1, canSeeAll: true);
        Assert.NotNull(refreshed);
        Assert.Equal(100_000m, refreshed!.ApprovedVoTotal);
        Assert.Equal(1_100_000m, refreshed.CurrentValue);
        Assert.Equal(2, refreshed.AppendixCount);
    }

    [Fact]
    public async Task Get_HasSignedScan_ReflectsAttachmentKind()
    {
        var contract = await _sut.CreateAsync(Req(), 1, canReassignOwner: true);
        Assert.False((await _sut.GetAsync(contract.Id, 1, canSeeAll: true))!.HasSignedScan);

        _db.ContractAttachments.Add(new ContractAttachment
        {
            ContractId = contract.Id,
            Kind = ContractAttachmentKind.Supporting,
            FilePath = "/x",
            OriginalFileName = "x.pdf",
            ContentType = "application/pdf",
        });
        _db.SaveChanges();
        Assert.False((await _sut.GetAsync(contract.Id, 1, canSeeAll: true))!.HasSignedScan);

        _db.ContractAttachments.Add(new ContractAttachment
        {
            ContractId = contract.Id,
            Kind = ContractAttachmentKind.SignedScan,
            FilePath = "/y",
            OriginalFileName = "y.pdf",
            ContentType = "application/pdf",
        });
        _db.SaveChanges();
        Assert.True((await _sut.GetAsync(contract.Id, 1, canSeeAll: true))!.HasSignedScan);
    }
}
