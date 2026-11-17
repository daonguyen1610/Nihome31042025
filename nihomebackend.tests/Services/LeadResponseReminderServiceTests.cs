using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class LeadResponseReminderServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<INotificationService> notifications = new();
    private readonly LeadResponseReminderService service;

    public LeadResponseReminderServiceTests()
    {
        service = new LeadResponseReminderService(
            db, notifications.Object, NullLogger<LeadResponseReminderService>.Instance);
    }

    [Fact]
    public async Task NotifyDueAsync_RemindsOwnerOnceWhenOpenLeadReachesItsDate()
    {
        var owner = User("0911555001", active: true);
        var due = Lead("Anh Phúc - nhà phố Thủ Đức", owner, Today, LeadStatus.Contacted);

        await service.NotifyDueAsync(Today, CancellationToken.None);
        await service.NotifyDueAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id,
            LeadResponseReminderService.TemplateCode,
            It.Is<IDictionary<string, string>>(data =>
                data["leadName"] == "Anh Phúc - nhà phố Thủ Đức" && data["dueDate"] == "17/11/2026"),
            "Lead",
            due.Id,
            $"/admin/leads/{due.Id}",
            "vi"), Times.Once);
        Assert.NotNull((await db.Leads.AsNoTracking().SingleAsync(item => item.Id == due.Id)).ResponseDueNotifiedAt);
    }

    [Fact]
    public async Task NotifyDueAsync_SkipsFutureClosedAndInactiveOwnerLeads()
    {
        var owner = User("0911555002", active: true);
        var inactive = User("0911555003", active: false);
        Lead("Chưa đến hạn", owner, Today.AddDays(1), LeadStatus.New);
        Lead("Đã chuyển đổi", owner, Today.AddDays(-2), LeadStatus.Converted);
        Lead("Không quan tâm", owner, Today.AddDays(-2), LeadStatus.NotInterested);
        Lead("Người phụ trách đã nghỉ", inactive, Today, LeadStatus.New);

        await service.NotifyDueAsync(Today, CancellationToken.None);

        notifications.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(LeadStatus.New, -1, true)]
    [InlineData(LeadStatus.Interested, -1, true)]
    [InlineData(LeadStatus.Contacted, 0, false)]
    [InlineData(LeadStatus.Converted, -5, false)]
    [InlineData(LeadStatus.Junk, -5, false)]
    public void IsOverdue_OnlyCountsOpenLeadsPastTheirDate(LeadStatus status, int offsetDays, bool expected)
    {
        Assert.Equal(expected, LeadResponseDeadline.IsOverdue(status, Today.AddDays(offsetDays), Today));
        Assert.False(LeadResponseDeadline.IsOverdue(status, null, Today));
    }

    public void Dispose() => db.Dispose();

    private ApplicationUser User(string phone, bool active)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            Email = $"{phone}@nihome.test",
            FullName = $"Kinh doanh {phone}",
            PasswordHash = "test",
            IsActive = active,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private Lead Lead(string name, ApplicationUser owner, DateOnly dueDate, LeadStatus status)
    {
        var lead = new Lead
        {
            Name = name,
            SourceCode = "referral",
            Status = status,
            OwnerUserId = owner.Id,
            ResponseDueDate = dueDate,
        };
        db.Leads.Add(lead);
        db.SaveChanges();
        return lead;
    }
}
