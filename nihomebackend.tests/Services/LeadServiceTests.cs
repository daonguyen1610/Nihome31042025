using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class LeadServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPermissionService> _permissions;
    private readonly Mock<INotificationService> _notifications;
    private readonly LeadService _sut;

    public LeadServiceTests()
    {
        _db = DbContextFactory.Create();
        _permissions = new Mock<IPermissionService>();
        _notifications = new Mock<INotificationService>();
        _sut = new LeadService(
            _db,
            _permissions.Object,
            _notifications.Object,
            NullLogger<LeadService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_RequiresPhoneOrEmail()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");

        var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", SourceCode = "marketing" },
            sales.Id,
            canManage: true));

        Assert.Contains("at least one", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.Leads);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownSourceCode()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing"); // only marketing seeded

        var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "0900000000", SourceCode = "tiktok" },
            sales.Id,
            canManage: true));

        Assert.Contains("SourceCode", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsInactiveSource()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing", isActive: false);

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "0900000000", SourceCode = "marketing" },
            sales.Id,
            canManage: true));
    }

    [Fact]
    public async Task CreateAsync_WithoutManagePermission_Throws()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "0900000000", SourceCode = "marketing" },
            sales.Id,
            canManage: false));
    }

    [Fact]
    public async Task CreateAsync_PersistsLeadWithProvidedOwner()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var owner = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(owner.Id);

        var response = await _sut.CreateAsync(
            new CreateLeadRequest
            {
                Name = "Ms. Nga",
                CompanyName = "ACME",
                Phone = "0900000000",
                SourceCode = "marketing",
                OwnerUserId = owner.Id,
                Note = "referred by CEO",
            },
            creator.Id,
            canManage: true);

        Assert.Equal(owner.Id, response.OwnerUserId);
        Assert.Equal(LeadStatus.New, response.Status);
        Assert.Equal("marketing", response.SourceCode);

        var saved = Assert.Single(_db.Leads);
        Assert.Equal(creator.Id, saved.CreatedByUserId);
    }

    [Fact]
    public async Task CreateAsync_RoundRobin_PicksUserWithFewestOpenLeads()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var salesA = await SeedUserAsync(UserRole.USER);
        var salesB = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(salesA.Id);
        AllowManageLeads(salesB.Id);

        // salesA already owns 2 open leads; salesB owns 0.
        _db.Leads.AddRange(
            new Lead { Name = "L1", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.New },
            new Lead { Name = "L2", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.Contacted });
        await _db.SaveChangesAsync();

        var response = await _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "090", SourceCode = "marketing" },
            creator.Id,
            canManage: true);

        Assert.Equal(salesB.Id, response.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_RoundRobin_IgnoresConvertedAndJunkLeadsWhenCounting()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var salesA = await SeedUserAsync(UserRole.USER);
        var salesB = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(salesA.Id);
        AllowManageLeads(salesB.Id);

        // salesA has 3 Converted + 1 Junk (all ignored) → workload = 0.
        // salesB has 1 New → workload = 1. Round-robin should pick salesA.
        _db.Leads.AddRange(
            new Lead { Name = "c1", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.Converted },
            new Lead { Name = "c2", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.Converted },
            new Lead { Name = "c3", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.Converted },
            new Lead { Name = "j1", SourceCode = "marketing", Phone = "1", OwnerUserId = salesA.Id, Status = LeadStatus.Junk },
            new Lead { Name = "b1", SourceCode = "marketing", Phone = "1", OwnerUserId = salesB.Id, Status = LeadStatus.New });
        await _db.SaveChangesAsync();

        var response = await _sut.CreateAsync(
            new CreateLeadRequest { Name = "Fresh", Phone = "090", SourceCode = "marketing" },
            creator.Id,
            canManage: true);

        Assert.Equal(salesA.Id, response.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_LeavesUnassigned_WhenNoEligibleOwnerExists()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        // No user has crm.leads.manage — permissions.HasAsync stays false by default.

        var response = await _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "090", SourceCode = "marketing" },
            creator.Id,
            canManage: true);

        Assert.Null(response.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_RejectsInactiveOrUnpermittedProvidedOwner()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var owner = await SeedUserAsync(UserRole.USER, isActive: false);
        SeedSource("marketing");
        AllowManageLeads(owner.Id); // has permission but user inactive

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.CreateAsync(
            new CreateLeadRequest { Name = "N", Phone = "1", SourceCode = "marketing", OwnerUserId = owner.Id },
            creator.Id,
            canManage: true));
    }

    [Fact]
    public async Task CreateAsync_FiresLeadAssignedNotification_WhenOwnerSet()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var owner = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(owner.Id);

        await _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "0900", SourceCode = "marketing", OwnerUserId = owner.Id },
            creator.Id,
            canManage: true,
            languageCode: "en");

        _notifications.Verify(n => n.NotifyFromTemplateAsync(
            owner.Id,
            "lead.assigned",
            It.Is<IDictionary<string, string>>(d => d["leadName"].Contains("Ms. Nga")),
            "Lead",
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            "en"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DoesNotFail_WhenNotificationDispatchThrows()
    {
        var creator = await SeedUserAsync(UserRole.USER);
        var owner = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(owner.Id);
        _notifications
            .Setup(n => n.NotifyFromTemplateAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var response = await _sut.CreateAsync(
            new CreateLeadRequest { Name = "Ms. Nga", Phone = "090", SourceCode = "marketing", OwnerUserId = owner.Id },
            creator.Id,
            canManage: true);

        Assert.NotNull(response);
        Assert.Single(_db.Leads);
    }

    // ---------------- List & Get scoping ----------------

    [Fact]
    public async Task ListAsync_SalesUser_SeesOnlyOwnLeads()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        var other = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        _db.Leads.AddRange(
            new Lead { Name = "Mine", SourceCode = "marketing", Phone = "1", OwnerUserId = sales.Id },
            new Lead { Name = "Theirs", SourceCode = "marketing", Phone = "1", OwnerUserId = other.Id });
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(sales.Id, canSeeAll: false);

        Assert.Equal(1, list.Total);
        Assert.Equal("Mine", Assert.Single(list.Items).Name);
    }

    [Fact]
    public async Task ListAsync_Manager_SeesAllLeads()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        _db.Leads.AddRange(
            new Lead { Name = "A", SourceCode = "marketing", Phone = "1", OwnerUserId = sales.Id },
            new Lead { Name = "B", SourceCode = "marketing", Phone = "1", OwnerUserId = null });
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(manager.Id, canSeeAll: true);

        Assert.Equal(2, list.Total);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndSource()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        SeedSource("referral");
        _db.Leads.AddRange(
            new Lead { Name = "A", SourceCode = "marketing", Phone = "1", Status = LeadStatus.New },
            new Lead { Name = "B", SourceCode = "referral", Phone = "1", Status = LeadStatus.Contacted },
            new Lead { Name = "C", SourceCode = "marketing", Phone = "1", Status = LeadStatus.Contacted });
        await _db.SaveChangesAsync();

        var byStatus = await _sut.ListAsync(manager.Id, canSeeAll: true, status: LeadStatus.Contacted);
        Assert.Equal(2, byStatus.Total);

        var bySource = await _sut.ListAsync(manager.Id, canSeeAll: true, sourceCode: "marketing");
        Assert.Equal(2, bySource.Total);

        var combined = await _sut.ListAsync(manager.Id, canSeeAll: true, status: LeadStatus.Contacted, sourceCode: "marketing");
        Assert.Equal("C", Assert.Single(combined.Items).Name);
    }

    [Fact]
    public async Task ListAsync_SearchesNamePhoneEmailCompany()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        _db.Leads.AddRange(
            new Lead { Name = "Anna", SourceCode = "marketing", Phone = "555" },
            new Lead { Name = "Bob", CompanyName = "AnnaCorp", SourceCode = "marketing", Phone = "1" },
            new Lead { Name = "Chi", Email = "anna@ex.com", SourceCode = "marketing", Phone = "1" },
            new Lead { Name = "Zach", SourceCode = "marketing", Phone = "1" });
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(manager.Id, canSeeAll: true, search: "anna");
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task ListAsync_CapsPageSizeAt100()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        for (var i = 0; i < 120; i++)
        {
            _db.Leads.Add(new Lead { Name = $"L{i}", SourceCode = "marketing", Phone = "1" });
        }
        await _db.SaveChangesAsync();

        var page = await _sut.ListAsync(manager.Id, canSeeAll: true, pageSize: 500);

        Assert.Equal(120, page.Total);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(100, page.Items.Count);
    }

    [Fact]
    public async Task GetAsync_SalesUser_CannotSeeOtherLead()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        var other = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        _db.Leads.Add(new Lead { Id = 42, Name = "L", SourceCode = "marketing", Phone = "1", OwnerUserId = other.Id });
        await _db.SaveChangesAsync();

        var found = await _sut.GetAsync(42, sales.Id, canSeeAll: false);
        Assert.Null(found);
    }

    [Fact]
    public async Task GetAsync_ReturnsActivitiesOrderedByCreatedAtDesc()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = new Lead { Name = "L", SourceCode = "marketing", Phone = "1", OwnerUserId = sales.Id };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        _db.LeadActivities.AddRange(
            new LeadActivity { LeadId = lead.Id, Type = LeadActivityType.Call, Content = "1st", CreatedByUserId = sales.Id, CreatedAt = new DateTime(2026, 1, 1) },
            new LeadActivity { LeadId = lead.Id, Type = LeadActivityType.Email, Content = "2nd", CreatedByUserId = sales.Id, CreatedAt = new DateTime(2026, 1, 5) },
            new LeadActivity { LeadId = lead.Id, Type = LeadActivityType.Note, Content = "3rd", CreatedByUserId = sales.Id, CreatedAt = new DateTime(2026, 1, 3) });
        await _db.SaveChangesAsync();

        var found = await _sut.GetAsync(lead.Id, sales.Id, canSeeAll: false);

        Assert.NotNull(found);
        Assert.Equal(new[] { "2nd", "3rd", "1st" }, found!.Activities.Select(a => a.Content));
    }

    // ---------------- Update / status rules ----------------

    [Fact]
    public async Task UpdateAsync_ConvertedLead_CannotBeEdited()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(status: LeadStatus.Converted);

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted),
            manager.Id,
            canManage: true,
            canSeeAll: true));
    }

    [Fact]
    public async Task UpdateAsync_SalesUser_CannotMoveToJunk()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Junk, ownerId: sales.Id),
            sales.Id,
            canManage: true,
            canSeeAll: false));

        Assert.Contains("Sales Manager", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_SalesUser_CannotMoveToNotInterested()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.NotInterested, ownerId: sales.Id),
            sales.Id,
            canManage: true,
            canSeeAll: false));
    }

    [Fact]
    public async Task UpdateAsync_Manager_CanMoveToJunk()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync();

        var response = await _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Junk),
            manager.Id,
            canManage: true,
            canSeeAll: true);

        Assert.NotNull(response);
        Assert.Equal(LeadStatus.Junk, response!.Status);
    }

    [Fact]
    public async Task UpdateAsync_RejectsDirectTransitionToConverted()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync();

        var ex = await Assert.ThrowsAsync<LeadOperationException>(() => _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Converted),
            manager.Id,
            canManage: true,
            canSeeAll: true));

        Assert.Contains("/convert", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_SalesUser_CannotReassignToDifferentOwner()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        var other = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(other.Id);
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        await Assert.ThrowsAsync<LeadOperationException>(() => _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted, ownerId: other.Id),
            sales.Id,
            canManage: true,
            canSeeAll: false));
    }

    [Fact]
    public async Task UpdateAsync_Manager_ReassignFiresNotificationForNewOwnerOnly()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        var current = await SeedUserAsync(UserRole.USER);
        var newOwner = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        AllowManageLeads(newOwner.Id);
        var lead = await SeedLeadAsync(ownerId: current.Id);

        await _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted, ownerId: newOwner.Id),
            manager.Id,
            canManage: true,
            canSeeAll: true);

        _notifications.Verify(n => n.NotifyFromTemplateAsync(
            newOwner.Id,
            "lead.assigned",
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<string>()), Times.Once);

        _notifications.Verify(n => n.NotifyFromTemplateAsync(
            current.Id,
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SameOwner_DoesNotFireNotification()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        await _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted, ownerId: sales.Id),
            sales.Id,
            canManage: true,
            canSeeAll: false);

        _notifications.Verify(n => n.NotifyFromTemplateAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SalesUser_CannotSeeOrEditOtherOwnersLead()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        var otherSales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: otherSales.Id);

        var response = await _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted, ownerId: sales.Id),
            sales.Id,
            canManage: true,
            canSeeAll: false);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateAsync_Manager_CanUnassignOwner()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        var current = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: current.Id);

        var response = await _sut.UpdateAsync(
            lead.Id,
            BuildUpdate(status: LeadStatus.Contacted, ownerId: null),
            manager.Id,
            canManage: true,
            canSeeAll: true);

        Assert.NotNull(response);
        Assert.Null(response!.OwnerUserId);

        // Unassignment must NOT fire the assign notification.
        _notifications.Verify(n => n.NotifyFromTemplateAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<string>()), Times.Never);
    }

    // ---------------- Convert ----------------

    [Fact]
    public async Task ConvertAsync_RequiresConvertPermission()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        await Assert.ThrowsAsync<LeadOperationException>(() =>
            _sut.ConvertAsync(lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: false));
    }

    [Fact]
    public async Task ConvertAsync_SetsStatusAndStamps()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        var response = await _sut.ConvertAsync(
            lead.Id,
            new ConvertLeadRequest { CustomerId = 700, OpportunityId = 701, Note = "signed" },
            sales.Id,
            canConvert: true);

        Assert.NotNull(response);
        Assert.Equal(LeadStatus.Converted, response!.Status);
        Assert.Equal(700, response.ConvertedCustomerId);
        Assert.Equal(701, response.ConvertedOpportunityId);
        Assert.NotNull(response.ConvertedAt);

        var savedActivity = Assert.Single(_db.LeadActivities);
        Assert.Contains("signed", savedActivity.Content);
    }

    [Fact]
    public async Task ConvertAsync_AlreadyConverted_Throws()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(status: LeadStatus.Converted);

        await Assert.ThrowsAsync<LeadOperationException>(() =>
            _sut.ConvertAsync(lead.Id, new ConvertLeadRequest(), sales.Id, canConvert: true));
    }

    [Fact]
    public async Task ConvertAsync_JunkOrNotInterested_Throws()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var junk = await SeedLeadAsync(status: LeadStatus.Junk);

        await Assert.ThrowsAsync<LeadOperationException>(() =>
            _sut.ConvertAsync(junk.Id, new ConvertLeadRequest(), sales.Id, canConvert: true));
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_ConvertedLead_Throws()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(status: LeadStatus.Converted);

        await Assert.ThrowsAsync<LeadOperationException>(() =>
            _sut.DeleteAsync(lead.Id, manager.Id, canManage: true));

        Assert.Single(_db.Leads);
    }

    [Fact]
    public async Task DeleteAsync_UnconvertedLead_Removed()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync();

        var deleted = await _sut.DeleteAsync(lead.Id, manager.Id, canManage: true);

        Assert.True(deleted);
        Assert.Empty(_db.Leads);
    }

    [Fact]
    public async Task DeleteAsync_MissingId_ReturnsFalse()
    {
        var manager = await SeedUserAsync(UserRole.USER);
        var deleted = await _sut.DeleteAsync(99999, manager.Id, canManage: true);
        Assert.False(deleted);
    }

    // ---------------- Activities ----------------

    [Fact]
    public async Task AddActivityAsync_OtherOwnersLead_ReturnsNullForSales()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        var owner = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: owner.Id);

        var response = await _sut.AddActivityAsync(
            lead.Id,
            new CreateLeadActivityRequest { Type = LeadActivityType.Call, Content = "Hi" },
            sales.Id,
            canSeeAll: false);

        Assert.Null(response);
        Assert.Empty(_db.LeadActivities);
    }

    [Fact]
    public async Task AddActivityAsync_OwnerCanAppendEntry()
    {
        var sales = await SeedUserAsync(UserRole.USER);
        SeedSource("marketing");
        var lead = await SeedLeadAsync(ownerId: sales.Id);

        var response = await _sut.AddActivityAsync(
            lead.Id,
            new CreateLeadActivityRequest { Type = LeadActivityType.Meeting, Content = "  site visit  " },
            sales.Id,
            canSeeAll: false);

        Assert.NotNull(response);
        Assert.Equal(LeadActivityType.Meeting, response!.Type);
        Assert.Equal("site visit", response.Content);
    }

    // ---------------- Helpers ----------------

    private async Task<ApplicationUser> SeedUserAsync(UserRole role, bool isActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            PhoneNumber = suffix,
            PasswordHash = "hash",
            Role = role,
            IsActive = isActive,
            Email = $"user-{suffix}@nihome.test",
            FullName = $"User {suffix[..4]}",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private void SeedSource(string code, bool isActive = true)
    {
        if (_db.MasterDataOptions.Any(o => o.Category == "customer_source" && o.Code == code))
        {
            return;
        }

        _db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "customer_source",
            Code = code,
            Name = code,
            SortOrder = 1,
            IsActive = isActive,
        });
        _db.SaveChanges();
    }

    private void AllowManageLeads(int userId)
    {
        _permissions
            .Setup(p => p.HasAsync(userId, "crm.leads.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private async Task<Lead> SeedLeadAsync(
        LeadStatus status = LeadStatus.New,
        int? ownerId = null,
        string sourceCode = "marketing")
    {
        var lead = new Lead
        {
            Name = "Ms. Nga",
            SourceCode = sourceCode,
            Phone = "0900000000",
            Status = status,
            OwnerUserId = ownerId,
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return lead;
    }

    private static UpdateLeadRequest BuildUpdate(
        LeadStatus status = LeadStatus.Contacted,
        int? ownerId = null,
        string sourceCode = "marketing") => new()
        {
            Name = "Ms. Nga",
            Phone = "0900000000",
            SourceCode = sourceCode,
            Status = status,
            OwnerUserId = ownerId,
        };
}
