using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class CustomerServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new CustomerService(_db, NullLogger<CustomerService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ---------------- Create validation ----------------

    [Fact]
    public async Task Create_WithoutManage_Throws()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(BuildCreate(), user.Id, canManage: false));
    }

    [Fact]
    public async Task Create_CompanyMissingTaxId_Throws()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var req = BuildCreate(type: CustomerType.Company);
        req.TaxId = null;

        var ex = await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(req, user.Id, canManage: true));
        Assert.Contains("TaxId", ex.Message);
    }

    [Fact]
    public async Task Create_CompanyMissingAddressOrRepresentative_Throws()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");

        var noAddr = BuildCreate(type: CustomerType.Company);
        noAddr.Address = null;
        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(noAddr, user.Id, canManage: true));

        var noRep = BuildCreate(type: CustomerType.Company);
        noRep.RepresentativeName = null;
        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(noRep, user.Id, canManage: true));
    }

    [Fact]
    public async Task Create_IndividualDoesNotRequireCompanyFields()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var req = BuildCreate(type: CustomerType.Individual);
        req.TaxId = null;
        req.Address = null;
        req.RepresentativeName = null;

        var response = await _sut.CreateAsync(req, user.Id, canManage: true);
        Assert.NotNull(response);
        Assert.Equal(CustomerType.Individual, response.Type);
    }

    [Fact]
    public async Task Create_ContactMustHavePhoneOrEmail()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var req = BuildCreate();
        req.PrimaryContact.Phone = null;
        req.PrimaryContact.Email = null;
        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(req, user.Id, canManage: true));
    }

    [Fact]
    public async Task Create_RejectsUnknownSource()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var req = BuildCreate();
        req.SourceCode = "tiktok";
        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.CreateAsync(req, user.Id, canManage: true));
    }

    [Fact]
    public async Task Create_AssignsCallerAsOwner_WhenOwnerNotSpecified()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var response = await _sut.CreateAsync(BuildCreate(), user.Id, canManage: true);
        Assert.Equal(user.Id, response.OwnerUserId);
    }

    [Fact]
    public async Task Create_ForcesPrimaryContactFlag()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var req = BuildCreate();
        req.PrimaryContact.IsPrimary = false; // even if caller lies, service forces primary=true

        var response = await _sut.CreateAsync(req, user.Id, canManage: true);
        Assert.Single(response.Contacts);
        Assert.True(response.Contacts[0].IsPrimary);
    }

    // ---------------- Duplicate detection ----------------

    [Fact]
    public async Task Create_Company_DuplicateTaxId_ThrowsDuplicateWithoutReason()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        await _sut.CreateAsync(BuildCreate(type: CustomerType.Company, name: "ACME"), user.Id, canManage: true);

        var ex = await Assert.ThrowsAsync<CustomerDuplicateException>(() =>
            _sut.CreateAsync(BuildCreate(type: CustomerType.Company, name: "ACME clone"), user.Id, canManage: true));

        Assert.Equal("TaxId", ex.Detail.Field);
        Assert.Contains("ACME", ex.Detail.ExistingCustomerName);
    }

    [Fact]
    public async Task Create_Company_DuplicateTaxId_AllowsWithOverrideReason()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        await _sut.CreateAsync(BuildCreate(type: CustomerType.Company, name: "ACME"), user.Id, canManage: true);

        var second = BuildCreate(type: CustomerType.Company, name: "ACME sister");
        second.DuplicateOverrideReason = "Cùng tập đoàn, khác pháp nhân";
        var response = await _sut.CreateAsync(second, user.Id, canManage: true);

        Assert.NotNull(response);
        Assert.Equal(2, _db.Customers.Count());
    }

    [Fact]
    public async Task Create_Individual_DuplicatePhone_ThrowsDuplicate()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        await _sut.CreateAsync(BuildCreate(type: CustomerType.Individual, name: "Nga"), user.Id, canManage: true);

        var ex = await Assert.ThrowsAsync<CustomerDuplicateException>(() =>
            _sut.CreateAsync(BuildCreate(type: CustomerType.Individual, name: "Nga 2"), user.Id, canManage: true));

        Assert.Equal("Phone", ex.Detail.Field);
    }

    [Fact]
    public async Task Update_ExcludesSelfFromDuplicateCheck()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(type: CustomerType.Company, name: "ACME"), user.Id, canManage: true);

        var updated = await _sut.UpdateAsync(
            created.Id,
            new UpdateCustomerRequest
            {
                Type = CustomerType.Company,
                Name = "ACME renamed",
                TaxId = "1234567890",   // same as before
                Address = "1 Nguyễn Trãi",
                RepresentativeName = "CEO",
                SourceCode = "marketing",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                OwnerUserId = user.Id,
            },
            user.Id,
            canManage: true,
            canSeeAll: true);

        Assert.NotNull(updated);
        Assert.Equal("ACME renamed", updated!.Name);
    }

    // ---------------- RBAC scoping ----------------

    [Fact]
    public async Task List_SalesUser_SeesOnlyOwnCustomers()
    {
        var sales = await SeedUserAsync();
        var other = await SeedUserAsync();
        SeedSource("marketing");
        await _sut.CreateAsync(BuildCreate(name: "Mine"), sales.Id, canManage: true);
        await _sut.CreateAsync(BuildCreate(name: "Other", phone: "0900002222"), other.Id, canManage: true);

        var list = await _sut.ListAsync(sales.Id, canSeeAll: false);
        Assert.Equal(1, list.Total);
        Assert.Equal("Mine", list.Items[0].Name);
    }

    [Fact]
    public async Task Get_SalesUser_CannotSeeOtherOwnersCustomer()
    {
        var sales = await SeedUserAsync();
        var other = await SeedUserAsync();
        SeedSource("marketing");
        var owned = await _sut.CreateAsync(BuildCreate(name: "Theirs"), other.Id, canManage: true);

        Assert.Null(await _sut.GetAsync(owned.Id, sales.Id, canSeeAll: false));
    }

    [Fact]
    public async Task Update_SalesUser_CannotReassign()
    {
        var sales = await SeedUserAsync();
        var other = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(name: "Mine"), sales.Id, canManage: true);

        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.UpdateAsync(created.Id, BuildUpdate(ownerUserId: other.Id), sales.Id, canManage: true, canSeeAll: false));
    }

    [Fact]
    public async Task Update_SalesUser_CannotSuspend()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        var ex = await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.UpdateAsync(created.Id,
                BuildUpdate(status: CustomerRelationshipStatus.Suspended, ownerUserId: sales.Id),
                sales.Id,
                canManage: true,
                canSeeAll: false));
        Assert.Contains("suspend", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Manager_CanSuspendAndReassign()
    {
        var manager = await SeedUserAsync();
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        var updated = await _sut.UpdateAsync(created.Id,
            BuildUpdate(status: CustomerRelationshipStatus.Suspended, ownerUserId: manager.Id),
            manager.Id,
            canManage: true,
            canSeeAll: true);

        Assert.Equal(CustomerRelationshipStatus.Suspended, updated!.RelationshipStatus);
        Assert.Equal(manager.Id, updated.OwnerUserId);
    }

    // ---------------- Contacts ----------------

    [Fact]
    public async Task UpsertContact_AddingWithIsPrimaryDemotesExistingPrimary()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        var newContact = await _sut.UpsertContactAsync(created.Id,
            new UpsertCustomerContactRequest { FullName = "Anh Long", Phone = "0900004444", IsPrimary = true },
            sales.Id, canManage: true, canSeeAll: false);

        Assert.NotNull(newContact);
        Assert.True(newContact!.IsPrimary);

        var contacts = _db.CustomerContacts.Where(c => c.CustomerId == created.Id).ToList();
        Assert.Single(contacts, c => c.IsPrimary);
        Assert.Equal("Anh Long", contacts.Single(c => c.IsPrimary).FullName);
    }

    [Fact]
    public async Task UpsertContact_UnsettingOnlyPrimary_KeepsItPrimary()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);
        var primaryContactId = created.Contacts[0].Id;

        var updated = await _sut.UpsertContactAsync(created.Id,
            new UpsertCustomerContactRequest
            {
                Id = primaryContactId,
                FullName = created.Contacts[0].FullName,
                Phone = created.Contacts[0].Phone,
                IsPrimary = false, // caller tries to unset — service must refuse to leave zero primaries
            },
            sales.Id, canManage: true, canSeeAll: false);

        Assert.True(updated!.IsPrimary);
    }

    [Fact]
    public async Task DeleteContact_OnlyContact_Throws()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        await Assert.ThrowsAsync<CustomerOperationException>(() =>
            _sut.DeleteContactAsync(created.Id, created.Contacts[0].Id, sales.Id, canManage: true, canSeeAll: false));
    }

    [Fact]
    public async Task DeleteContact_PrimaryPromotesOldestSurvivor()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);
        var primaryId = created.Contacts[0].Id;

        // Add second contact non-primary.
        var second = await _sut.UpsertContactAsync(created.Id,
            new UpsertCustomerContactRequest { FullName = "Backup", Phone = "0900004444" },
            sales.Id, canManage: true, canSeeAll: false);

        var deleted = await _sut.DeleteContactAsync(created.Id, primaryId, sales.Id, canManage: true, canSeeAll: false);
        Assert.True(deleted);

        var remaining = _db.CustomerContacts.Where(c => c.CustomerId == created.Id).ToList();
        Assert.Single(remaining);
        Assert.True(remaining[0].IsPrimary);
        Assert.Equal(second!.Id, remaining[0].Id);
    }

    // ---------------- Activities ----------------

    [Fact]
    public async Task AddActivity_SalesUser_CannotAddToOtherOwnersCustomer()
    {
        var sales = await SeedUserAsync();
        var other = await SeedUserAsync();
        SeedSource("marketing");
        var owned = await _sut.CreateAsync(BuildCreate(name: "Their"), other.Id, canManage: true);

        var response = await _sut.AddActivityAsync(owned.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Call, Content = "hi" },
            sales.Id, canSeeAll: false);

        Assert.Null(response);
        Assert.Empty(_db.CustomerActivities);
    }

    [Fact]
    public async Task AddActivity_OwnerCanAppend()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        var response = await _sut.AddActivityAsync(created.Id,
            new CreateCustomerActivityRequest { Type = CustomerActivityType.Meeting, Content = "  Site visit  " },
            sales.Id, canSeeAll: false);

        Assert.NotNull(response);
        Assert.Equal("Site visit", response!.Content);
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task Delete_UnconvertedCustomer_Removed()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        Assert.True(await _sut.DeleteAsync(created.Id, sales.Id, canManage: true));
        Assert.Empty(_db.Customers);
    }

    // ---------------- Helpers ----------------

    private async Task<ApplicationUser> SeedUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            PhoneNumber = suffix,
            PasswordHash = "hash",
            Role = UserRole.USER,
            IsActive = true,
            Email = $"user-{suffix}@nihome.test",
            FullName = $"User {suffix[..4]}",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private void SeedSource(string code)
    {
        if (_db.MasterDataOptions.Any(o => o.Category == "customer_source" && o.Code == code)) return;
        _db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "customer_source",
            Code = code,
            Name = code,
            SortOrder = 1,
            IsActive = true,
        });
        _db.SaveChanges();
    }

    private static CreateCustomerRequest BuildCreate(
        CustomerType type = CustomerType.Individual,
        string name = "Ms. Nga",
        string phone = "0900001111",
        string sourceCode = "marketing") => new()
        {
            Type = type,
            Name = name,
            SourceCode = sourceCode,
            TaxId = type == CustomerType.Company ? "1234567890" : null,
            Address = type == CustomerType.Company ? "1 Nguyễn Trãi, Hà Nội" : null,
            RepresentativeName = type == CustomerType.Company ? "Nguyễn Văn CEO" : null,
            PrimaryContact = new UpsertCustomerContactRequest
            {
                FullName = name,
                Phone = phone,
                IsPrimary = true,
            },
        };

    private static UpdateCustomerRequest BuildUpdate(
        CustomerRelationshipStatus status = CustomerRelationshipStatus.InProgress,
        int? ownerUserId = null) => new()
        {
            Type = CustomerType.Individual,
            Name = "Ms. Nga",
            SourceCode = "marketing",
            RelationshipStatus = status,
            OwnerUserId = ownerUserId,
        };
}
