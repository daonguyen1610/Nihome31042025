using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class QuoteApprovalEmailServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IPermissionService> permissions = new();
    private readonly Mock<IEmailService> email = new();

    public void Dispose() => db.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Worker_SendsCcOnlyWhileTeamMemberStillCanViewQuote(bool ownerCanView)
    {
        var managerRole = new Role { Code = "PROCUREMENT_MANAGER", Name = "Head of Procurement" };
        var owner = new ApplicationUser
        {
            Email = "sales@example.com",
            PhoneNumber = "0901234001",
            FullName = "Sales",
            IsActive = true,
        };
        var approver = new ApplicationUser
        {
            Email = "manager@example.com",
            PhoneNumber = "0901234002",
            FullName = "Manager",
            RoleEntity = managerRole,
            IsActive = true,
        };
        var project = new OperationalProject { Code = "PJ-QUOTE-CC", Name = "Dự án báo giá" };
        db.AddRange(owner, approver, project);
        await db.SaveChangesAsync();
        var quote = new Quote
        {
            Code = "QT-CC-01",
            Opportunity = new Opportunity { Name = "Cơ hội báo giá" },
            OperationalProjectId = project.Id,
            OwnerUserId = owner.Id,
            Status = QuoteStatus.PendingApproval,
            ApprovalApproverUserId = approver.Id,
            SubmittedByUserId = owner.Id,
        };
        db.Quotes.Add(quote);
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = owner.Id,
            Position = "Sales",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
        });
        await db.SaveChangesAsync();
        db.QuoteApprovalEmails.Add(new QuoteApprovalEmail
        {
            QuoteId = quote.Id,
            ApproverUserId = approver.Id,
            ToEmail = approver.Email,
            CcEmailsJson = JsonSerializer.Serialize(new[] { owner.Email }),
            Subject = "Duyệt báo giá",
            Body = "<p>Xin duyệt</p>",
        });
        await db.SaveChangesAsync();
        permissions.Setup(item => item.HasAsync(approver.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissions.Setup(item => item.HasAsync(owner.Id, "crm.quotes.view", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerCanView);
        email.Setup(item => item.SendEmailWithCcAsync(
                approver.Email, It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new QuoteApprovalEmailService(db, new EphemeralDataProtectionProvider(),
            permissions.Object, email.Object, new ConfigurationManager(),
            new HttpContextAccessor(), NullLogger<QuoteApprovalEmailService>.Instance);

        await service.SendPendingAsync(default);

        var snapshot = await db.QuoteApprovalEmails.SingleAsync();
        Assert.Equal(ownerCanView ? "Sent" : "NeedsReview", snapshot.Status);
        email.Verify(item => item.SendEmailWithCcAsync(approver.Email,
            It.Is<IReadOnlyCollection<string>>(cc => cc.Count == 1 && cc.Contains(owner.Email)),
            "Duyệt báo giá", "<p>Xin duyệt</p>", It.IsAny<CancellationToken>()),
            ownerCanView ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task QueueDecision_AddressesAuthorAndCcsTheTeam()
    {
        var (quote, owner, approver) = await SeedPendingQuoteAsync();
        var teammate = new ApplicationUser
        {
            Email = "design@example.com",
            PhoneNumber = "0901234003",
            FullName = "Design",
            IsActive = true,
        };
        db.Users.Add(teammate);
        await db.SaveChangesAsync();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = quote.OperationalProjectId!.Value,
            UserId = teammate.Id,
            Position = "Design",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
        });
        await db.SaveChangesAsync();
        permissions.Setup(item => item.HasAsync(It.IsAny<int>(), "crm.quotes.view", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissions.Setup(item => item.HasAsync(It.IsAny<int>(), "crm.quotes.view.all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        await service.QueueDecisionAsync(quote.Id, approver.Id, approved: true, "Đồng ý", default);
        await db.SaveChangesAsync();

        var message = await db.QuoteApprovalEmails.SingleAsync();
        Assert.Equal("Decision", message.Kind);
        Assert.Equal("Approved", message.Decision);
        Assert.Equal(owner.Email, message.ToEmail);
        Assert.Equal(owner.Id, message.RecipientUserId);
        Assert.Equal(approver.Id, message.ApproverUserId);
        foreach (var cc in JsonSerializer.Deserialize<string[]>(message.CcEmailsJson)!)
            Assert.NotEqual(owner.Email, cc, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decision_IsDeliveredWhenAuthorIsAlsoATeamMember()
    {
        var (quote, owner, approver) = await SeedPendingQuoteAsync();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = quote.OperationalProjectId!.Value,
            UserId = owner.Id,
            Position = "Sales",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
        });
        await db.SaveChangesAsync();
        permissions.Setup(item => item.HasAsync(It.IsAny<int>(), "crm.quotes.view", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();
        await service.QueueDecisionAsync(quote.Id, approver.Id, approved: true, null, default);

        quote.Status = QuoteStatus.Approved;
        await db.SaveChangesAsync();
        email.Setup(item => item.SendEmailWithCcAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.SendPendingAsync(default);

        Assert.Equal("Sent", (await db.QuoteApprovalEmails.SingleAsync()).Status);
    }

    [Fact]
    public async Task RejectedDecision_IsStillDeliveredAfterRejectClearsSubmitter()
    {
        var (quote, owner, approver) = await SeedPendingQuoteAsync();
        var service = CreateService();
        await service.QueueDecisionAsync(quote.Id, approver.Id, approved: false, null, default);

        permissions.Setup(item => item.HasAsync(owner.Id, "crm.quotes.view", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Rejecting a quote clears the submitter and approver columns in the same save.
        quote.Status = QuoteStatus.Draft;
        quote.SubmittedByUserId = null;
        quote.ApprovalApproverUserId = null;
        await db.SaveChangesAsync();
        email.Setup(item => item.SendEmailWithCcAsync(owner.Email, It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.SendPendingAsync(default);

        var message = await db.QuoteApprovalEmails.SingleAsync();
        Assert.Equal("Sent", message.Status);
        email.Verify(item => item.SendEmailWithCcAsync(owner.Email, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovedDecision_IsHeldForReviewWhenQuoteIsNotApproved()
    {
        var (quote, _, approver) = await SeedPendingQuoteAsync();
        var service = CreateService();
        await service.QueueDecisionAsync(quote.Id, approver.Id, approved: true, null, default);

        quote.Status = QuoteStatus.Draft;
        quote.SubmittedByUserId = null;
        quote.ApprovalApproverUserId = null;
        await db.SaveChangesAsync();

        await service.SendPendingAsync(default);

        Assert.Equal("NeedsReview", (await db.QuoteApprovalEmails.SingleAsync()).Status);
        email.Verify(item => item.SendEmailWithCcAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecisionWorker_RevalidatesSubmitterIdentityAndQuoteAccess(bool emailReassigned)
    {
        var (quote, owner, approver) = await SeedPendingQuoteAsync();
        permissions.Setup(item => item.HasAsync(owner.Id, "crm.quotes.view", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();
        await service.QueueDecisionAsync(quote.Id, approver.Id, true, null, default);
        quote.Status = QuoteStatus.Approved;
        await db.SaveChangesAsync();
        if (emailReassigned)
        {
            var previousEmail = owner.Email;
            owner.Email = "changed@example.test";
            db.Users.Add(new ApplicationUser
            {
                Email = previousEmail,
                PhoneNumber = "0901234999",
                FullName = "NICON – Người dùng khác",
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }
        else
            permissions.Setup(item => item.HasAsync(owner.Id, "crm.quotes.view", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        await service.SendPendingAsync(default);

        Assert.Equal("NeedsReview", (await db.QuoteApprovalEmails.SingleAsync()).Status);
        email.Verify(item => item.SendEmailWithCcAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Decision_CannotBeQueuedBySomeoneOtherThanTheApprover()
    {
        var (quote, _, approver) = await SeedPendingQuoteAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<QuoteOperationException>(() =>
            service.QueueDecisionAsync(quote.Id, quote.OwnerUserId!.Value, approved: true, null, default));
    }

    private async Task<(Quote Quote, ApplicationUser Owner, ApplicationUser Approver)> SeedPendingQuoteAsync()
    {
        var managerRole = new Role { Code = RbacRoleCodes.ProcurementManager, Name = "Head of Procurement" };
        var owner = new ApplicationUser
        {
            Email = "sales@example.com",
            PhoneNumber = "0901234001",
            FullName = "Sales",
            IsActive = true,
        };
        var approver = new ApplicationUser
        {
            Email = "manager@example.com",
            PhoneNumber = "0901234002",
            FullName = "Manager",
            RoleEntity = managerRole,
            IsActive = true,
        };
        var project = new OperationalProject { Code = "PJ-DECISION", Name = "Dự án quyết định" };
        db.AddRange(owner, approver, project);
        await db.SaveChangesAsync();
        var quote = new Quote
        {
            Code = "QT-DECISION-01",
            Opportunity = new Opportunity { Name = "Cơ hội quyết định" },
            OperationalProjectId = project.Id,
            OwnerUserId = owner.Id,
            Status = QuoteStatus.PendingApproval,
            ApprovalApproverUserId = approver.Id,
            SubmittedByUserId = owner.Id,
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return (quote, owner, approver);
    }

    private QuoteApprovalEmailService CreateService() => new(db, new EphemeralDataProtectionProvider(),
        permissions.Object, email.Object,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:PublicBaseUrl"] = "https://nicon.example",
        }).Build(),
        new HttpContextAccessor(), NullLogger<QuoteApprovalEmailService>.Instance);
}
