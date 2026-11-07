using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectAssignmentAttachmentServiceTests : IDisposable
{
    private const string StoredPath = "/files/business-documents/project-team/abc.pdf";
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IBusinessDocumentStorageService> _storage = new();
    private readonly ProjectAssignmentAttachmentService _sut;
    private readonly int _projectId;
    private readonly int _otherProjectId;
    private readonly OperationalProjectAssignment _assignment;

    public ProjectAssignmentAttachmentServiceTests()
    {
        _db.Customers.Add(new Customer { Name = "Attachment customer", Type = CustomerType.Company });
        _db.SaveChanges();
        var customerId = _db.Customers.Single().Id;
        var project = AddProject(customerId, "OP-ATTACH");
        _otherProjectId = AddProject(customerId, "OP-ATTACH-OTHER").Id;
        _projectId = project.Id;
        var member = new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = 1,
            Position = "Architect",
            StartedAt = DateTime.UtcNow,
        };
        _db.OperationalProjectMembers.Add(member);
        _db.SaveChanges();
        _assignment = new OperationalProjectAssignment
        {
            OperationalProjectId = project.Id,
            WorkKey = "DES-ATTACH",
            Title = "Attachment assignment",
            Module = "Design",
            AssigneeMemberId = member.Id,
            Status = ProjectAssignmentStatus.Planned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.OperationalProjectAssignments.Add(_assignment);
        _db.SaveChanges();
        _storage.Setup(item => item.StoreAsync(
                It.IsAny<IFormFile>(), BusinessDocumentArea.ProjectTeam, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessDocumentUploadResponse
            {
                Path = StoredPath,
                OriginalFileName = "spec.pdf",
                FileSize = 3,
                ContentType = "application/pdf",
            });
        _storage.Setup(item => item.GetContent(BusinessDocumentArea.ProjectTeam, "abc.pdf"))
            .Returns(new ManagedDocumentContent("/tmp/abc.pdf", "abc.pdf", "application/pdf"));
        _storage.Setup(item => item.Delete(StoredPath, BusinessDocumentArea.ProjectTeam)).Returns(true);
        _sut = new ProjectAssignmentAttachmentService(
            _db, _storage.Object, NullLogger<ProjectAssignmentAttachmentService>.Instance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AggregatePlan_DisclosesAndQuarantinesAssignmentFiles(bool designScope)
    {
        var member = await _db.OperationalProjectMembers.FindAsync(_assignment.AssigneeMemberId);
        var design = new DesignProject
        {
            OperationalProjectId = _projectId,
            CustomerId = _db.Customers.Single().Id,
            ProjectCode = "DP-NICON-FILES",
            Name = "NICON – Hồ sơ thiết kế cơ điện",
        };
        if (designScope)
        {
            _db.DesignProjects.Add(design);
            await _db.SaveChangesAsync();
            member!.DesignProjectId = design.Id;
            await _db.SaveChangesAsync();
        }
        var attachment = new OperationalProjectAssignmentAttachment
        {
            AssignmentId = _assignment.Id,
            Path = StoredPath,
            OriginalFileName = "NICON – Biên bản khảo sát.pdf",
        };
        _db.OperationalProjectAssignmentAttachments.Add(attachment);
        await _db.SaveChangesAsync();
        var files = new Mock<NihomeBackend.Services.HardDelete.IHardDeleteFileService>();
        files.Setup(item => item.ValidateManagedPath(It.IsAny<string>())).Returns((string path) => path);
        var plans = new NihomeBackend.Services.HardDelete.ProjectHardDeletePlanService(_db,
            Mock.Of<NihomeBackend.Services.GoogleDrive.IGoogleDriveAdapter>(),
            new TestGoogleDriveSettingsStore(new NihomeBackend.Services.GoogleDrive.GoogleDriveOptions()), files.Object);
        var plan = designScope
            ? (await plans.ForDesignProjectAsync(design.Id))!
            : (await plans.ForOperationalProjectAsync(_projectId))!;

        Assert.Contains(plan.Impact.Items, item => item.Key == "operations.assignmentAttachments" &&
            item.Action == "Delete" && item.Count == 1);
        Assert.Contains(plan.Items, item => item.Kind == HardDeleteItemKind.LocalFile && item.ActionIdentifier == StoredPath);
        Assert.True(plan.Items.Single(item => item.Kind == HardDeleteItemKind.LocalFile).Sequence <
            plan.Items.Single(item => item.Kind == HardDeleteItemKind.DatabaseAggregate).Sequence);
        _db.OperationalProjectAssignmentAttachments.Add(new OperationalProjectAssignmentAttachment
        {
            AssignmentId = _assignment.Id,
            Path = "/files/business-documents/project-team/second.pdf",
            OriginalFileName = "NICON – Phụ lục.pdf",
        });
        await _db.SaveChangesAsync();
        var changed = designScope
            ? (await plans.ForDesignProjectAsync(design.Id))!
            : (await plans.ForOperationalProjectAsync(_projectId))!;
        Assert.NotEqual(plan.Impact.PlanToken, changed.Impact.PlanToken);
        Assert.Equal(2, await _db.OperationalProjectAssignmentAttachments.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AggregatePlan_UnsafeOrSharedPath_BlocksWithoutChangingReferences(bool shared)
    {
        var path = shared ? StoredPath : "/files/contracts/not-owned.pdf";
        _db.OperationalProjectAssignmentAttachments.Add(new OperationalProjectAssignmentAttachment
        {
            AssignmentId = _assignment.Id,
            Path = path,
            OriginalFileName = "NICON – Tệp cần kiểm tra.pdf",
        });
        if (shared)
        {
            var member = new OperationalProjectMember
            {
                OperationalProjectId = _otherProjectId,
                UserId = 1,
                Position = "Điều phối dự án khác",
                StartedAt = DateTime.UtcNow,
            };
            _db.OperationalProjectMembers.Add(member);
            await _db.SaveChangesAsync();
            var other = new OperationalProjectAssignment
            {
                OperationalProjectId = _otherProjectId,
                AssigneeMemberId = member.Id,
                WorkKey = "OTHER-DESIGN",
                Title = "Thiết kế dự án khác",
                Module = "Design",
            };
            _db.OperationalProjectAssignments.Add(other);
            await _db.SaveChangesAsync();
            _db.OperationalProjectAssignmentAttachments.Add(new OperationalProjectAssignmentAttachment
            {
                AssignmentId = other.Id,
                Path = path,
                OriginalFileName = "NICON – Tệp dùng chung.pdf",
            });
        }
        await _db.SaveChangesAsync();
        var files = new Mock<NihomeBackend.Services.HardDelete.IHardDeleteFileService>();
        files.Setup(item => item.ValidateManagedPath(It.IsAny<string>())).Returns((string value) => value);
        var plans = new NihomeBackend.Services.HardDelete.ProjectHardDeletePlanService(_db,
            Mock.Of<NihomeBackend.Services.GoogleDrive.IGoogleDriveAdapter>(),
            new TestGoogleDriveSettingsStore(new NihomeBackend.Services.GoogleDrive.GoogleDriveOptions()), files.Object);

        var plan = (await plans.ForOperationalProjectAsync(_projectId))!;

        Assert.False(plan.Impact.CanDelete);
        Assert.DoesNotContain(plan.Items, item => item.Kind == HardDeleteItemKind.LocalFile);
        Assert.Equal(shared ? 2 : 1, await _db.OperationalProjectAssignmentAttachments.CountAsync());
        Assert.True(await _db.OperationalProjectAssignments.AnyAsync(item => item.Id == _assignment.Id));
        files.Verify(item => item.QuarantineAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadListDownloadDelete_RoundTrip()
    {
        var uploaded = await _sut.UploadAsync(
            _projectId, _assignment.Id, null, FormFile(), callerId: 7, CancellationToken.None);

        Assert.NotNull(uploaded);
        Assert.Equal("spec.pdf", uploaded!.OriginalFileName);
        Assert.Equal("application/pdf", _db.OperationalProjectAssignmentAttachments.Single().ContentType);

        var listed = await _sut.ListAsync(_projectId, _assignment.Id, null, CancellationToken.None);
        Assert.Equal(uploaded.Id, Assert.Single(listed!).Id);

        var content = await _sut.DownloadAsync(
            _projectId, _assignment.Id, uploaded.Id, null, CancellationToken.None);
        Assert.NotNull(content);
        Assert.Equal("spec.pdf", content!.OriginalFileName);

        Assert.True(await _sut.DeleteAsync(
            _projectId, _assignment.Id, uploaded.Id, null, CancellationToken.None));
        _storage.Verify(item => item.Delete(StoredPath, BusinessDocumentArea.ProjectTeam), Times.Once);

        var attachment = await _db.OperationalProjectAssignmentAttachments.AsNoTracking().SingleAsync();
        Assert.NotNull(attachment.DeletedAt);
        Assert.NotNull(attachment.StorageDeletedAt);
        Assert.Empty((await _sut.ListAsync(_projectId, _assignment.Id, null, CancellationToken.None))!);
    }

    [Fact]
    public async Task UploadOrDelete_TerminalAssignment_IsRejectedWithoutTouchingStorage()
    {
        _assignment.Status = ProjectAssignmentStatus.Completed;
        await _db.SaveChangesAsync();
        var existing = new OperationalProjectAssignmentAttachment
        {
            AssignmentId = _assignment.Id,
            Path = StoredPath,
            OriginalFileName = "spec.pdf",
            FileSize = 3,
            ContentType = "application/pdf",
            CreatedAt = DateTime.UtcNow,
        };
        _db.OperationalProjectAssignmentAttachments.Add(existing);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() => _sut.UploadAsync(
            _projectId, _assignment.Id, null, FormFile(), callerId: 7, CancellationToken.None));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() => _sut.DeleteAsync(
            _projectId, _assignment.Id, existing.Id, null, CancellationToken.None));

        _storage.Verify(item => item.StoreAsync(
            It.IsAny<IFormFile>(), It.IsAny<BusinessDocumentArea>(), It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(item => item.Delete(
            It.IsAny<string?>(), It.IsAny<BusinessDocumentArea>()), Times.Never);
    }

    [Fact]
    public async Task Accessors_AssignmentFromAnotherProject_ReturnNull()
    {
        Assert.Null(await _sut.ListAsync(_otherProjectId, _assignment.Id, null, CancellationToken.None));
        Assert.Null(await _sut.UploadAsync(
            _otherProjectId, _assignment.Id, null, FormFile(), callerId: 7, CancellationToken.None));
        Assert.Null(await _sut.DownloadAsync(
            _otherProjectId, _assignment.Id, 1, null, CancellationToken.None));
        Assert.False(await _sut.DeleteAsync(
            _otherProjectId, _assignment.Id, 1, null, CancellationToken.None));
    }

    [Fact]
    public async Task Upload_EmptyOrNamelessFile_IsRejectedBeforeStorage()
    {
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() => _sut.UploadAsync(
            _projectId, _assignment.Id, null, null, callerId: 7, CancellationToken.None));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() => _sut.UploadAsync(
            _projectId, _assignment.Id, null,
            new FormFile(Stream.Null, 0, 0, "file", "empty.pdf"), callerId: 7, CancellationToken.None));

        _storage.Verify(item => item.StoreAsync(
            It.IsAny<IFormFile>(), It.IsAny<BusinessDocumentArea>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private OperationalProject AddProject(int customerId, string code)
    {
        var project = new OperationalProject { Code = code, Name = code, CustomerId = customerId };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    private static FormFile FormFile() =>
        new(new MemoryStream([1, 2, 3]), 0, 3, "file", "spec.pdf");

    public void Dispose() => _db.Dispose();
}
