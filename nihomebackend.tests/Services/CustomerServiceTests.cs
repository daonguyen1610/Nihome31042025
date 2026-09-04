using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class CustomerServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ICrmHardDeletePlanService _plans;
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _db = DbContextFactory.Create();
        var hardDelete = HardDeleteTestServices.Create(
            _db, Mock.Of<IProjectDocumentStagingService>());
        _plans = hardDelete.CrmPlans;
        _sut = new CustomerService(
            _db,
            NullLogger<CustomerService>.Instance,
            hardDelete.CrmPlans,
            hardDelete.Operations);
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
    public async Task Delete_CustomerWithoutDownstreamRoots_RemovesOwnedRows()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);
        var customer = await _db.Customers.SingleAsync(item => item.Id == created.Id);
        customer.RowVersion = BitConverter.GetBytes(1L);
        _db.CustomerActivities.Add(new CustomerActivity
        {
            CustomerId = customer.Id,
            Type = CustomerActivityType.Note,
            Content = "Owned history",
            CreatedByUserId = sales.Id,
        });
        await _db.SaveChangesAsync();

        var impact = await _sut.GetDeletionImpactAsync(
            created.Id, sales.Id, canManage: true, canSeeAll: true);
        var result = await _sut.DeleteAsync(
            created.Id, Confirm(impact!, customer), sales.Id, canManage: true, canSeeAll: true);

        Assert.True(result!.IsComplete);
        Assert.Empty(_db.Customers);
        Assert.Empty(_db.CustomerContacts);
        Assert.Empty(_db.CustomerActivities);
    }

    [Fact]
    public async Task Delete_SalesUser_CannotDeleteOtherOwnersCustomer()
    {
        // SECURITY: Sales must never be able to wipe another user's customer
        // just by knowing the id. Return false (not throw) so the caller
        // cannot infer whether the row exists.
        var sales = await SeedUserAsync();
        var other = await SeedUserAsync();
        SeedSource("marketing");
        var owned = await _sut.CreateAsync(BuildCreate(name: "Theirs"), other.Id, canManage: true);

        var impact = await _sut.GetDeletionImpactAsync(
            owned.Id, sales.Id, canManage: true, canSeeAll: false);

        Assert.Null(impact);
        Assert.Single(_db.Customers); // still exists
    }

    [Fact]
    public async Task Delete_SalesUser_CanDeleteOwnCustomer()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var mine = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);

        var customer = await _db.Customers.SingleAsync(item => item.Id == mine.Id);
        customer.RowVersion = BitConverter.GetBytes(2L);
        await _db.SaveChangesAsync();
        var impact = await _sut.GetDeletionImpactAsync(
            mine.Id, sales.Id, canManage: true, canSeeAll: false);

        Assert.True((await _sut.DeleteAsync(
            mine.Id, Confirm(impact!, customer), sales.Id, canManage: true, canSeeAll: false))!.IsComplete);
        Assert.Empty(_db.Customers);
    }

    [Fact]
    public async Task ForCustomerAsync_ManagedDocumentCreatesDurableLocalFileItem()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);
        _db.CustomerDocuments.Add(new CustomerDocument
        {
            CustomerId = created.Id,
            FilePath = $"/files/customers/{created.Id}/brief.pdf",
            OriginalFileName = "brief.pdf",
        });
        await _db.SaveChangesAsync();

        var plan = await _plans.ForCustomerAsync(created.Id);

        Assert.True(plan!.Impact.CanDelete);
        Assert.Contains(plan.Items, item =>
            item.Kind == HardDeleteItemKind.LocalFile &&
            item.ActionIdentifier == $"/files/customers/{created.Id}/brief.pdf");
    }

    [Fact]
    public async Task Delete_ConfirmationWithWhitespaceRejectsAndPreservesCustomer()
    {
        var sales = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), sales.Id, canManage: true);
        var customer = await _db.Customers.SingleAsync(item => item.Id == created.Id);
        customer.RowVersion = BitConverter.GetBytes(3L);
        await _db.SaveChangesAsync();
        var impact = await _sut.GetDeletionImpactAsync(
            created.Id, sales.Id, canManage: true, canSeeAll: true);
        var request = Confirm(impact!, customer);
        request.Confirmation = $" {request.Confirmation} ";

        await Assert.ThrowsAsync<CustomerOperationException>(() => _sut.DeleteAsync(
            created.Id, request, sales.Id, canManage: true, canSeeAll: true));

        Assert.NotNull(await _db.Customers.FindAsync(created.Id));
        Assert.Empty(_db.HardDeleteOperations);
    }

    [Fact]
    public async Task ForwardRecovery_WhenManagePermissionWasRevoked_IsRejected()
    {
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(
                It.IsAny<int>(), "crm.customers.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CustomerHardDeleteHandler(
            _db, Mock.Of<ICrmHardDeletePlanService>(), permissions.Object);
        var context = new HardDeleteResourceContext(
            Guid.NewGuid(), "Customer", "123", "plan", "456",
            "delete-customer-aggregate", IsForwardRecovery: true);

        await Assert.ThrowsAsync<HardDeleteAuthorizationException>(() =>
            handler.AuthorizeAsync(context));
    }

    [Fact]
    public async Task DeletionImpact_DownstreamRootsBlockAndPreserveCompleteGraph()
    {
        var user = await SeedUserAsync();
        SeedSource("marketing");
        var created = await _sut.CreateAsync(BuildCreate(), user.Id, canManage: true);
        var otherCustomer = new Customer
        {
            Name = "Unrelated customer",
            Type = CustomerType.Individual,
            SourceCode = "marketing",
            OwnerUserId = user.Id,
        };
        _db.Customers.Add(otherCustomer);
        await _db.SaveChangesAsync();

        var opportunity = new Opportunity
        {
            Name = "Owned opportunity",
            CustomerId = created.Id,
            OwnerUserId = user.Id,
        };
        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();
        var quote = new Quote
        {
            Code = "QT-DELETE-CUSTOMER",
            OpportunityId = opportunity.Id,
            OwnerUserId = user.Id,
            AreaSqm = 1,
            UnitPricePerSqm = 1,
            Subtotal = 1,
            GrandTotal = 1,
        };
        var contract = new Contract
        {
            ContractNumber = "HD-DELETE-CUSTOMER",
            CustomerId = created.Id,
            OpportunityId = opportunity.Id,
            OwnerUserId = user.Id,
        };
        var tender = new Tender
        {
            Code = "TD-DELETE-CUSTOMER",
            Name = "Owned tender",
            CustomerId = created.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(1),
            ChecklistItems =
            [
                new TenderChecklistItem { Title = "Owned checklist item" },
            ],
        };
        var project = new DesignProject
        {
            ProjectCode = "DP-DELETE-CUSTOMER",
            Name = "Owned design project",
            CustomerId = created.Id,
            Contract = contract,
        };
        var convertedLead = new Lead
        {
            Name = "Preserved converted lead",
            Phone = "0900000888",
            SourceCode = "marketing",
            ConvertedCustomerId = created.Id,
            ConvertedOpportunityId = opportunity.Id,
        };
        _db.AddRange(quote, tender, project, convertedLead);
        await _db.SaveChangesAsync();
        _db.EntityTranslations.AddRange(
            new EntityTranslation
            {
                EntityType = "Customer",
                EntityId = created.Id,
                FieldName = "Name",
                LanguageCode = "en",
                Value = "Deleted customer",
            },
            new EntityTranslation
            {
                EntityType = "DesignProject",
                EntityId = project.Id,
                FieldName = "Name",
                LanguageCode = "en",
                Value = "Deleted project",
            });
        await _db.SaveChangesAsync();

        var impact = await _plans.ForCustomerAsync(created.Id);

        Assert.NotNull(impact);
        Assert.False(impact!.Impact.CanDelete);
        Assert.Contains(impact.Impact.Items, item =>
            item.Key == "customer.opportunities" && item.Action == DeletionImpactActions.Block);
        Assert.Contains(impact.Impact.Items, item =>
            item.Key == "customer.contracts" && item.Action == DeletionImpactActions.Block);
        Assert.Contains(impact.Impact.Items, item =>
            item.Key == "customer.tenders" && item.Action == DeletionImpactActions.Block);
        Assert.Contains(impact.Impact.Items, item =>
            item.Key == "customer.designProjects" && item.Action == DeletionImpactActions.Block);
        await Assert.ThrowsAsync<CustomerOperationException>(() => _sut.DeleteAsync(
            created.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = impact.Impact.PlanToken,
                Confirmation = impact.Impact.RequiredConfirmation,
                RowVersion = Convert.ToBase64String(BitConverter.GetBytes(1L)),
            },
            user.Id, canManage: true, canSeeAll: true));

        Assert.NotNull(await _db.Customers.FindAsync(created.Id));
        Assert.NotNull(await _db.Opportunities.FindAsync(opportunity.Id));
        Assert.NotNull(await _db.Quotes.FindAsync(quote.Id));
        Assert.NotNull(await _db.Contracts.FindAsync(contract.Id));
        Assert.NotNull(await _db.Tenders.FindAsync(tender.Id));
        Assert.NotNull(await _db.DesignProjects.FindAsync(project.Id));
        var preservedLead = await _db.Leads.FindAsync(convertedLead.Id);
        Assert.Equal(created.Id, preservedLead!.ConvertedCustomerId);
        Assert.Equal(opportunity.Id, preservedLead.ConvertedOpportunityId);
        Assert.NotNull(await _db.Customers.FindAsync(otherCustomer.Id));
        Assert.NotNull(await _db.Users.FindAsync(user.Id));
    }

    private static ConfirmDeletionRequest Confirm(DeletionImpactResponse impact, Customer customer) => new()
    {
        PlanToken = impact.PlanToken,
        Confirmation = impact.RequiredConfirmation,
        RowVersion = CrmConcurrency.Encode(customer.RowVersion),
    };

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
