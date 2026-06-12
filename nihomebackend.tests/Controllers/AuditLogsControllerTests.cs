using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Controllers;

public class AuditLogsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuditLogsController _sut;
    private readonly AuditLogQueue _queue;

    public AuditLogsControllerTests()
    {
        _db = DbContextFactory.Create();
        _queue = new AuditLogQueue();
        var logger = new AuditLogger(
            _queue,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            NullLogger<AuditLogger>.Instance);
        _sut = new AuditLogsController(_db, logger);
    }

    public void Dispose() => _db.Dispose();

    private AuditLog Make(
        string action = "test.action",
        string status = AuditStatus.Success,
        string resourceType = "Resource",
        string? resourceId = null,
        string? actorPhone = null,
        string? ip = null,
        string? correlationId = null,
        DateTime? createdAt = null)
        => new()
        {
            AuditId = Guid.NewGuid().ToString("N"),
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Message = "msg",
            ActorType = AuditActorType.User,
            ActorPhone = actorPhone,
            SourceSystem = "nihomebackend",
            Channel = "http",
            IpAddress = ip,
            Status = status,
            CorrelationId = correlationId,
        };

    private void SeedSettings(int retentionMinutes = 1440)
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "Nihome",
            PrimaryEmail = "n@n.vn",
            NotificationEmail = "n@n.vn",
            AuditLogRetentionMinutes = retentionMinutes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Get_NoData_ReturnsEmptyPage()
    {
        var result = await _sut.Get(null, null, null, null, null, null, null, null, null, 1, 50);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<AuditLogsController.AuditLogPage>(ok.Value);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
        Assert.Empty(page.Actions);
    }

    [Fact]
    public async Task Get_ReturnsItemsSortedByCreatedAtDescending()
    {
        _db.AuditLogs.AddRange(
            Make(action: "a", createdAt: DateTime.UtcNow.AddMinutes(-30)),
            Make(action: "b", createdAt: DateTime.UtcNow.AddMinutes(-10)),
            Make(action: "c", createdAt: DateTime.UtcNow.AddMinutes(-20)));
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, null, null, null, null, null, null, null, 1, 50);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(3, page.Total);
        Assert.Equal(new[] { "b", "c", "a" }, page.Items.Select(i => i.Action).ToArray());
        Assert.Contains("a", page.Actions);
        Assert.Contains("b", page.Actions);
        Assert.Contains("c", page.Actions);
    }

    [Fact]
    public async Task Get_FilterByAction_ReturnsMatching()
    {
        _db.AuditLogs.AddRange(
            Make(action: "user.login"),
            Make(action: "user.logout"));
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, "user.login", null, null, null, null, null, null, 1, 50);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(1, page.Total);
        Assert.Equal("user.login", page.Items[0].Action);
    }

    [Fact]
    public async Task Get_FilterByStatus_ReturnsMatching()
    {
        _db.AuditLogs.AddRange(
            Make(status: AuditStatus.Success),
            Make(status: AuditStatus.Failure),
            Make(status: AuditStatus.Failure));
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, null, null, null, AuditStatus.Failure, null, null, null, 1, 50);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, i => Assert.Equal(AuditStatus.Failure, i.Status));
    }

    [Fact]
    public async Task Get_FilterByResourceTypeAndId_ReturnsMatching()
    {
        _db.AuditLogs.AddRange(
            Make(resourceType: "Process", resourceId: "1"),
            Make(resourceType: "Process", resourceId: "2"),
            Make(resourceType: "News", resourceId: "1"));
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, null, null, null, null, "Process", "1", null, 1, 50);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(1, page.Total);
        Assert.Equal("Process", page.Items[0].ResourceType);
        Assert.Equal("1", page.Items[0].ResourceId);
    }

    [Fact]
    public async Task Get_FilterByCorrelationId_ReturnsMatching()
    {
        _db.AuditLogs.AddRange(
            Make(correlationId: "corr-1"),
            Make(correlationId: "corr-2"));
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, null, null, null, null, null, null, "corr-1", 1, 50);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(1, page.Total);
        Assert.Equal("corr-1", page.Items[0].CorrelationId);
    }

    [Fact]
    public async Task Get_FilterByActorPhoneAndIp_ReturnsMatching()
    {
        _db.AuditLogs.AddRange(
            Make(actorPhone: "0335240370", ip: "10.0.0.5"),
            Make(actorPhone: "0335240371", ip: "10.0.0.6"));
        await _db.SaveChangesAsync();

        var byPhone = await _sut.Get(null, null, null, "0335240370", null, null, null, null, null, 1, 50);
        var p1 = (AuditLogsController.AuditLogPage)((OkObjectResult)byPhone.Result!).Value!;
        Assert.Equal(1, p1.Total);

        var byIp = await _sut.Get(null, null, null, null, "10.0.0.6", null, null, null, null, 1, 50);
        var p2 = (AuditLogsController.AuditLogPage)((OkObjectResult)byIp.Result!).Value!;
        Assert.Equal(1, p2.Total);
    }

    [Fact]
    public async Task Get_PageSizeClampedTo200()
    {
        for (var i = 0; i < 5; i++) _db.AuditLogs.Add(Make());
        await _db.SaveChangesAsync();

        var result = await _sut.Get(null, null, null, null, null, null, null, null, null, 1, 99999);
        var page = (AuditLogsController.AuditLogPage)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(200, page.PageSize);
    }

    [Fact]
    public async Task GetConfig_NoSettings_ReturnsZero()
    {
        var result = await _sut.GetConfig();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var cfg = Assert.IsType<AuditLogsController.AuditConfigDto>(ok.Value);
        Assert.Equal(0, cfg.RetentionMinutes);
    }

    [Fact]
    public async Task GetConfig_ReturnsConfiguredMinutes()
    {
        SeedSettings(retentionMinutes: 7200);
        var result = await _sut.GetConfig();
        var cfg = (AuditLogsController.AuditConfigDto)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(7200, cfg.RetentionMinutes);
    }

    [Fact]
    public async Task UpdateConfig_ValidValue_PersistsAndReturns()
    {
        SeedSettings();

        var result = await _sut.UpdateConfig(new AuditLogsController.AuditConfigDto { RetentionMinutes = 60 });
        var cfg = (AuditLogsController.AuditConfigDto)((OkObjectResult)result.Result!).Value!;

        Assert.Equal(60, cfg.RetentionMinutes);
        var persisted = _db.SiteSettings.Single();
        Assert.Equal(60, persisted.AuditLogRetentionMinutes);
    }

    [Fact]
    public async Task UpdateConfig_NegativeValue_ReturnsBadRequest()
    {
        SeedSettings();
        var result = await _sut.UpdateConfig(new AuditLogsController.AuditConfigDto { RetentionMinutes = -5 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
