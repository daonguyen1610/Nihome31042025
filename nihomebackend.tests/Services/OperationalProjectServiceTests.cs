using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            new LegacyProjectTeamSyncService(_db),
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
        var member = await _db.OperationalProjectMembers.Include(item => item.Roles).SingleAsync();
        Assert.Equal(_managerId, member.UserId);
        Assert.Contains(member.Roles, role =>
            role.RoleCode == ProjectTeamRoleCode.ProjectManager &&
            role.Scope == ProjectRoleScope.Project &&
            role.EndedAt == null);
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
    public async Task CreateAsync_InactiveManager_IsRejectedWithoutMembership()
    {
        var inactive = AddUser("0900100003", "Inactive Manager");
        inactive.IsActive = false;
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.ProjectManagerUserId = inactive.Id;

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(request, _managerId, true));

        Assert.Empty(await _db.OperationalProjectMembers.ToListAsync());
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
    public async Task ActiveTeamMember_CanDiscoverAndReadButCannotUpdateProject()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        _db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = created.Id,
            UserId = _otherUserId,
            Position = "Observer",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = _managerId,
            UpdatedByUserId = _managerId,
        });
        await _db.SaveChangesAsync();

        var list = await _service.ListAsync(
            new OperationalProjectListParams(), _otherUserId, false);
        var detail = await _service.GetAsync(created.Id, _otherUserId, false);
        var timeline = await _service.GetTimelineAsync(created.Id, _otherUserId, false);
        var update = await _service.UpdateAsync(
            created.Id,
            ValidUpdate(created, OperationalProjectStatus.Planning),
            _otherUserId,
            false);
        var delete = await _service.DeleteAsync(
            created.Id, _otherUserId, false, created.RowVersion);

        Assert.Contains(list.Items, item => item.Id == created.Id);
        Assert.NotNull(detail);
        Assert.NotNull(timeline);
        Assert.Null(update);
        Assert.False(delete);
    }

    [Fact]
    public async Task DeleteAsync_WithResponsibilityHistory_IsRejectedAndPreservesHistory()
    {
        var project = new OperationalProject
        {
            Code = "PJ-HISTORY-ONLY",
            Name = "History only",
            CustomerId = _customerId,
            ProjectManagerUserId = _managerId,
            CreatedByUserId = _managerId,
            UpdatedByUserId = _managerId,
        };
        _db.OperationalProjects.Add(project);
        await _db.SaveChangesAsync();
        _db.OperationalProjectTeamHistory.Add(new OperationalProjectTeamHistory
        {
            OperationalProjectId = project.Id,
            EntityType = "Project",
            EntityId = project.Id,
            Action = "Created",
            SnapshotJson = "{}",
            ChangedByUserId = _managerId,
        });
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.OperationalProjectMembers
            .Where(item => item.OperationalProjectId == project.Id).ToListAsync());
        Assert.Empty(await _db.OperationalProjectAssignments
            .Where(item => item.OperationalProjectId == project.Id).ToListAsync());

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.DeleteAsync(project.Id, _managerId, false, null));

        Assert.NotNull(await _db.OperationalProjects.FindAsync(project.Id));
        Assert.NotEmpty(await _db.OperationalProjectTeamHistory
            .Where(item => item.OperationalProjectId == project.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenDependencyWinsRace_ReturnsConcurrencyConflict()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new OperationalProjectDeleteFailureInterceptor())
            .Options;
        await using var db = new AppDbContext(options);
        var user = new ApplicationUser
        {
            PhoneNumber = "0900100099",
            FullName = "Race manager",
            Email = "race.manager@example.com",
            PasswordHash = "x",
            Role = UserRole.USER,
            IsActive = true,
        };
        var customer = new Customer
        {
            Name = "Race customer",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.AddRange(user, customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = "PJ-RACE",
            Name = "Delete race",
            CustomerId = customer.Id,
            ProjectManagerUserId = user.Id,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var service = new OperationalProjectService(
            db,
            new LegacyProjectTeamSyncService(db),
            NullLogger<OperationalProjectService>.Instance);

        await Assert.ThrowsAsync<CrmConcurrencyException>(() =>
            service.DeleteAsync(project.Id, user.Id, false, null));
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

    [Fact]
    public async Task GetTimelineAsync_AggregatesMilestonesAcrossContractsInPlannedDateOrder()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var firstContract = new Contract
        {
            ContractNumber = "HD-OP-TL-1",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            Value = 1_000,
        };
        var secondContract = new Contract
        {
            ContractNumber = "HD-OP-TL-2",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            Value = 2_000,
        };
        _db.Contracts.AddRange(firstContract, secondContract);
        await _db.SaveChangesAsync();
        _db.ContractPaymentMilestones.AddRange(
            new ContractPaymentMilestone
            {
                ContractId = firstContract.Id,
                Order = 2,
                Name = "Later milestone",
                PercentValue = 25,
                DueDate = new DateTime(2026, 9, 1),
            },
            new ContractPaymentMilestone
            {
                ContractId = secondContract.Id,
                Order = 1,
                Name = "Paid milestone",
                PercentValue = 50,
                DueDate = new DateTime(2026, 8, 1),
                Status = PaymentMilestoneStatus.Paid,
                ActualPaymentDate = new DateTime(2026, 8, 30),
                Note = "Paid after customer acceptance",
            });
        await _db.SaveChangesAsync();

        var result = await _service.GetTimelineAsync(created.Id, _managerId, false);

        Assert.NotNull(result);
        Assert.Collection(
            result!,
            item =>
            {
                Assert.Equal(secondContract.Id, item.ContractId);
                Assert.Equal("HD-OP-TL-2", item.ContractNumber);
                Assert.Equal(1_000, item.Amount);
                Assert.Equal(new DateTime(2026, 8, 30), item.ActualDate);
                Assert.Equal("ContractPaymentMilestone", item.Source);
            },
            item =>
            {
                Assert.Equal(firstContract.Id, item.ContractId);
                Assert.Null(item.ActualDate);
            });
    }

    [Fact]
    public async Task GetTimelineAsync_ProjectOutsideCallerScope_ReturnsNull()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);

        var result = await _service.GetTimelineAsync(created.Id, _otherUserId, false);

        Assert.Null(result);
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

    private sealed class OperationalProjectDeleteFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<OperationalProject>()
                .Any(entry => entry.State == EntityState.Deleted) == true)
                throw new DbUpdateException("A concurrent dependency prevents deletion.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
