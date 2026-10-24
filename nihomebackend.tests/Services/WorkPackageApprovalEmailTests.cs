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

public sealed class WorkPackageApprovalEmailTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IPermissionService> permissions = new();
    private readonly Mock<IEmailService> email = new();
    private readonly WorkPackageApprovalEmailService service;
    private readonly ApplicationUser author = new() { Email = "author@example.test", FullName = "Mai <Anh>", PhoneNumber = "0901234567" };
    private readonly ApplicationUser lead = new() { Email = "lead@example.test", FullName = "Trần An", PhoneNumber = "0901234568" };
    private readonly DepartmentWorkPackage package;

    public WorkPackageApprovalEmailTests()
    {
        db.AddRange(author, lead);
        db.SaveChanges();
        package = new() { Code = "WP-AN-PHU-01", Name = "Báo giá MEP <An Phú>", LeadUserId = lead.Id, CreatedByUserId = author.Id };
        db.Add(package);
        db.SaveChanges();
        permissions.Setup(item => item.HasAsync(lead.Id, "work.packages.verify", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        service = new(db, new EphemeralDataProtectionProvider(), permissions.Object, email.Object, NullLogger<WorkPackageApprovalEmailService>.Instance);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("inactive")]
    [InlineData("email")]
    [InlineData("permission")]
    [InlineData("blank")]
    public async Task Preview_RejectsInvalidApprover(string reason)
    {
        if (reason == "self") package.LeadUserId = author.Id;
        if (reason == "inactive") lead.IsActive = false;
        if (reason == "email") lead.Email = "not-an-email";
        if (reason == "blank") lead.Email = " ";
        if (reason == "permission") permissions.Setup(item => item.HasAsync(lead.Id, "work.packages.verify", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() => Preview());
        Assert.Empty(await db.WorkPackageApprovalEmails.ToListAsync());
    }

    [Fact]
    public async Task Preview_EncodesBusinessValues_AndQueueFreezesExactContent()
    {
        var preview = await Preview();
        Assert.Contains("&lt;An Ph", preview.Body);
        Assert.DoesNotContain("<An Phú>", preview.Body);
        Assert.DoesNotContain("{{", preview.Body);
        await Queue(preview.PreviewToken);
        await db.SaveChangesAsync();
        var queued = Assert.Single(await db.WorkPackageApprovalEmails.ToListAsync());
        Assert.Equal(preview.Subject, queued.Subject);
        Assert.Equal(preview.Body, queued.Body);
        Assert.Equal(lead.Email, queued.ToEmail);
        email.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("recipient")]
    [InlineData("record")]
    [InlineData("caller")]
    [InlineData("token")]
    public async Task Queue_RejectsChangedOrForgedPreview(string change)
    {
        var preview = await Preview();
        if (change == "template") { db.SiteSettings.Add(new SiteSettings { WorkPackageApprovalEmailSubjectTemplate = "Changed {{workPackageCode}}" }); }
        if (change == "recipient") lead.Email = "new-lead@example.test";
        if (change == "record") package.Name = "Cập nhật phạm vi MEP";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() => service.QueueAsync(package, "InputPriceOffer", 10, "BG-AN-PHU-01",
            author.Id, "version", change == "caller" ? lead.Id : author.Id, change == "token" ? "forged" : preview.PreviewToken, default));
        Assert.Empty(await db.WorkPackageApprovalEmails.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_SendsOnce_AndNeverAutomaticallyRetriesUncertainDelivery(bool fail)
    {
        var preview = await Preview();
        await Queue(preview.PreviewToken);
        db.ProcurementInputPriceOffers.Add(new() { Id = 10, WorkPackageId = package.Id, Status = InputPriceOfferStatus.Submitted, SubmittedAt = db.WorkPackageApprovalEmails.Local.Single().CreatedAt });
        await db.SaveChangesAsync();
        if (fail) email.Setup(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IOException("SMTP disconnected"));
        await service.SendPendingAsync(default);
        await service.SendPendingAsync(default);
        email.Verify(item => item.SendEmailAsync(lead.Email, preview.Subject, preview.Body), Times.Once);
        Assert.Equal(fail ? "NeedsReview" : "Sent", (await db.WorkPackageApprovalEmails.SingleAsync()).Status);
    }

    [Fact]
    public async Task Worker_DoesNotSendAfterLeadChanges()
    {
        await Queue((await Preview()).PreviewToken);
        db.ProcurementInputPriceOffers.Add(new() { Id = 10, WorkPackageId = package.Id, Status = InputPriceOfferStatus.Submitted, SubmittedAt = db.WorkPackageApprovalEmails.Local.Single().CreatedAt });
        lead.Email = "changed@example.test";
        await db.SaveChangesAsync();
        await service.SendPendingAsync(default);
        Assert.Equal("NeedsReview", (await db.WorkPackageApprovalEmails.SingleAsync()).Status);
        email.Verify(item => item.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<p>{{unknown}}</p>")]
    [InlineData("<p>{{workPackageCode}}</p>")]
    [InlineData("<p onclick='alert(1)'>{{workPackageCode}} {{recordReference}}</p>")]
    public void Template_RejectsUnsafeOrIncompleteHtml(string html) =>
        Assert.Throws<ArgumentException>(() => WorkPackageApprovalEmailTemplates.Validate(html, true));

    [Fact]
    public void DefaultTemplate_IsAcceptedByServerValidator()
    {
        Assert.Equal(WorkPackageApprovalEmailTemplates.DefaultSubject, WorkPackageApprovalEmailTemplates.Validate(WorkPackageApprovalEmailTemplates.DefaultSubject, false));
        Assert.Equal(WorkPackageApprovalEmailTemplates.DefaultBody, WorkPackageApprovalEmailTemplates.Validate(WorkPackageApprovalEmailTemplates.DefaultBody, true));
    }

    [Fact]
    public async Task DefaultPreview_PrefillsLead_WithNoCc()
    {
        var preview = await Preview();
        Assert.Equal(lead.Email, preview.ToEmail);
        Assert.Empty(preview.CcEmails!);
    }

    [Fact]
    public async Task CustomRecipients_AreNormalizedFrozenAndDeliveredWithoutChangingLead()
    {
        var preview = await service.PreviewAsync(package, "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id,
            "version", author.Id, default, new WorkPackageApprovalRecipientsRequest
            {
                ToEmail = " delegate@example.test ",
                CcEmails = [" cm@example.test ", "CM@example.test", "delegate@example.test", "qs@example.test"],
            });
        Assert.Equal("delegate@example.test", preview.ToEmail);
        Assert.Equal(new[] { "cm@example.test", "qs@example.test" }, preview.CcEmails);
        await Queue(preview.PreviewToken);
        var queued = db.WorkPackageApprovalEmails.Local.Single();
        Assert.Equal(lead.Email, queued.LeadEmail);
        Assert.Equal(lead.Id, queued.RecipientUserId);
        db.ProcurementInputPriceOffers.Add(new() { Id = 10, WorkPackageId = package.Id, Status = InputPriceOfferStatus.Submitted, SubmittedAt = queued.CreatedAt });
        await db.SaveChangesAsync();
        email.Setup(item => item.SendEmailWithCcAsync(preview.ToEmail, It.IsAny<IReadOnlyCollection<string>>(), preview.Subject, preview.Body, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await service.SendPendingAsync(default);
        email.Verify(item => item.SendEmailWithCcAsync("delegate@example.test",
            It.Is<IReadOnlyCollection<string>>(cc => cc.SequenceEqual(new[] { "cm@example.test", "qs@example.test" })),
            preview.Subject, preview.Body, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Sent", queued.Status);
        Assert.Equal(lead.Id, package.LeadUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-email")]
    [InlineData("a@example.test,b@example.test")]
    [InlineData("a@example.test\r\nBcc: hidden@example.test")]
    public async Task Preview_RejectsInvalidTo(string toEmail)
    {
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() => service.PreviewAsync(package,
            "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id, "version", author.Id, default, new() { ToEmail = toEmail }));
    }

    [Fact]
    public async Task Preview_RejectsInvalidOrExcessiveCc()
    {
        foreach (var cc in new List<string>[] { [""], ["not-email"], Enumerable.Repeat("cc@example.test", 21).ToList() })
            await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() => service.PreviewAsync(package,
                "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id, "version", author.Id, default, new() { CcEmails = cc }));
        Assert.Empty(await db.WorkPackageApprovalEmails.ToListAsync());
    }

    [Fact]
    public async Task UnicodeCc_AtRecipientLimit_FitsPersistedSnapshotAndSubmitToken()
    {
        var recipients = Enumerable.Range(1, 20).Select(index => $"{new string('安', 60)}{index}@example.test").ToList();
        var preview = await service.PreviewAsync(package, "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id,
            "version", author.Id, default, new() { CcEmails = recipients });
        Assert.InRange(preview.PreviewToken.Length, 1, 32768);
        await Queue(preview.PreviewToken);
        var queued = db.WorkPackageApprovalEmails.Local.Single();
        Assert.InRange(queued.CcEmailsJson.Length, 4001, 20000);
    }

    private Task<WorkPackageApprovalPreview> Preview() => service.PreviewAsync(package, "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id, "version", author.Id, default);
    private Task Queue(string token) => service.QueueAsync(package, "InputPriceOffer", 10, "BG-AN-PHU-01", author.Id, "version", author.Id, token, default);
    public void Dispose() => db.Dispose();
}
