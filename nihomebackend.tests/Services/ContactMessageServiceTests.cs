using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ContactMessageServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ContactMessageService _sut;

    public ContactMessageServiceTests()
    {
        _db = DbContextFactory.Create();
        _emailServiceMock = new Mock<IEmailService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _sut = new ContactMessageService(
            _db,
            _emailServiceMock.Object,
            _notificationServiceMock.Object,
            Mock.Of<ILogger<ContactMessageService>>());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SubmitAsync_UsesCodeBaselineForAdminEmail()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            NotificationEmail = "admin@nicon.test"
        });
        await _db.SaveChangesAsync();

        await _sut.SubmitAsync(new SubmitContactRequest
        {
            Name = "Nguyễn Văn An",
            Email = "an@nicon.test",
            Subject = "Tư vấn thiết kế nhà máy",
            Message = "Cần tư vấn thiết kế"
        });

        _emailServiceMock.Verify(email => email.SendEmailAsync("admin@nicon.test",
            It.IsAny<string>(), It.Is<string>(body =>
                body.Contains("font-family:Arial,sans-serif") && body.Contains("Nguyễn Văn An") &&
                body.Contains("Cần tư vấn thiết kế"))), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_CreatesAdminNotification_WhenContactSaved()
    {
        var result = await _sut.SubmitAsync(new SubmitContactRequest
        {
            Name = "Nam",
            Email = "nam@test.com",
            Subject = "Tư vấn",
            Message = "Xin chào"
        });

        Assert.Equal("Nam", result.Name);
        _notificationServiceMock.Verify(
            n => n.CreateForAdminsAsync(
                "Contact",
                It.Is<string>(title => title.Contains("Nam")),
                "Tư vấn",
                "/admin/contacts"),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_DoesNotFail_WhenNotificationCreationFails()
    {
        _notificationServiceMock
            .Setup(n => n.CreateForAdminsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("notification failed"));

        var result = await _sut.SubmitAsync(new SubmitContactRequest
        {
            Name = "Nam",
            Email = "nam@test.com",
            Subject = "Tư vấn",
            Message = "Xin chào"
        });

        Assert.Equal("Nam", result.Name);
    }
}
