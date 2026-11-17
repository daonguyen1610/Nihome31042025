using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class CrmDueReminderServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<INotificationService> notifications = new();
    private readonly CrmDueReminderService service;

    public CrmDueReminderServiceTests()
    {
        service = new CrmDueReminderService(db, notifications.Object, NullLogger<CrmDueReminderService>.Instance);
    }

    // ---------------- Lead response deadline ----------------

    [Fact]
    public async Task LeadResponses_RemindOwnerOnceWithoutTouchingTheLead()
    {
        var owner = User("0911555001", active: true);
        var lead = Lead("Anh Phúc - nhà phố Thủ Đức", owner, Today, LeadStatus.Contacted);
        var rowVersion = lead.RowVersion.ToArray();

        await service.NotifyLeadResponsesAsync(Today, CancellationToken.None);
        await service.NotifyLeadResponsesAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id,
            CrmDueReminderService.LeadTemplateCode,
            It.Is<IDictionary<string, string>>(data =>
                data["leadName"] == "Anh Phúc - nhà phố Thủ Đức" && data["dueDate"] == "17/11/2026"),
            "Lead", lead.Id, $"/admin/leads/{lead.Id}", "vi"), Times.Once);
        Assert.Equal(rowVersion, (await db.Leads.AsNoTracking().SingleAsync(item => item.Id == lead.Id)).RowVersion);
        Assert.Equal(1, await db.CrmDueReminders.CountAsync());
    }

    [Fact]
    public async Task LeadResponses_RemindAgainWhenTheDueDateChanges()
    {
        var owner = User("0911555002", active: true);
        var lead = Lead("Chị Lan - căn hộ Quận 7", owner, Today, LeadStatus.New);
        db.CrmDueReminders.Add(new CrmDueReminder
        {
            Kind = CrmDueReminderKinds.LeadResponse,
            EntityId = lead.Id,
            DueDate = Today.AddDays(-3),
        });
        db.SaveChanges();

        await service.NotifyLeadResponsesAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id, CrmDueReminderService.LeadTemplateCode, It.IsAny<IDictionary<string, string>>(),
            "Lead", lead.Id, It.IsAny<string>(), "vi"), Times.Once);
    }

    [Fact]
    public async Task LeadResponses_SkipFutureClosedAndInactiveOwnerLeads()
    {
        var owner = User("0911555003", active: true);
        var inactive = User("0911555004", active: false);
        Lead("Chưa đến hạn", owner, Today.AddDays(1), LeadStatus.New);
        Lead("Đã chuyển đổi", owner, Today.AddDays(-2), LeadStatus.Converted);
        Lead("Không quan tâm", owner, Today.AddDays(-2), LeadStatus.NotInterested);
        Lead("Người phụ trách đã nghỉ", inactive, Today, LeadStatus.New);

        await service.NotifyLeadResponsesAsync(Today, CancellationToken.None);

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

    // ---------------- Customer follow-ups ----------------

    [Fact]
    public async Task CustomerFollowUps_RemindTheCurrentOwnerOnce()
    {
        var author = User("0911666001", active: true);
        var owner = User("0911666002", active: true);
        var customer = Customer("Công ty TNHH Nội thất Thành Đạt", CustomerRelationshipStatus.InProgress, owner);
        var activity = Activity(customer, author, Today, "Gọi lại chốt lịch khảo sát showroom Quận 3");

        await service.NotifyCustomerFollowUpsAsync(Today, CancellationToken.None);
        await service.NotifyCustomerFollowUpsAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id,
            CrmDueReminderService.CustomerTemplateCode,
            It.Is<IDictionary<string, string>>(data =>
                data["customerName"] == "Công ty TNHH Nội thất Thành Đạt" &&
                data["dueDate"] == "17/11/2026" &&
                data["summary"] == "Gọi lại chốt lịch khảo sát showroom Quận 3"),
            "Customer", customer.Id, $"/admin/customers?open={customer.Id}", "vi"), Times.Once);
        notifications.VerifyNoOtherCalls();
        Assert.True(await db.CrmDueReminders.AnyAsync(item =>
            item.Kind == CrmDueReminderKinds.CustomerFollowUp && item.EntityId == activity.Id));
    }

    [Fact]
    public async Task CustomerFollowUps_FallBackToAuthorWithoutAnActiveOwner()
    {
        var author = User("0911666003", active: true);
        var inactiveOwner = User("0911666004", active: false);
        var customer = Customer("Gia dụng Tân Phúc", CustomerRelationshipStatus.Prospect, inactiveOwner);
        Activity(customer, author, Today, "Hẹn thăm công trình");

        await service.NotifyCustomerFollowUpsAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            author.Id, CrmDueReminderService.CustomerTemplateCode, It.IsAny<IDictionary<string, string>>(),
            "Customer", customer.Id, It.IsAny<string>(), "vi"), Times.Once);
    }

    [Fact]
    public async Task CustomerFollowUps_SkipFutureAndSuspendedCustomers()
    {
        var author = User("0911666005", active: true);
        var active = Customer("Nhà phố Nguyễn Văn An", CustomerRelationshipStatus.InProgress, author);
        var suspended = Customer("Khách đã ngừng giao dịch", CustomerRelationshipStatus.Suspended, author);
        Activity(active, author, Today.AddDays(1), "Hẹn thăm công trình tuần sau");
        Activity(suspended, author, Today, "Không còn giao dịch");

        await service.NotifyCustomerFollowUpsAsync(Today, CancellationToken.None);

        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddActivityAsync_RejectsPastFollowUpAndKeepsTodayOrLater()
    {
        var hardDelete = HardDeleteTestServices.Create(db, Mock.Of<IProjectDocumentStagingService>());
        var customers = new CustomerService(
            db, NullLogger<CustomerService>.Instance, hardDelete.CrmPlans, hardDelete.Operations);
        var author = User("0911666006", active: true);
        var customer = Customer("Chị Hạnh - nhà phố Gò Vấp", CustomerRelationshipStatus.Prospect, author);
        var today = await DisplaySettings.TodayAsync(db, CancellationToken.None);

        await Assert.ThrowsAsync<CustomerOperationException>(() => customers.AddActivityAsync(customer.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Call, Content = "Gọi tư vấn", FollowUpDate = today.AddDays(-1) },
            author.Id, canSeeAll: true));
        var saved = await customers.AddActivityAsync(customer.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Call, Content = "Gọi tư vấn", FollowUpDate = today },
            author.Id, canSeeAll: true);

        Assert.Equal(today, saved!.FollowUpDate);
        Assert.Equal(1, await db.CustomerActivities.CountAsync(item => item.CustomerId == customer.Id));
    }

    // ---------------- Tender deadlines, dossier items and quote expiry ----------------

    private static readonly TimeZoneInfo Zone = DisplaySettings.VietnamTimeZone;

    /// <summary>10:00 local time on the business day <paramref name="offsetDays"/> from today, as UTC.</summary>
    private static DateTime LocalMorningUtc(int offsetDays) =>
        TimeZoneInfo.ConvertTimeToUtc(DisplaySettings.TodayIn(Zone).AddDays(offsetDays).AddHours(10), Zone);

    [Theory]
    [InlineData(3, 0, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(7, -1, false)]
    public void IsInReminderWindow_StartsNDaysAheadAndEndsOnTheDueDate(int daysBefore, int daysUntilDue, bool expected)
    {
        Assert.Equal(expected, CrmDueReminderService.IsInReminderWindow(Today.AddDays(daysUntilDue), daysBefore, Today));
    }

    [Fact]
    public async Task TenderDeadlines_RemindPreparerOnceInsideTheirOwnWindow()
    {
        var preparer = User("0911777001", active: true);
        var customer = Customer("Công ty CP Nhà máy An Phú", CustomerRelationshipStatus.InProgress, preparer);
        var dueSoon = Tender("TD-AP-01", "Nhà máy may An Phú – Quế Võ", customer, preparer, preparer, LocalMorningUtc(2), 3);
        Tender("TD-AP-02", "Chưa tới cửa sổ nhắc", customer, preparer, preparer, LocalMorningUtc(5), 3);
        Tender("TD-AP-03", "Đã nộp", customer, preparer, preparer, LocalMorningUtc(1), 3, TenderStatus.Submitted);
        var onTheDay = Tender("TD-AP-04", "Nhắc đúng ngày", customer, preparer, preparer, LocalMorningUtc(0), 0);

        await service.NotifyTenderDeadlinesAsync(Zone, CancellationToken.None);
        await service.NotifyTenderDeadlinesAsync(Zone, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            preparer.Id, CrmDueReminderService.TenderTemplateCode,
            It.Is<IDictionary<string, string>>(data => data["code"] == "TD-AP-01" && data["name"] == "Nhà máy may An Phú – Quế Võ"),
            "Tender", dueSoon.Id, $"/admin/tenders/{dueSoon.Id}", "vi"), Times.Once);
        notifications.Verify(item => item.NotifyFromTemplateAsync(
            preparer.Id, CrmDueReminderService.TenderTemplateCode, It.IsAny<IDictionary<string, string>>(),
            "Tender", onTheDay.Id, It.IsAny<string>(), "vi"), Times.Once);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TenderDeadlines_FallBackToInvitationReceiverWhenPreparerLeft()
    {
        var receiver = User("0911777002", active: true);
        var formerPreparer = User("0911777003", active: false);
        var customer = Customer("Showroom Thành Đạt", CustomerRelationshipStatus.Prospect, receiver);
        var tender = Tender("TD-TD-01", "Showroom Quận 3", customer, formerPreparer, receiver, LocalMorningUtc(1), 3);

        await service.NotifyTenderDeadlinesAsync(Zone, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            receiver.Id, CrmDueReminderService.TenderTemplateCode, It.IsAny<IDictionary<string, string>>(),
            "Tender", tender.Id, It.IsAny<string>(), "vi"), Times.Once);
    }

    [Fact]
    public async Task TenderChecklistItems_RemindOwnerOrPreparerOnUnfinishedItemsDueToday()
    {
        var preparer = User("0911777004", active: true);
        var owner = User("0911777005", active: true);
        var customer = Customer("Gia dụng Tân Phúc", CustomerRelationshipStatus.InProgress, preparer);
        var tender = Tender("TD-TP-01", "Cải tạo trụ sở Tân Phúc", customer, preparer, preparer, LocalMorningUtc(10), 3);
        var owned = ChecklistItem(tender, "Hồ sơ năng lực", owner, LocalMorningUtc(0), TenderChecklistItemStatus.Preparing);
        var unowned = ChecklistItem(tender, "Bảo lãnh dự thầu", null, LocalMorningUtc(0), TenderChecklistItemStatus.NotStarted);
        ChecklistItem(tender, "Đã xong", owner, LocalMorningUtc(0), TenderChecklistItemStatus.Done);
        ChecklistItem(tender, "Chưa tới hạn", owner, LocalMorningUtc(2), TenderChecklistItemStatus.NotStarted);

        await service.NotifyTenderChecklistItemsAsync(Zone, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id, CrmDueReminderService.TenderChecklistTemplateCode,
            It.Is<IDictionary<string, string>>(data => data["item"] == "Hồ sơ năng lực" && data["code"] == "TD-TP-01"),
            "Tender", tender.Id, It.IsAny<string>(), "vi"), Times.Once);
        notifications.Verify(item => item.NotifyFromTemplateAsync(
            preparer.Id, CrmDueReminderService.TenderChecklistTemplateCode,
            It.Is<IDictionary<string, string>>(data => data["item"] == "Bảo lãnh dự thầu"),
            "Tender", tender.Id, It.IsAny<string>(), "vi"), Times.Once);
        notifications.VerifyNoOtherCalls();
        Assert.Equal(2, await db.CrmDueReminders.CountAsync(item => item.Kind == CrmDueReminderKinds.TenderChecklistItem &&
            (item.EntityId == owned.Id || item.EntityId == unowned.Id)));
    }

    [Fact]
    public async Task QuoteExpiries_RemindOwnerOfApprovedOrSentQuotesInsideTheirWindow()
    {
        var owner = User("0911777006", active: true);
        var sent = Quote("BG-2026-0101", owner, QuoteStatus.SentToCustomer, LocalMorningUtc(2), 3);
        Quote("BG-2026-0102", owner, QuoteStatus.Draft, LocalMorningUtc(1), 3);
        Quote("BG-2026-0103", owner, QuoteStatus.Approved, LocalMorningUtc(2), 0);

        await service.NotifyQuoteExpiriesAsync(Zone, CancellationToken.None);
        await service.NotifyQuoteExpiriesAsync(Zone, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            owner.Id, CrmDueReminderService.QuoteTemplateCode,
            It.Is<IDictionary<string, string>>(data => data["code"] == "BG-2026-0101"),
            "Quote", sent.Id, $"/admin/quotes/{sent.Id}", "vi"), Times.Once);
        notifications.VerifyNoOtherCalls();
    }

    private Tender Tender(string code, string name, Customer customer, ApplicationUser preparer, ApplicationUser receiver,
        DateTime deadlineUtc, int reminderDays, TenderStatus status = TenderStatus.Preparing)
    {
        var tender = new Tender
        {
            Code = code,
            Name = name,
            CustomerId = customer.Id,
            InvitationReceivedByUserId = receiver.Id,
            InvitationSource = "Email chủ đầu tư",
            PreparerUserId = preparer.Id,
            SubmissionDeadline = deadlineUtc,
            DeadlineReminderDays = reminderDays,
            Status = status,
        };
        db.Tenders.Add(tender);
        db.SaveChanges();
        return tender;
    }

    private TenderChecklistItem ChecklistItem(Tender tender, string title, ApplicationUser? owner, DateTime deadlineUtc,
        TenderChecklistItemStatus status)
    {
        var item = new TenderChecklistItem
        {
            TenderId = tender.Id,
            Title = title,
            OwnerUserId = owner?.Id,
            InternalDeadline = deadlineUtc,
            Status = status,
        };
        db.TenderChecklistItems.Add(item);
        db.SaveChanges();
        return item;
    }

    private Quote Quote(string code, ApplicationUser owner, QuoteStatus status, DateTime validUntilUtc, int reminderDays)
    {
        var quote = new Quote
        {
            Code = code,
            OpportunityId = 1,
            OwnerUserId = owner.Id,
            Status = status,
            ValidUntil = validUntilUtc,
            ExpiryReminderDays = reminderDays,
        };
        db.Quotes.Add(quote);
        db.SaveChanges();
        return quote;
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

    private Customer Customer(string name, CustomerRelationshipStatus status, ApplicationUser owner)
    {
        var customer = new Customer
        {
            Name = name,
            Type = CustomerType.Company,
            SourceCode = "referral",
            RelationshipStatus = status,
            OwnerUserId = owner.Id,
        };
        db.Customers.Add(customer);
        db.SaveChanges();
        return customer;
    }

    private CustomerActivity Activity(Customer customer, ApplicationUser author, DateOnly followUp, string content)
    {
        var activity = new CustomerActivity
        {
            CustomerId = customer.Id,
            Type = CustomerActivityType.Call,
            Content = content,
            CreatedByUserId = author.Id,
            FollowUpDate = followUp,
        };
        db.CustomerActivities.Add(activity);
        db.SaveChanges();
        return activity;
    }
}
