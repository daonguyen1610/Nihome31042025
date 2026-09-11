using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ContractMilestoneNotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    [Fact]
    public async Task NotifyDueAsync_NotifiesOnceAndPersistsMarker()
    {
        var owner = new ApplicationUser
        {
            PhoneNumber = "0900000777",
            FullName = "Contract Owner",
            Email = "owner@test.local",
            PasswordHash = "test",
            IsActive = true,
        };
        var customer = new Customer { Name = "Due Reminder Customer", Type = CustomerType.Individual };
        _db.AddRange(owner, customer);
        await _db.SaveChangesAsync();
        var contract = new Contract
        {
            ContractNumber = "HD-DUE-0001",
            CustomerId = customer.Id,
            OwnerUserId = owner.Id,
            Direction = ContractDirection.Upstream,
            Type = ContractType.Design,
            Status = ContractStatus.Signed,
            SignedDate = DateTime.UtcNow.Date.AddDays(-1),
        };
        var milestone = new ContractPaymentMilestone
        {
            Contract = contract,
            Order = 1,
            Name = "Collection 1",
            PercentValue = 100m,
            DueDate = DateTime.UtcNow.Date,
            Status = PaymentMilestoneStatus.Pending,
        };
        _db.Add(milestone);
        await _db.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.NotifyManyFromTemplateAsync(
                It.IsAny<IEnumerable<int>>(),
                "crm.contract.milestone-due",
                It.IsAny<IDictionary<string, string>>(),
                nameof(ContractPaymentMilestone),
                milestone.Id,
                It.IsAny<string>(),
                "vi"))
            .ReturnsAsync(1);
        var service = new ContractMilestoneNotificationService(
            _db, notifications.Object, NullLogger<ContractMilestoneNotificationService>.Instance);

        await service.NotifyDueAsync();
        await service.NotifyDueAsync();

        notifications.Verify(item => item.NotifyManyFromTemplateAsync(
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { owner.Id })),
            "crm.contract.milestone-due",
            It.IsAny<IDictionary<string, string>>(),
            nameof(ContractPaymentMilestone),
            milestone.Id,
            $"/admin/finance/contracts/{contract.Id}?tab=schedule",
            "vi"), Times.Once);
        Assert.NotNull(_db.ContractPaymentMilestones.Single().DueNotificationSentAt);
    }

    [Fact]
    public async Task NotifyDueAsync_ExcludesDraftDownstreamAndPaidMilestones()
    {
        var owner = new ApplicationUser
        {
            PhoneNumber = "0900000778",
            FullName = "Excluded Owner",
            Email = "excluded@test.local",
            PasswordHash = "test",
            IsActive = true,
        };
        var customer = new Customer { Name = "Excluded Reminder Customer", Type = CustomerType.Individual };
        _db.AddRange(owner, customer);
        await _db.SaveChangesAsync();
        var contracts = new[]
        {
            new Contract { ContractNumber = "HD-DRAFT-DUE", CustomerId = customer.Id, OwnerUserId = owner.Id, Direction = ContractDirection.Upstream, Type = ContractType.Design, Status = ContractStatus.Draft },
            new Contract { ContractNumber = "HD-DOWN-DUE", CustomerId = customer.Id, OwnerUserId = owner.Id, Direction = ContractDirection.Downstream, Type = ContractType.Supply, Status = ContractStatus.Signed, SignedDate = DateTime.UtcNow.Date.AddDays(-1) },
            new Contract { ContractNumber = "HD-PAID-DUE", CustomerId = customer.Id, OwnerUserId = owner.Id, Direction = ContractDirection.Upstream, Type = ContractType.Design, Status = ContractStatus.Signed, SignedDate = DateTime.UtcNow.Date.AddDays(-1) },
        };
        _db.AddRange(contracts);
        await _db.SaveChangesAsync();
        _db.ContractPaymentMilestones.AddRange(contracts.Select((contract, index) => new ContractPaymentMilestone
        {
            ContractId = contract.Id,
            Order = 1,
            Name = $"Excluded {index}",
            PercentValue = 100m,
            DueDate = DateTime.UtcNow.Date,
            Status = index == 2 ? PaymentMilestoneStatus.Paid : PaymentMilestoneStatus.Pending,
            ActualPaymentDate = index == 2 ? DateTime.UtcNow.Date : null,
        }));
        await _db.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        var service = new ContractMilestoneNotificationService(
            _db, notifications.Object, NullLogger<ContractMilestoneNotificationService>.Instance);

        await service.NotifyDueAsync();

        notifications.Verify(item => item.NotifyManyFromTemplateAsync(
            It.IsAny<IEnumerable<int>>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.All(_db.ContractPaymentMilestones, item => Assert.Null(item.DueNotificationSentAt));
    }

    [Fact]
    public async Task NotifyDueAsync_NoActiveRecipient_LeavesMilestoneForRetry()
    {
        var owner = new ApplicationUser
        {
            PhoneNumber = "0900000779",
            FullName = "Inactive Contract Owner",
            Email = "inactive-owner@test.local",
            PasswordHash = "test",
            IsActive = false,
        };
        var customer = new Customer { Name = "Retry Reminder Customer", Type = CustomerType.Individual };
        _db.AddRange(owner, customer);
        await _db.SaveChangesAsync();
        var milestone = new ContractPaymentMilestone
        {
            Contract = new Contract
            {
                ContractNumber = "HD-DUE-RETRY",
                CustomerId = customer.Id,
                OwnerUserId = owner.Id,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Design,
                Status = ContractStatus.Signed,
                SignedDate = DateTime.UtcNow.Date.AddDays(-1),
            },
            Order = 1,
            Name = "Collection retry",
            PercentValue = 100m,
            DueDate = DateTime.UtcNow.Date,
            Status = PaymentMilestoneStatus.Pending,
        };
        _db.Add(milestone);
        await _db.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        var service = new ContractMilestoneNotificationService(
            _db, notifications.Object, NullLogger<ContractMilestoneNotificationService>.Instance);

        await service.NotifyDueAsync();

        notifications.Verify(item => item.NotifyManyFromTemplateAsync(
            It.IsAny<IEnumerable<int>>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Null(_db.ContractPaymentMilestones.Single().DueNotificationSentAt);
    }

    public void Dispose() => _db.Dispose();
}
