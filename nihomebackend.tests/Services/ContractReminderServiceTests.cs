using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ContractReminderServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IEmailService> mail = new();
    private readonly Mock<IPermissionService> permissions = new();
    private readonly EphemeralDataProtectionProvider protection = new();

    private ContractReminderService Service => new(db, protection, mail.Object, permissions.Object,
        NullLogger<ContractReminderService>.Instance);

    private async Task<Contract> SeedAsync()
    {
        permissions.Setup(item => item.HasAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user = new ApplicationUser { PhoneNumber = "0900000888", FullName = "NICON Sales", IsActive = true, PasswordHash = "test" };
        var customer = new Customer { Name = "An Phú <factory>", Contacts = [new() { FullName = "Mai Anh", Email = "mai.anh@example.test", IsPrimary = true }] };
        var contract = new Contract
        {
            ContractNumber = "HD-ANPHU-2026",
            Customer = customer,
            Owner = user,
            Status = ContractStatus.Signed,
            Direction = ContractDirection.Upstream,
            Type = ContractType.DesignAndBuild,
            EndDate = ContractReminderService.Today.AddDays(30),
            SignedDate = ContractReminderService.Today.AddDays(-30)
        };
        db.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }

    private async Task<ContractReminder> ScheduleAsync(Contract contract, string kind = "Renewal", int days = 0)
    {
        var service = Service;
        var preview = await service.PreviewAsync(contract.Id, new ContractReminderPreviewRequest
        { Kind = kind, ReminderDate = DateOnly.FromDateTime(ContractReminderService.Today.AddDays(days)) }, contract.OwnerUserId!.Value, false, default);
        Assert.NotNull(preview);
        Assert.Contains("&lt;factory&gt;", preview.Body);
        Assert.DoesNotContain("<factory>", preview.Body);
        Assert.Contains("An Phú <factory>", System.Net.WebUtility.HtmlDecode(preview.Body));
        var result = await service.ScheduleAsync(contract.Id, preview.PreviewToken, contract.OwnerUserId.Value, false, default);
        return await db.ContractReminders.SingleAsync(item => item.Id == result!.Id);
    }

    [Theory]
    [InlineData("Renewal")]
    [InlineData("End")]
    public async Task DueEmail_UsesApprovedSnapshot_Once_AndRecordsCustomerCare(string kind)
    {
        var contract = await SeedAsync();
        var reminder = await ScheduleAsync(contract, kind);
        db.SiteSettings.Add(new SiteSettings { ContractRenewalEmailSubjectTemplate = "Changed AFTER approval" });
        await db.SaveChangesAsync();
        await Service.SendDueAsync();
        await Service.SendDueAsync();
        mail.Verify(item => item.SendEmailAsync("mai.anh@example.test", reminder.Subject, reminder.Body), Times.Once);
        Assert.Equal("Sent", reminder.Status);
        Assert.NotNull(reminder.SentAt);
        Assert.Single(db.ContractCareActivities);
        Assert.Equal(ContractStatus.Signed, contract.Status);
    }

    [Fact]
    public async Task FutureAndCancelledSchedules_DoNotSend()
    {
        var contract = await SeedAsync();
        await ScheduleAsync(contract, days: 1);
        var end = await ScheduleAsync(contract, "End");
        Assert.True(await Service.CancelAsync(contract.Id, end.Id, contract.OwnerUserId!.Value, false, default));
        await Service.SendDueAsync();
        mail.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(db.ContractCareActivities);
    }

    [Theory]
    [InlineData("endDate")]
    [InlineData("email")]
    [InlineData("customer")]
    [InlineData("cancelled")]
    [InlineData("downstream")]
    [InlineData("inactive")]
    [InlineData("permission")]
    public async Task ChangedBusinessContext_RequiresReviewWithoutSending(string field)
    {
        var contract = await SeedAsync();
        var reminder = await ScheduleAsync(contract);
        switch (field)
        {
            case "endDate": contract.EndDate = contract.EndDate!.Value.AddDays(1); break;
            case "email": contract.Customer.Contacts[0].Email = "other@example.test"; break;
            case "customer": contract.Customer.Name = "Changed legal customer"; break;
            case "cancelled": contract.Status = ContractStatus.Cancelled; break;
            case "downstream": contract.Direction = ContractDirection.Downstream; break;
            case "inactive": contract.Owner!.IsActive = false; break;
            case "permission": permissions.Setup(item => item.HasAsync(It.IsAny<int>(), "crm.contracts.manage", It.IsAny<CancellationToken>())).ReturnsAsync(false); break;
        }
        await db.SaveChangesAsync();
        await Service.SendDueAsync();
        Assert.Equal("NeedsReview", reminder.Status);
        Assert.Equal("CONTEXT_CHANGED", reminder.ErrorCode);
        mail.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AmbiguousSmtpFailure_DoesNotRetryOrClaimSuccessfulDelivery()
    {
        var contract = await SeedAsync();
        var reminder = await ScheduleAsync(contract);
        mail.Setup(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IOException("SMTP disconnected"));
        await Service.SendDueAsync();
        await Service.SendDueAsync();
        Assert.Equal("NeedsReview", reminder.Status);
        Assert.Equal("DELIVERY_UNCERTAIN", reminder.ErrorCode);
        Assert.Null(reminder.SentAt);
        Assert.Empty(db.ContractCareActivities);
        mail.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyClaimedByWorker_IsNeverResent()
    {
        var contract = await SeedAsync();
        var reminder = await ScheduleAsync(contract);
        reminder.Status = "Sending";
        await db.SaveChangesAsync();
        await Service.SendDueAsync();
        mail.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("<script>{{contractNumber}}{{endDate}}</script>")]
    [InlineData("<p onclick='alert(1)'>{{contractNumber}}{{endDate}}</p>")]
    [InlineData("<p>{{contractNumber}}</p>")]
    [InlineData("<p>{{contractNumber}}{{endDate}}{{secret}}</p>")]
    [InlineData("<p>{{contractNumber}}{{endDate}}{{</p>")]
    public void UnsafeOrIncompleteTemplates_AreRejected(string template) =>
        Assert.Throws<ArgumentException>(() => ContractReminderTemplates.Validate(template, true));

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task MovingHtmlBetweenSubjectAndBody_InvalidatesTheApprovedPreview()
    {
        var contract = await SeedAsync();
        var settings = new SiteSettings
        {
            ContractRenewalEmailSubjectTemplate = "NICON",
            ContractRenewalEmailBodyTemplate = "<span></span><p>{{contractNumber}} {{endDate}}</p>",
        };
        db.SiteSettings.Add(settings);
        await db.SaveChangesAsync();
        var userId = contract.OwnerUserId!.Value;
        var preview = await Service.PreviewAsync(contract.Id, new ContractReminderPreviewRequest
        { Kind = "Renewal", ReminderDate = DateOnly.FromDateTime(ContractReminderService.Today) }, userId, false, default);
        settings.ContractRenewalEmailSubjectTemplate = "NICON<span></span>";
        settings.ContractRenewalEmailBodyTemplate = "<p>{{contractNumber}} {{endDate}}</p>";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Service.ScheduleAsync(contract.Id, preview!.PreviewToken, userId, false, default));
        Assert.Empty(db.ContractReminders);
    }

    [Fact]
    public async Task ExpiredAndForeignActorPreviews_CannotSchedule()
    {
        var contract = await SeedAsync();
        var userId = contract.OwnerUserId!.Value;
        var preview = await Service.PreviewAsync(contract.Id, new ContractReminderPreviewRequest
        { Kind = "End", ReminderDate = DateOnly.FromDateTime(ContractReminderService.Today) }, userId, false, default);
        var protector = protection.CreateProtector("ContractReminder.Preview.v1");
        var payload = System.Text.Json.Nodes.JsonNode.Parse(protector.Unprotect(preview!.PreviewToken))!;
        payload["ExpiresAt"] = DateTime.UtcNow.AddMinutes(-1);
        var expired = protector.Protect(payload.ToJsonString());
        await Assert.ThrowsAsync<ArgumentException>(() => Service.ScheduleAsync(contract.Id, expired, userId, false, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.ScheduleAsync(contract.Id, preview.PreviewToken, userId + 1, true, default));
        Assert.Empty(db.ContractReminders);
    }
}
