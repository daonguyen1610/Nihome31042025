using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class CustomerFollowUpReminderServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<INotificationService> notifications = new();
    private readonly CustomerFollowUpReminderService service;

    public CustomerFollowUpReminderServiceTests()
    {
        service = new CustomerFollowUpReminderService(
            db, notifications.Object, NullLogger<CustomerFollowUpReminderService>.Instance);
    }

    [Fact]
    public async Task NotifyDueAsync_RemindsAuthorOnceOnTheFollowUpDate()
    {
        var author = User("0911666001", active: true);
        var customer = Customer("Công ty TNHH Nội thất Thành Đạt", CustomerRelationshipStatus.InProgress);
        var activity = Activity(customer, author, Today, "Gọi lại chốt lịch khảo sát showroom Quận 3");

        await service.NotifyDueAsync(Today, CancellationToken.None);
        await service.NotifyDueAsync(Today, CancellationToken.None);

        notifications.Verify(item => item.NotifyFromTemplateAsync(
            author.Id,
            CustomerFollowUpReminderService.TemplateCode,
            It.Is<IDictionary<string, string>>(data =>
                data["customerName"] == "Công ty TNHH Nội thất Thành Đạt" &&
                data["dueDate"] == "17/11/2026" &&
                data["summary"] == "Gọi lại chốt lịch khảo sát showroom Quận 3"),
            "Customer",
            customer.Id,
            $"/admin/customers?open={customer.Id}",
            "vi"), Times.Once);
        Assert.NotNull((await db.CustomerActivities.AsNoTracking().SingleAsync(item => item.Id == activity.Id))
            .FollowUpNotifiedAt);
    }

    [Fact]
    public async Task NotifyDueAsync_SkipsFutureSuspendedCustomerAndInactiveAuthor()
    {
        var author = User("0911666002", active: true);
        var inactive = User("0911666003", active: false);
        var active = Customer("Gia dụng Tân Phúc", CustomerRelationshipStatus.InProgress);
        var suspended = Customer("Khách đã ngừng giao dịch", CustomerRelationshipStatus.Suspended);
        Activity(active, author, Today.AddDays(1), "Hẹn thăm công trình tuần sau");
        Activity(suspended, author, Today, "Không còn giao dịch");
        Activity(active, inactive, Today, "Nhân viên đã nghỉ việc");

        await service.NotifyDueAsync(Today, CancellationToken.None);

        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddActivityAsync_RejectsPastFollowUpAndKeepsTodayOrLater()
    {
        var hardDelete = HardDeleteTestServices.Create(db, Mock.Of<IProjectDocumentStagingService>());
        var customers = new CustomerService(
            db, NullLogger<CustomerService>.Instance, hardDelete.CrmPlans, hardDelete.Operations);
        var author = User("0911666004", active: true);
        var customer = Customer("Nhà phố Nguyễn Văn An", CustomerRelationshipStatus.Prospect);
        var today = DisplaySettings.VietnamToday();

        await Assert.ThrowsAsync<CustomerOperationException>(() => customers.AddActivityAsync(customer.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Call, Content = "Gọi tư vấn", FollowUpDate = today.AddDays(-1) },
            author.Id, canSeeAll: true));
        var saved = await customers.AddActivityAsync(customer.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Call, Content = "Gọi tư vấn", FollowUpDate = today },
            author.Id, canSeeAll: true);

        Assert.Equal(today, saved!.FollowUpDate);
        Assert.Equal(1, await db.CustomerActivities.CountAsync(item => item.CustomerId == customer.Id));
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

    private Customer Customer(string name, CustomerRelationshipStatus status)
    {
        var customer = new Customer
        {
            Name = name,
            Type = CustomerType.Company,
            SourceCode = "referral",
            RelationshipStatus = status,
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
