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
}
