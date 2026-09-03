using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Sanity coverage for the shipped notification-template catalogue.
/// </summary>
public class NotificationTemplateSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public NotificationTemplateSeederTests()
    {
        _db = DbContextFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_LoadsAllGd2Templates()
    {
        NotificationTemplateSeeder.Seed(_db);

        var codes = _db.NotificationTemplates.Select(t => t.Code).ToList();
        foreach (var code in new[]
        {
            "lead.assigned",
            "quote.submitted-for-approval",
            "quote.approved",
            "quote.rejected",
            "contract.activated",
            "design.revision.created",
            "permit.expiring-soon",
        })
        {
            Assert.Contains(code, codes);
        }
    }

    [Fact]
    public void Seed_ChannelIsRespectedFromJson()
    {
        NotificationTemplateSeeder.Seed(_db);

        Assert.Equal(NotificationChannel.InApp,
            _db.NotificationTemplates.Single(t => t.Code == "lead.assigned").Channel);
        Assert.Equal(NotificationChannel.Both,
            _db.NotificationTemplates.Single(t => t.Code == "quote.submitted-for-approval").Channel);
        Assert.Equal(NotificationChannel.Both,
            _db.NotificationTemplates.Single(t => t.Code == "permit.expiring-soon").Channel);
    }

    [Fact]
    public void Seed_KeysFollowConvention()
    {
        NotificationTemplateSeeder.Seed(_db);

        var t = _db.NotificationTemplates.Single(x => x.Code == "quote.approved");
        Assert.Equal("notification.quote.approved.title", t.TitleKey);
        Assert.Equal("notification.quote.approved.body", t.BodyKey);
    }

    [Fact]
    public void Seed_IsIdempotent()
    {
        NotificationTemplateSeeder.Seed(_db);
        var afterFirst = _db.NotificationTemplates.Count();

        NotificationTemplateSeeder.Seed(_db);
        var afterSecond = _db.NotificationTemplates.Count();

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Seed_DoesNotOverwriteAdminEdits()
    {
        NotificationTemplateSeeder.Seed(_db);
        var t = _db.NotificationTemplates.Single(x => x.Code == "lead.assigned");
        t.Channel = NotificationChannel.Both;
        t.IsActive = false;
        _db.SaveChanges();

        NotificationTemplateSeeder.Seed(_db);

        var stillEdited = _db.NotificationTemplates.Single(x => x.Code == "lead.assigned");
        Assert.Equal(NotificationChannel.Both, stillEdited.Channel);
        Assert.False(stillEdited.IsActive);
    }

    [Fact]
    public void Seed_PartialStateAddsMissingTemplatesAndKeepsStableExistingId()
    {
        _db.NotificationTemplates.Add(new NotificationTemplate
        {
            Code = "lead.assigned",
            Module = "custom-module",
            TitleKey = "custom.title",
            BodyKey = "custom.body",
            Channel = NotificationChannel.Email,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
        var existingId = _db.NotificationTemplates.Single().Id;

        NotificationTemplateSeeder.Seed(_db);
        var count = _db.NotificationTemplates.Count();
        NotificationTemplateSeeder.Seed(_db);

        var existing = _db.NotificationTemplates.Single(template => template.Code == "lead.assigned");
        Assert.Equal(existingId, existing.Id);
        Assert.Equal("custom-module", existing.Module);
        Assert.Equal(NotificationChannel.Email, existing.Channel);
        Assert.False(existing.IsActive);
        Assert.Equal(count, _db.NotificationTemplates.Count());
        Assert.Contains(_db.NotificationTemplates, template => template.Code == "quote.approved");
    }

    [Fact]
    public void Seed_RejectsDuplicateTemplateDefinitionsBeforeInsertion()
    {
        const string json = """
            { "templates": [
              { "code": "lead.assigned", "module": "leads", "channel": "InApp" },
              { "code": "LEAD.ASSIGNED", "module": "leads", "channel": "Both" }
            ] }
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var error = Assert.Throws<InvalidDataException>(() => NotificationTemplateSeeder.Seed(_db, stream));

        Assert.Contains("lead.assigned", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.NotificationTemplates);
    }
}
