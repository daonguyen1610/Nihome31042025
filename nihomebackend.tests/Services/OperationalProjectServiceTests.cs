using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class OperationalProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly OperationalProjectService _service;
    private readonly int _managerId;
    private readonly int _otherUserId;
    private readonly int _customerId;

    public OperationalProjectServiceTests()
    {
        _service = new OperationalProjectService(
            _db,
            NullLogger<OperationalProjectService>.Instance);
        var manager = AddUser("0900100001", "Project Manager");
        var other = AddUser("0900100002", "Other Manager");
        var customer = new Customer
        {
            Name = "NICON Project Customer",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _managerId = manager.Id;
        _otherUserId = other.Id;
        _customerId = customer.Id;
    }

    [Fact]
    public async Task CreateAsync_AllocatesCodeAndDefaultsManager()
    {
        var result = await _service.CreateAsync(ValidCreate(), _managerId, false);

        Assert.StartsWith($"PJ-{DateTime.UtcNow.Year}-", result.Code);
        Assert.Equal(_managerId, result.ProjectManagerUserId);
        Assert.Equal("Planning", result.Status);
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_IsRejected()
    {
        var request = ValidCreate();
        request.StartDate = new DateTime(2026, 8, 20);
        request.EndDate = new DateTime(2026, 8, 19);

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(request, _managerId, false));
    }

    [Fact]
    public async Task CreateAsync_NonManagerCannotAssignAnotherUser()
    {
        var request = ValidCreate();
        request.ProjectManagerUserId = _otherUserId;

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(request, _managerId, false));
    }

    [Fact]
    public async Task ListAsync_NonManagerSeesOnlyOwnProjects()
    {
        await _service.CreateAsync(ValidCreate("Own project"), _managerId, false);
        await _service.CreateAsync(ValidCreate("Other project"), _otherUserId, false);

        var result = await _service.ListAsync(
            new OperationalProjectListParams(),
            _managerId,
            false);

        var item = Assert.Single(result.Items);
        Assert.Equal("Own project", item.Name);
    }

    [Fact]
    public async Task UpdateAsync_InvalidTerminalTransition_IsRejected()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var activated = await _service.UpdateAsync(
            created.Id,
            ValidUpdate(created, OperationalProjectStatus.Active),
            _managerId,
            false);
        var completed = await _service.UpdateAsync(
            created.Id,
            ValidUpdate(activated!, OperationalProjectStatus.Completed),
            _managerId,
            false);

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.UpdateAsync(
                created.Id,
                ValidUpdate(completed!, OperationalProjectStatus.Active),
                _managerId,
                false));
    }

    [Fact]
    public async Task DeleteAsync_WithContract_IsRejectedAndPreservesProject()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        _db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-2026-OP-1",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            Value = 100,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.DeleteAsync(created.Id, _managerId, false, created.RowVersion));
        Assert.NotNull(await _db.OperationalProjects.FindAsync(created.Id));
    }

    [Fact]
    public async Task UpdateAsync_CustomerWithDependencies_CannotChange()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        _db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-2026-OP-2",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
        });
        var otherCustomer = new Customer
        {
            Name = "Other project customer",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        _db.Customers.Add(otherCustomer);
        await _db.SaveChangesAsync();
        var request = ValidUpdate(created, OperationalProjectStatus.Planning);
        request.CustomerId = otherCustomer.Id;

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.UpdateAsync(created.Id, request, _managerId, false));
    }

    [Fact]
    public async Task GetAsync_AggregatesLinkedBusinessRecords()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var opportunity = new Opportunity
        {
            Name = "Linked opportunity",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            OwnerUserId = _managerId,
        };
        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();
        _db.Quotes.Add(new Quote
        {
            Code = "QT-OP-1",
            OpportunityId = opportunity.Id,
            OperationalProjectId = created.Id,
        });
        _db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-OP-1",
            CustomerId = _customerId,
            OpportunityId = opportunity.Id,
            OperationalProjectId = created.Id,
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAsync(created.Id, _managerId, false);

        Assert.NotNull(result);
        Assert.Single(result!.Opportunities);
        Assert.Single(result.Quotes);
        Assert.Single(result.Contracts);
    }

    public void Dispose() => _db.Dispose();

    private CreateOperationalProjectRequest ValidCreate(string name = "Central project") => new()
    {
        Name = name,
        CustomerId = _customerId,
        ProjectManagerUserId = null,
    };

    private static UpdateOperationalProjectRequest ValidUpdate(
        OperationalProjectResponse current,
        OperationalProjectStatus status) => new()
        {
            Name = current.Name,
            CustomerId = current.CustomerId,
            ProjectManagerUserId = current.ProjectManagerUserId,
            StartDate = current.StartDate,
            EndDate = current.EndDate,
            Note = current.Note,
            Status = status,
            RowVersion = current.RowVersion,
        };

    private ApplicationUser AddUser(string phone, string name)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = $"{phone}@example.com",
            PasswordHash = "x",
            Role = UserRole.USER,
            IsActive = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }
}
