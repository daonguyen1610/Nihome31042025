using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class SurveyServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SurveyService _sut;
    private readonly Mock<IProjectDocumentStagingService> _projectDocuments = new();
    private readonly int _userId;
    private readonly int _projectId;

    public SurveyServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new SurveyService(_db, NullLogger<SurveyService>.Instance, _projectDocuments.Object);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000010",
            FullName = "Surveyor Tester",
            Email = "surveyor.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        var customer = new Customer { Name = "Survey test customer", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        var project = new OperationalProject
        {
            Code = "OP-SURVEY-TEST",
            Name = "Survey test project",
            CustomerId = customer.Id,
            ProjectManagerUserId = user.Id,
        };
        _db.OperationalProjects.Add(project);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "construction_type", Code = "residential", Name = "Nhà ở dân dụng", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "construction_type", Code = "commercial", Name = "Thương mại", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "construction_type", Code = "retired", Name = "Đã ẩn", IsActive = false, SortOrder = 9 }
        );
        _db.SaveChanges();
        _userId = user.Id;
        _projectId = project.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateSurveyRequest ValidCreate(string? location = null,
        string? typeCode = "residential",
        DateTime? date = null) => new()
        {
            Location = location ?? "Số 12 Nguyễn Trãi, Q. Thanh Xuân, Hà Nội",
            ConstructionTypeCode = typeCode,
            SurveyDate = date ?? DateTime.UtcNow.AddDays(-2),
            SurveyorUserId = _userId,
            OperationalProjectId = _projectId,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesCodeAndLabel()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith($"SV-{DateTime.UtcNow.Year}-", resp.Code);
        Assert.EndsWith("-0001", resp.Code);
        Assert.Equal("Nhà ở dân dụng", resp.ConstructionTypeLabel);
        Assert.Equal("NotSynced", resp.DriveSyncStatus);
    }

    [Fact]
    public async Task CreateAsync_MissingLocation_Throws()
    {
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(location: "  "), _userId));
    }

    [Fact]
    public async Task CreateAsync_MissingOperationalProject_ThrowsActionableError()
    {
        var request = ValidCreate();
        request.OperationalProjectId = null;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(request, _userId));

        Assert.Contains("Dự án vận hành", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_OpportunityProjectMismatch_Throws()
    {
        var otherProject = new OperationalProject
        {
            Code = "OP-SURVEY-OTHER",
            Name = "Other survey project",
            CustomerId = (await _db.OperationalProjects.FindAsync(_projectId))!.CustomerId,
        };
        _db.OperationalProjects.Add(otherProject);
        await _db.SaveChangesAsync();
        var opportunity = new Opportunity
        {
            Name = "Mismatched opportunity",
            CustomerId = otherProject.CustomerId,
            OperationalProjectId = otherProject.Id,
        };
        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.LinkedOpportunityId = opportunity.Id;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(request, _userId));

        Assert.Contains("không khớp", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_UnknownConstructionType_Throws()
    {
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(typeCode: "not-a-type"), _userId));
    }

    [Fact]
    public async Task CreateAsync_InactiveConstructionType_Throws()
    {
        // Master-data option exists but IsActive = false — should still 400.
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(ValidCreate(typeCode: "retired"), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownSurveyor_Throws()
    {
        var req = ValidCreate();
        req.SurveyorUserId = 99999;
        await Assert.ThrowsAsync<SurveyOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerYear()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.EndsWith("-0001", a.Code);
        Assert.EndsWith("-0002", b.Code);
    }

    // ---------------- Get / List ----------------

    [Fact]
    public async Task GetAsync_ResolvesConstructionLabel()
    {
        var created = await _sut.CreateAsync(ValidCreate(typeCode: "commercial"), _userId);
        var got = await _sut.GetAsync(created.Id, _userId, false);
        Assert.NotNull(got);
        Assert.Equal("Thương mại", got!.ConstructionTypeLabel);
    }

    [Fact]
    public async Task GetAsync_UnknownReturnsNull()
    {
        Assert.Null(await _sut.GetAsync(99999, _userId, false));
    }

    [Fact]
    public async Task ListAsync_DefaultsToSurveyDateDescending()
    {
        var older = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-30)), _userId);
        var newer = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-1)), _userId);

        var list = await _sut.ListAsync(new SurveyListParams { PageSize = 50 }, _userId, false);
        Assert.Equal(2, list.Total);
        Assert.Equal(newer.Id, list.Items[0].Id);
        Assert.Equal(older.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchAndConstructionType()
    {
        await _sut.CreateAsync(ValidCreate(location: "Alpha site"), _userId);
        await _sut.CreateAsync(ValidCreate(location: "Beta site", typeCode: "commercial"), _userId);

        var searched = await _sut.ListAsync(new SurveyListParams { Search = "Alpha" }, _userId, false);
        Assert.Single(searched.Items);
        Assert.Contains("Alpha", searched.Items[0].Location);

        var byType = await _sut.ListAsync(
            new SurveyListParams { ConstructionTypeCode = "commercial" }, _userId, false);
        Assert.Single(byType.Items);
        Assert.Equal("commercial", byType.Items[0].ConstructionTypeCode);
    }

    [Fact]
    public async Task ListAsync_FiltersByDateRange()
    {
        await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-60)), _userId);
        var inside = await _sut.CreateAsync(ValidCreate(date: DateTime.UtcNow.AddDays(-5)), _userId);

        var list = await _sut.ListAsync(new SurveyListParams
        {
            DateFrom = DateTime.UtcNow.AddDays(-10),
            DateTo = DateTime.UtcNow,
        }, _userId, false);
        Assert.Single(list.Items);
        Assert.Equal(inside.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByDriveStatusCsv()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(), _userId);
        var rawA = await _db.Surveys.FirstAsync(s => s.Id == a.Id);
        rawA.DriveSyncStatus = SurveyDriveSyncStatus.Synced;
        var rawB = await _db.Surveys.FirstAsync(s => s.Id == b.Id);
        rawB.DriveSyncStatus = SurveyDriveSyncStatus.Failed;
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(
            new SurveyListParams { DriveSyncStatus = "Synced,Failed" }, _userId, false);
        Assert.Equal(2, list.Total);

        var syncedOnly = await _sut.ListAsync(
            new SurveyListParams { DriveSyncStatus = "Synced" }, _userId, false);
        Assert.Single(syncedOnly.Items);
        Assert.Equal(a.Id, syncedOnly.Items[0].Id);
    }

    [Fact]
    public async Task ListAndGetAsync_ScopedUserCannotSeeUnassignedSurvey()
    {
        var otherUser = AddUser("0900000011", "other.surveyor@example.com");
        var otherProject = AddProject("OP-SURVEY-PRIVATE", otherUser.Id);
        var privateSurvey = AddSurvey("SV-PRIVATE", otherProject.Id, otherUser.Id, otherUser.Id);
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(new SurveyListParams { PageSize = 100 }, _userId, false);

        Assert.DoesNotContain(list.Items, item => item.Id == privateSurvey.Id);
        Assert.Null(await _sut.GetAsync(privateSurvey.Id, _userId, false));
        Assert.False(await _sut.CanAccessAsync(privateSurvey.Id, _userId, false));
    }

    [Fact]
    public async Task ListAndGetAsync_ViewAllUserCanSeeUnassignedSurvey()
    {
        var otherUser = AddUser("0900000012", "all.scope.owner@example.com");
        var otherProject = AddProject("OP-SURVEY-ALL", otherUser.Id);
        var privateSurvey = AddSurvey("SV-ALL", otherProject.Id, otherUser.Id, otherUser.Id);
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(new SurveyListParams { PageSize = 100 }, _userId, true);

        Assert.Contains(list.Items, item => item.Id == privateSurvey.Id);
        Assert.NotNull(await _sut.GetAsync(privateSurvey.Id, _userId, true));
        Assert.True(await _sut.CanAccessAsync(privateSurvey.Id, _userId, true));
    }

    [Fact]
    public async Task CreateAsync_ScopedUserCannotCreateInAnotherUsersProject()
    {
        var otherUser = AddUser("0900000013", "foreign.project@example.com");
        var otherProject = AddProject("OP-SURVEY-FOREIGN", otherUser.Id);
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.OperationalProjectId = otherProject.Id;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.CreateAsync(request, _userId));

        Assert.Contains("dự án do mình tạo hoặc phụ trách", exception.Message);
        Assert.False(await _db.Surveys.AnyAsync(survey => survey.OperationalProjectId == otherProject.Id));
    }

    [Fact]
    public async Task UpdateAsync_ScopedUserCannotMoveSurveyToAnotherUsersProject()
    {
        var created = await _sut.CreateAsync(ValidCreate(location: "Original scoped site"), _userId);
        var otherUser = AddUser("0900000014", "foreign.destination@example.com");
        var otherProject = AddProject("OP-SURVEY-DESTINATION", otherUser.Id);
        await _db.SaveChangesAsync();
        var request = ValidUpdate(created.Id, location: "Must not move");
        request.OperationalProjectId = otherProject.Id;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, request, _userId));

        Assert.Contains("chuyển phiếu khảo sát", exception.Message);
        var persisted = await _db.Surveys.AsNoTracking().SingleAsync(survey => survey.Id == created.Id);
        Assert.Equal(_projectId, persisted.OperationalProjectId);
        Assert.Equal("Original scoped site", persisted.Location);
    }

    [Fact]
    public async Task UpdateAsync_AssignedSurveyorCannotTransferAssignmentWithoutProjectLeadership()
    {
        var projectOwner = AddUser("0900000015", "project.owner@example.com");
        var replacement = AddUser("0900000016", "replacement.surveyor@example.com");
        var project = AddProject("OP-SURVEY-ASSIGNED", projectOwner.Id);
        var survey = AddSurvey("SV-ASSIGNED", project.Id, _userId, projectOwner.Id);
        await _db.SaveChangesAsync();
        var request = ValidUpdate(survey.Id, location: "Must not transfer", surveyorId: replacement.Id);
        request.OperationalProjectId = project.Id;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(survey.Id, request, _userId));

        Assert.Contains("phân công người khảo sát khác", exception.Message);
        var persisted = await _db.Surveys.AsNoTracking().SingleAsync(item => item.Id == survey.Id);
        Assert.Equal(_userId, persisted.SurveyorUserId);
        Assert.Equal("SV-ASSIGNED", persisted.Location);
    }

    // ---------------- NIH-100 Update / Delete ----------------

    private UpdateSurveyRequest ValidUpdate(int _1, string? location = null,
        string? typeCode = "commercial", DateTime? date = null,
        int? surveyorId = null, string? note = null) => new()
        {
            Location = location ?? "Địa điểm cập nhật",
            ConstructionTypeCode = typeCode,
            SurveyDate = date ?? DateTime.UtcNow.AddDays(-1),
            SurveyorUserId = surveyorId ?? _userId,
            OperationalProjectId = _projectId,
            Note = note,
        };

    [Fact]
    public async Task UpdateAsync_HappyPath_AppliesEveryEditableField()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var newDate = DateTime.UtcNow.AddDays(-3);
        var updated = await _sut.UpdateAsync(created.Id, ValidUpdate(
            created.Id,
            location: "Địa điểm mới",
            typeCode: "commercial",
            date: newDate,
            note: "Ghi chú"), _userId);

        Assert.NotNull(updated);
        Assert.Equal("Địa điểm mới", updated!.Location);
        Assert.Equal("commercial", updated.ConstructionTypeCode);
        Assert.Equal("Thương mại", updated.ConstructionTypeLabel);
        Assert.Equal(newDate, updated.SurveyDate, TimeSpan.FromSeconds(1));
        Assert.Equal("Ghi chú", updated.Note);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var res = await _sut.UpdateAsync(99999, ValidUpdate(0), _userId);
        Assert.Null(res);
    }

    [Fact]
    public async Task UpdateAsync_MissingLocation_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, location: "  "), _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownConstructionType_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, typeCode: "no-such-type"), _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownSurveyor_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, ValidUpdate(created.Id, surveyorId: 99999), _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownLinkedProject_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var req = ValidUpdate(created.Id);
        req.LinkedProjectId = 99999;
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, req, _userId));
    }

    [Fact]
    public async Task UpdateAsync_UnknownLinkedOpportunity_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var req = ValidUpdate(created.Id);
        req.LinkedOpportunityId = 99999;
        await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, req, _userId));
    }

    [Fact]
    public async Task UpdateAsync_ClearsOptionalFields_WhenNullPassed()
    {
        // Seed with a construction type + note so we can prove they are
        // cleared by a subsequent update that omits both.
        var initial = ValidCreate();
        initial.Note = "some note";
        var created = await _sut.CreateAsync(initial, _userId);
        Assert.Equal("residential", created.ConstructionTypeCode);
        Assert.Equal("some note", created.Note);

        var updated = await _sut.UpdateAsync(created.Id, new UpdateSurveyRequest
        {
            Location = created.Location,
            SurveyDate = created.SurveyDate,
            ConstructionTypeCode = null,
            SurveyorUserId = null,
            Note = null,
        }, _userId);

        Assert.NotNull(updated);
        Assert.Null(updated!.ConstructionTypeCode);
        Assert.Null(updated.SurveyorUserId);
        Assert.Null(updated.Note);
    }

    [Fact]
    public async Task UpdateAsync_ExplicitOpportunityUnlink_PreservesPersistedProjectRouting()
    {
        var customer = new Customer { Name = "Survey customer", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = "OP-SURVEY",
            Name = "Survey",
            CustomerId = customer.Id,
            ProjectManagerUserId = _userId,
        };
        _db.OperationalProjects.Add(project);
        await _db.SaveChangesAsync();
        var opportunity = new Opportunity
        {
            Name = "Survey opportunity",
            CustomerId = customer.Id,
            OperationalProjectId = project.Id,
        };
        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();
        var create = ValidCreate();
        create.OperationalProjectId = project.Id;
        create.LinkedOpportunityId = opportunity.Id;
        var survey = await _sut.CreateAsync(create, _userId);
        var media = new SurveyMedia
        {
            SurveyId = survey.Id,
            OriginalFileName = "survey.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            Extension = ".jpg",
            Size = 10,
            RelativePath = $"/files/survey-media/{survey.Id}/stored.jpg",
            DriveFileId = "newer-drive-file",
            DriveFolderId = "old-folder",
            SyncStatus = SurveyMediaSyncStatus.Synced,
            SyncAttemptCount = 2,
        };
        _db.SurveyMedia.Add(media);
        await _db.SaveChangesAsync();

        var request = ValidUpdate(survey.Id);
        request.LinkedOpportunityId = null;
        request.OperationalProjectId = null;
        await _sut.UpdateAsync(survey.Id, request, _userId);

        Assert.Null((await _db.Surveys.FindAsync(survey.Id))!.LinkedOpportunityId);
        Assert.Equal(project.Id, (await _db.Surveys.FindAsync(survey.Id))!.OperationalProjectId);
        Assert.Equal(SurveyMediaSyncStatus.Synced, media.SyncStatus);
        _projectDocuments.Verify(staging => staging.StageExistingManagedFilesMoveAsync(
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<IReadOnlyCollection<ProjectDocumentMoveDescriptor>>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ChangingOperationalProject_StagesExistingMediaMove()
    {
        var customerId = (await _db.OperationalProjects.FindAsync(_projectId))!.CustomerId;
        var previousProject = new OperationalProject
        {
            Code = "OP-SURVEY-PREVIOUS",
            Name = "Previous survey project",
            CustomerId = customerId,
            ProjectManagerUserId = _userId,
        };
        _db.OperationalProjects.Add(previousProject);
        await _db.SaveChangesAsync();
        var survey = new Survey
        {
            Code = "SV-PROJECT-MOVE",
            Location = "Project move site",
            SurveyDate = DateTime.UtcNow,
            OperationalProjectId = previousProject.Id,
        };
        _db.Surveys.Add(survey);
        await _db.SaveChangesAsync();
        var media = new SurveyMedia
        {
            SurveyId = survey.Id,
            OriginalFileName = "move.jpg",
            StoredFileName = "move.jpg",
            ContentType = "image/jpeg",
            Extension = ".jpg",
            Size = 10,
            RelativePath = $"/files/survey-media/{survey.Id}/move.jpg",
        };
        _db.SurveyMedia.Add(media);
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(survey.Id, ValidUpdate(survey.Id), _userId);

        Assert.Equal(_projectId, survey.OperationalProjectId);
        _projectDocuments.Verify(staging => staging.StageExistingManagedFilesMoveAsync(
            previousProject.Id, _projectId,
            It.Is<IReadOnlyCollection<ProjectDocumentMoveDescriptor>>(files => files.Count == 1 &&
                files.Single().Category == ProjectDocumentCategory.Survey &&
                files.Single().SourceRecordId == media.Id),
            _userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ZeroOperationalProject_IsBlockedWithoutMutation()
    {
        var created = await _sut.CreateAsync(ValidCreate(location: "Original site"), _userId);
        var request = ValidUpdate(created.Id, location: "Must not be saved");
        request.OperationalProjectId = 0;

        var exception = await Assert.ThrowsAsync<SurveyOperationException>(() =>
            _sut.UpdateAsync(created.Id, request, _userId));

        Assert.Contains("Dự án vận hành là bắt buộc", exception.Message);
        Assert.Equal("Original site", (await _db.Surveys.FindAsync(created.Id))!.Location);
    }

    [Theory]
    [InlineData(SurveyDriveSyncStatus.NotSynced)]
    [InlineData(SurveyDriveSyncStatus.Syncing)]
    [InlineData(SurveyDriveSyncStatus.Synced)]
    [InlineData(SurveyDriveSyncStatus.Failed)]
    public async Task DeleteAsync_AnyDriveStatus_RemovesSurveyAndPreservesUser(SurveyDriveSyncStatus status)
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var raw = await _db.Surveys.FirstAsync(s => s.Id == created.Id);
        raw.DriveSyncStatus = status;
        await _db.SaveChangesAsync();

        Assert.True(await _sut.DeleteAsync(created.Id, _userId, false));
        Assert.False(await _db.Surveys.AnyAsync(s => s.Id == created.Id));
        Assert.True(await _db.Users.AnyAsync(u => u.Id == _userId));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(99999, _userId, false));
    }

    // ---------------- NIH-101 Timeline ----------------

    [Fact]
    public async Task GetTimelineAsync_ReturnsNullWhenMissing()
    {
        Assert.Null(await _sut.GetTimelineAsync(99999, 50, _userId, false));
    }

    [Fact]
    public async Task GetTimelineAsync_ReturnsSeededAuditRowsNewestFirst()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        _db.AuditLogs.AddRange(
            new AuditLog
            {
                AuditId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Action = "survey.update",
                ResourceType = "Survey",
                ResourceId = created.Id.ToString(),
                Message = "older",
            },
            new AuditLog
            {
                AuditId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                Action = "survey.create",
                ResourceType = "Survey",
                ResourceId = created.Id.ToString(),
                Message = "newer",
            });
        _db.SaveChanges();

        var events = await _sut.GetTimelineAsync(created.Id, 50, _userId, false);
        Assert.NotNull(events);
        Assert.Equal(2, events!.Count);
        Assert.Equal("newer", events[0].Message);
        Assert.Equal("older", events[1].Message);
    }

    private ApplicationUser AddUser(string phoneNumber, string email)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = email,
            Email = email,
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private OperationalProject AddProject(string code, int managerUserId)
    {
        var project = new OperationalProject
        {
            Code = code,
            Name = code,
            CustomerId = _db.OperationalProjects.Single(project => project.Id == _projectId).CustomerId,
            ProjectManagerUserId = managerUserId,
            CreatedByUserId = managerUserId,
        };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    private Survey AddSurvey(string code, int projectId, int surveyorUserId, int createdByUserId)
    {
        var survey = new Survey
        {
            Code = code,
            Location = code,
            SurveyDate = DateTime.UtcNow,
            OperationalProjectId = projectId,
            SurveyorUserId = surveyorUserId,
            CreatedByUserId = createdByUserId,
        };
        _db.Surveys.Add(survey);
        return survey;
    }
}
