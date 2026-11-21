using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public class OperationalProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IProjectDocumentStagingService> _projectDocuments = new();
    private readonly OperationalProjectService _service;
    private readonly int _managerId;
    private readonly int _otherUserId;
    private readonly int _customerId;

    public OperationalProjectServiceTests()
    {
        var hardDelete = HardDeleteTestServices.Create(_db, _projectDocuments.Object);
        _service = new OperationalProjectService(
            _db,
            new LegacyProjectTeamSyncService(_db),
            NullLogger<OperationalProjectService>.Instance,
            hardDelete.Plans,
            hardDelete.Operations);
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
    public async Task SuggestCodeAsync_ReturnsNextAutomaticCodeWithoutCreatingProject()
    {
        var suggestion = await _service.SuggestCodeAsync();

        Assert.Matches($"^PJ-{DateTime.UtcNow.Year}-\\d{{4}}$", suggestion);
        Assert.Empty(await _db.OperationalProjects.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_CustomCode_IsNormalizedAndPersisted()
    {
        var request = ValidCreate();
        request.Code = " db-2026-009 ";

        var result = await _service.CreateAsync(request, _managerId, false);

        Assert.Equal("DB-2026-009", result.Code);
        Assert.Equal("DB-2026-009", (await _db.OperationalProjects.SingleAsync()).Code);
    }

    [Fact]
    public async Task CreateAsync_CustomCodeAtMaximumLength_IsAccepted()
    {
        var request = ValidCreate();
        request.Code = new string('A', 40);

        var result = await _service.CreateAsync(request, _managerId, false);

        Assert.Equal(request.Code, result.Code);
    }

    [Fact]
    public async Task CreateAsync_CustomCodeAboveMaximumLength_IsRejected()
    {
        var request = ValidCreate();
        request.Code = new string('A', 41);

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(request, _managerId, false));

        Assert.Empty(await _db.OperationalProjects.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateCustomCode_IsRejectedWithoutCreatingAnotherProject()
    {
        var first = ValidCreate("First project");
        first.Code = "DB-2026-010";
        await _service.CreateAsync(first, _managerId, false);
        var duplicate = ValidCreate("Duplicate project");
        duplicate.Code = "db-2026-010";

        var error = await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(duplicate, _managerId, false));

        Assert.Contains("đã tồn tại", error.Message);
        Assert.Single(await _db.OperationalProjects.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("MÃ-2026-001")]
    [InlineData("DB/2026/001")]
    [InlineData("-DB-2026-001")]
    [InlineData("A")]
    public async Task CreateAsync_InvalidCustomCode_IsRejected(string code)
    {
        var request = ValidCreate();
        request.Code = code;

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(request, _managerId, false));

        Assert.Empty(await _db.OperationalProjects.ToListAsync());
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
            created.Id, new ConfirmDeletionRequest(), _otherUserId, false);

        Assert.Contains(list.Items, item => item.Id == created.Id);
        Assert.NotNull(detail);
        Assert.NotNull(timeline);
        Assert.Null(update);
        Assert.Null(delete);
    }

    [Fact]
    public async Task DeleteAsync_WithResponsibilityHistory_DeletesOwnedHistory()
    {
        var project = new OperationalProject
        {
            Code = "PJ-HISTORY-ONLY",
            Name = "History only",
            CustomerId = _customerId,
            ProjectManagerUserId = _managerId,
            CreatedByUserId = _managerId,
            UpdatedByUserId = _managerId,
            RowVersion = [1, 0, 0, 0, 0, 0, 0, 0],
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

        var impact = await _service.GetDeletionImpactAsync(project.Id, _managerId, false);
        var removed = await _service.DeleteAsync(
            project.Id, Confirm(impact!, CrmConcurrency.Encode(project.RowVersion)), _managerId, false);

        Assert.True(removed!.IsComplete);
        Assert.Null(await _db.OperationalProjects.FindAsync(project.Id));
        Assert.Empty(await _db.OperationalProjectTeamHistory
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
            RowVersion = [1, 0, 0, 0, 0, 0, 0, 0],
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var hardDelete = HardDeleteTestServices.Create(db, Mock.Of<IProjectDocumentStagingService>());
        var service = new OperationalProjectService(
            db,
            new LegacyProjectTeamSyncService(db),
            NullLogger<OperationalProjectService>.Instance,
            hardDelete.Plans,
            hardDelete.Operations);
        var impact = await service.GetDeletionImpactAsync(project.Id, user.Id, false);

        var result = await service.DeleteAsync(
            project.Id, Confirm(impact!, CrmConcurrency.Encode(project.RowVersion)), user.Id, false);

        Assert.Equal(HardDeleteOperationStatus.Failed, result!.Status);
        Assert.NotNull(await db.OperationalProjects.FindAsync(project.Id));
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
        var persisted = await _db.OperationalProjects.SingleAsync(item => item.Id == created.Id);
        persisted.Status = OperationalProjectStatus.Completed;
        await _db.SaveChangesAsync();
        var completed = await _service.GetAsync(created.Id, _managerId, false);

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.UpdateAsync(
                created.Id,
                ValidUpdate(completed!, OperationalProjectStatus.Active),
                _managerId,
                false));
    }

    [Fact]
    public async Task DeleteAsync_WithContract_UnlinksContractAndDeletesProject()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var project = await _db.OperationalProjects.FindAsync(created.Id);
        project!.RowVersion = [1, 0, 0, 0, 0, 0, 0, 0];
        _db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-2026-OP-1",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            Value = 100,
        });
        await _db.SaveChangesAsync();

        var impact = await _service.GetDeletionImpactAsync(created.Id, _managerId, false);
        var removed = await _service.DeleteAsync(
            created.Id, Confirm(impact!, CrmConcurrency.Encode(project.RowVersion)), _managerId, false);

        Assert.True(removed!.IsComplete);
        Assert.Null(await _db.OperationalProjects.FindAsync(created.Id));
        Assert.Null((await _db.Contracts.SingleAsync()).OperationalProjectId);
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

        var error = await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.UpdateAsync(created.Id, request, _managerId, false));

        // It says what blocks the change and how to fix a mistyped name.
        Assert.Equal("OPERATIONAL_PROJECT_CUSTOMER_IN_USE", error.Code);
        Assert.Equal(1, error.Parameters!["contracts"]);
        Assert.Contains("sửa tên tại Quản lý Khách hàng", error.Message);
    }

    [Fact]
    public async Task UpdateAsync_ProjectCreatedForTheWrongCustomer_MovesWithItsTeamAndDocuments()
    {
        // NICON feedback row 35: a project entered with the wrong customer, already staffed and
        // holding a document, but with nothing that belongs to the customer yet.
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        // Creating it already put its project manager on the team.
        Assert.True(await _db.OperationalProjectMembers.AnyAsync(item => item.OperationalProjectId == created.Id));
        var document = new ProjectDocument
        {
            OperationalProjectId = created.Id,
            CustomerId = _customerId,
            OriginalFileName = "khao-sat-ban-dau.pdf",
            LocalPath = "projects/khao-sat-ban-dau.pdf",
        };
        _db.ProjectDocuments.Add(document);
        var rightCustomer = new Customer
        {
            Name = "Công ty TNHH Thực phẩm Đức Hoà",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        _db.Customers.Add(rightCustomer);
        await _db.SaveChangesAsync();
        var request = ValidUpdate(created, OperationalProjectStatus.Planning);
        request.CustomerId = rightCustomer.Id;

        var updated = await _service.UpdateAsync(created.Id, request, _managerId, false);

        Assert.Equal(rightCustomer.Id, updated!.CustomerId);
        // Its documents are filed under the right customer too.
        Assert.Equal(rightCustomer.Id, (await _db.ProjectDocuments.SingleAsync(item => item.Id == document.Id)).CustomerId);
    }

    [Fact]
    public async Task RenameCodeAsync_NormalizesCodeAndCreatesAuditableDriveWork()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        _db.ProjectDriveFolders.Add(new ProjectDriveFolder
        {
            OperationalProjectId = created.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            DriveFolderId = "drive-design-folder",
        });
        var persistedProject = await _db.OperationalProjects.SingleAsync(item => item.Id == created.Id);
        persistedProject.RowVersion = BitConverter.GetBytes(1L);
        await _db.SaveChangesAsync();

        var result = await _service.RenameCodeAsync(created.Id, new RenameOperationalProjectRequest
        {
            NewCode = " db-2026-renamed ",
            Reason = "Đồng bộ mã theo phê duyệt của NICON",
            RowVersion = CrmConcurrency.Encode(persistedProject.RowVersion),
        }, _managerId, false);

        Assert.NotNull(result);
        Assert.Equal("DB-2026-RENAMED", result!.Code);
        Assert.Equal("Pending", result.LatestCodeChange!.Status);
        var history = await _db.OperationalProjectCodeChanges.SingleAsync();
        Assert.Equal(created.Code, history.OldCode);
        Assert.Equal("DB-2026-RENAMED", history.NewCode);
        Assert.Equal(_managerId, history.ChangedByUserId);
    }

    [Fact]
    public async Task ListAsync_FindsRenamedProjectByOldCode_WithinViewerScope()
    {
        var request = ValidCreate("Dự án dùng mã lịch sử");
        request.Code = "PJ-OLD-SEARCH";
        var created = await _service.CreateAsync(request, _managerId, false);
        var project = await _db.OperationalProjects.SingleAsync(item => item.Id == created.Id);
        project.RowVersion = BitConverter.GetBytes(1L);
        await _db.SaveChangesAsync();

        await _service.RenameCodeAsync(created.Id, new RenameOperationalProjectRequest
        {
            NewCode = "PJ-NEW-SEARCH",
            Reason = "Đổi mã theo quy ước của NICON",
            RowVersion = CrmConcurrency.Encode(project.RowVersion),
        }, _managerId, false);

        var ownResults = await _service.ListAsync(new OperationalProjectListParams
        {
            Search = "PJ-OLD-SEARCH",
        }, _managerId, false);
        var otherResults = await _service.ListAsync(new OperationalProjectListParams
        {
            Search = "PJ-OLD-SEARCH",
        }, _otherUserId, false);

        Assert.Single(ownResults.Items);
        Assert.Equal(created.Id, ownResults.Items[0].Id);
        Assert.Equal("PJ-NEW-SEARCH", ownResults.Items[0].Code);
        Assert.Empty(otherResults.Items);
    }

    [Fact]
    public async Task RenameCodeAsync_DuplicateCodeLeavesProjectUnchanged()
    {
        var first = ValidCreate("First project");
        first.Code = "PJ-RENAME-ONE";
        var firstProject = await _service.CreateAsync(first, _managerId, false);
        var second = ValidCreate("Second project");
        second.Code = "PJ-RENAME-TWO";
        var secondProject = await _service.CreateAsync(second, _managerId, false);

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.RenameCodeAsync(secondProject.Id, new RenameOperationalProjectRequest
            {
                NewCode = firstProject.Code,
                Reason = "Trùng mã để kiểm tra validation",
                RowVersion = secondProject.RowVersion,
            }, _managerId, false));

        Assert.Equal("PJ-RENAME-TWO",
            (await _db.OperationalProjects.SingleAsync(item => item.Id == secondProject.Id)).Code);
        Assert.Empty(await _db.OperationalProjectCodeChanges.ToListAsync());
    }

    [Theory]
    [InlineData(OperationalProjectStatus.Completed)]
    [InlineData(OperationalProjectStatus.Cancelled)]
    public async Task RenameCodeAsync_TerminalProjectRejectsWithoutMutation(
        OperationalProjectStatus status)
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var project = await _db.OperationalProjects.SingleAsync(item => item.Id == created.Id);
        project.Status = status;
        project.RowVersion = BitConverter.GetBytes(1L);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.RenameCodeAsync(created.Id, new RenameOperationalProjectRequest
            {
                NewCode = "PJ-CLOSED-RENAMED",
                Reason = "Không được đổi mã Dự án đã đóng",
                RowVersion = CrmConcurrency.Encode(project.RowVersion),
            }, _managerId, false));

        Assert.Equal(created.Code, project.Code);
        Assert.Empty(await _db.OperationalProjectCodeChanges.ToListAsync());
    }

    // NICON (Góp ý lập trình v2, answer 2): the code changes only before gate 3, the signed contract.
    // A purchase contract (Downstream) is not gate 3, and a cancelled contract no longer stands,
    // even one carrying a SignedDate (a draft may hold its planned date).
    [Theory]
    [InlineData(ContractDirection.Upstream, ContractStatus.Draft, false, false)]
    [InlineData(ContractDirection.Upstream, ContractStatus.Cancelled, false, false)]
    [InlineData(ContractDirection.Upstream, ContractStatus.Cancelled, true, false)]
    [InlineData(ContractDirection.Upstream, ContractStatus.Signed, true, true)]
    [InlineData(ContractDirection.Upstream, ContractStatus.InProgress, true, true)]
    [InlineData(ContractDirection.Upstream, ContractStatus.OnHold, true, true)]
    [InlineData(ContractDirection.Upstream, ContractStatus.Completed, true, true)]
    [InlineData(ContractDirection.Downstream, ContractStatus.Signed, true, false)]
    public async Task RenameCodeAsync_IsLockedOnceACustomerContractIsSigned(
        ContractDirection direction, ContractStatus contractStatus, bool wasSigned, bool locked)
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        var project = await _db.OperationalProjects.SingleAsync(item => item.Id == created.Id);
        project.RowVersion = BitConverter.GetBytes(1L);
        _db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-2026-GATE3",
            CustomerId = _customerId,
            OperationalProjectId = created.Id,
            Direction = direction,
            Status = contractStatus,
            SignedDate = wasSigned ? new DateTime(2026, 11, 1) : null,
        });
        await _db.SaveChangesAsync();

        Assert.Equal(locked, (await _service.GetAsync(created.Id, _managerId, false))!.CodeLocked);
        var rename = () => _service.RenameCodeAsync(created.Id, new RenameOperationalProjectRequest
        {
            NewCode = "PJ-2026-GATE3",
            Reason = "Sửa mã nhập nhầm",
            RowVersion = CrmConcurrency.Encode(project.RowVersion),
        }, _managerId, false);

        if (!locked)
        {
            Assert.Equal("PJ-2026-GATE3", (await rename())!.Code);
            return;
        }
        var error = await Assert.ThrowsAsync<OperationalProjectOperationException>(rename);
        Assert.Equal("OPERATIONAL_PROJECT_CODE_LOCKED", error.Code);
        Assert.Equal("HD-2026-GATE3", error.Parameters!["contract"]);
        Assert.Equal(created.Code, (await _db.OperationalProjects.AsNoTracking().SingleAsync(item => item.Id == created.Id)).Code);
        Assert.Empty(await _db.OperationalProjectCodeChanges.ToListAsync());
    }

    [Fact]
    public async Task RetryDriveRenameAsync_RejectsWorkAlreadyBeingProcessed()
    {
        var created = await _service.CreateAsync(ValidCreate(), _managerId, false);
        _db.OperationalProjectCodeChanges.Add(new OperationalProjectCodeChange
        {
            OperationalProjectId = created.Id,
            OldCode = created.Code,
            NewCode = "DB-2026-PROCESSING",
            Reason = "Kiểm tra chống xử lý trùng",
            ChangedByUserId = _managerId,
            DriveRenameStatus = ProjectDriveRenameStatus.Processing,
            NextDriveRenameAttemptAt = DateTime.UtcNow.AddMinutes(5),
        });
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.RetryDriveRenameAsync(created.Id, _managerId, false));

        Assert.Contains("đang được xử lý", exception.Message);
        Assert.Equal(ProjectDriveRenameStatus.Processing,
            (await _db.OperationalProjectCodeChanges.SingleAsync()).DriveRenameStatus);
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

    [Fact]
    public async Task SiteDetails_AreSaved_AndTheSectorDefaultsToTheCustomers()
    {
        // NICON feedback row 27: address, new build or renovation, sector and area.
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "business_sector", Code = "food-beverage", Name = "Thực phẩm & Đồ uống", IsActive = true },
            new MasterDataOption { Category = "business_sector", Code = "warehousing-logistics", Name = "Kho bãi & Logistics", IsActive = true });
        (await _db.Customers.FindAsync(_customerId))!.BusinessSectorCode = "food-beverage";
        await _db.SaveChangesAsync();

        // A quick create gives only a name: the sector is the customer's.
        var quick = await _service.CreateAsync(ValidCreate("Kho lạnh Đức Hoà"), _managerId, false);
        Assert.Equal("food-beverage", quick.BusinessSectorCode);
        Assert.Null(quick.SiteAddress);

        var full = ValidCreate("Kho trung chuyển Tân Uyên");
        full.SiteAddress = "  Lô C5, KCN Tân Uyên, Bình Dương ";
        full.ConstructionKind = ProjectConstructionKind.Renovation;
        full.BusinessSectorCode = "warehousing-logistics";
        full.SiteAreaM2 = 12500.5m;
        var created = await _service.CreateAsync(full, _managerId, false);
        Assert.Equal("Lô C5, KCN Tân Uyên, Bình Dương", created.SiteAddress);
        Assert.Equal(ProjectConstructionKind.Renovation, created.ConstructionKind);
        Assert.Equal("warehousing-logistics", created.BusinessSectorCode);
        Assert.Equal(12500.5m, created.SiteAreaM2);

        var unknown = ValidCreate("Dự án sai lĩnh vực");
        unknown.BusinessSectorCode = "khong-co";
        await Assert.ThrowsAsync<OperationalProjectOperationException>(() =>
            _service.CreateAsync(unknown, _managerId, false));
    }

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

    private static ConfirmDeletionRequest Confirm(
        DeletionImpactResponse impact,
        string? rowVersion = null) => new()
        {
            PlanToken = impact.PlanToken,
            Confirmation = impact.RequiredConfirmation,
            RowVersion = rowVersion,
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
