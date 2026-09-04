using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class ProjectDocumentServiceTests : IDisposable
{
    private readonly FailingAppDbContext db;
    private readonly Mock<IProjectDocumentStorageService> storage = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IProjectDriveFolderService> folders = new();
    private readonly ProjectDocumentService service;
    private readonly OperationalProject project;

    public ProjectDocumentServiceTests()
    {
        db = new FailingAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        project = new OperationalProject { Code = "OP-001", Name = "Nhà mẫu", CustomerId = 10, ProjectManagerUserId = 7 };
        db.OperationalProjects.Add(project);
        db.SaveChanges();
        folders.Setup(item => item.EnsureAsync(project, It.IsAny<ProjectDocumentCategory>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationalProject _, ProjectDocumentCategory category, int? _, CancellationToken _) =>
                new ProjectDriveFolder
                {
                    OperationalProjectId = project.Id,
                    Category = category,
                    DriveFolderId = $"folder-{category}",
                });
        service = new ProjectDocumentService(db, storage.Object, drive.Object, folders.Object,
            new TestGoogleDriveSettingsStore(new GoogleDriveOptions { Enabled = true, InstanceId = "test" }));
    }

    [Fact]
    public async Task ListAsync_UserOutsideProjectScope_ReturnsNull()
    {
        Assert.Null(await service.ListAsync(project.Id, 99, false));
    }

    [Fact]
    public async Task UploadAsync_UnclassifiedCategory_RejectsBeforeWritingFile()
    {
        var request = new ProjectDocumentUploadRequest
        {
            File = FormFile("drawing", "drawing.pdf"),
            Category = ProjectDocumentCategory.Unclassified,
        };

        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() =>
            service.UploadAsync(project.Id, request, 7, false));
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadAsync_MetadataSaveFails_TrashUploadedDriveFile()
    {
        var stored = new StoredProjectDocument(
            string.Empty, "drawing.pdf", "application/pdf", 7, new string('a', 64));
        storage.Setup(item => item.InspectUploadAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        drive.Setup(item => item.UploadAsync("folder-DesignBasic", It.IsAny<string>(), 1, "drawing.pdf",
                "application/pdf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveUpload("drive-file"));
        db.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(project.Id,
            new ProjectDocumentUploadRequest { File = FormFile("drawing", "drawing.pdf"), Category = ProjectDocumentCategory.DesignBasic },
            7, false));

        drive.Verify(item => item.DeleteAsync("drive-file", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task UploadAndDownloadAsync_UsesDriveWithoutCreatingHostPath()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("drawing");
        storage.Setup(item => item.InspectUploadAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredProjectDocument(string.Empty, "drawing.pdf", "application/pdf", bytes.Length, new string('a', 64)));
        drive.Setup(item => item.UploadAsync("folder-DesignBasic", It.IsAny<string>(), 1, "drawing.pdf",
                "application/pdf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveUpload("drive-file", "1", DateTime.UtcNow, "https://drive.test/drive-file"));
        drive.Setup(item => item.DownloadAsync("drive-file", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<string, Stream, CancellationToken>(async (_, destination, cancellationToken) =>
                await destination.WriteAsync(bytes, cancellationToken));

        var uploaded = await service.UploadAsync(project.Id,
            new ProjectDocumentUploadRequest { File = FormFile("drawing", "drawing.pdf"), Category = ProjectDocumentCategory.DesignBasic },
            7, false);
        var download = await service.DownloadAsync(project.Id, uploaded!.Id, 7, false);
        await using var destination = new MemoryStream();
        await download!.WriteToAsync(destination, CancellationToken.None);

        Assert.Equal("Nicon", uploaded.Origin);
        Assert.Equal("Synced", uploaded.SyncStatus);
        Assert.Equal("None", uploaded.DesiredOperation);
        Assert.Equal(bytes, destination.ToArray());
        Assert.Equal(string.Empty, (await db.ProjectDocuments.SingleAsync()).LocalPath);
        storage.Verify(item => item.StoreAsync(It.IsAny<int>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_FailedBeforeMaximum_QueuesSameGeneration()
    {
        var document = AddDocument(ProjectDocumentCategory.DesignBasic);
        document.SyncStatus = ProjectDocumentSyncStatus.Failed;
        document.SyncAttemptCount = 2;
        await db.SaveChangesAsync();

        var result = await service.RetryAsync(project.Id, document.Id, 7, false);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.SyncStatus);
        Assert.Equal(1, result.Generation);
        Assert.Null(document.SyncError);
    }

    [Fact]
    public async Task RetryAsync_PendingBackoffBeforeMaximum_QueuesImmediately()
    {
        var document = AddDocument(ProjectDocumentCategory.DesignBasic);
        document.SyncAttemptCount = 2;
        document.NextSyncAttemptAt = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        var result = await service.RetryAsync(project.Id, document.Id, 7, false);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.SyncStatus);
        Assert.True(document.NextSyncAttemptAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task RetryAsync_TerminalFailure_RemainsNonRetryable()
    {
        var document = AddDocument(ProjectDocumentCategory.DesignBasic);
        document.SyncStatus = ProjectDocumentSyncStatus.Failed;
        document.SyncAttemptCount = ProjectDocumentService.MaxSyncAttempts;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() =>
            service.RetryAsync(project.Id, document.Id, 7, false));
    }

    [Fact]
    public async Task RetryAsync_ExhaustedDeleteFailure_ResetsAndQueuesManualRetry()
    {
        var document = AddDocument(ProjectDocumentCategory.DesignBasic);
        document.SourceType = ProjectDocumentSourceType.ExistingManagedFile;
        document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        document.SyncStatus = ProjectDocumentSyncStatus.Failed;
        document.SyncAttemptCount = ProjectDocumentService.MaxSyncAttempts;
        await db.SaveChangesAsync();

        var result = await service.RetryAsync(project.Id, document.Id, 7, false);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.SyncStatus);
        Assert.Equal(0, document.SyncAttemptCount);
    }

    [Fact]
    public async Task ClassifyAsync_DriveImport_QueuesNewGenerationInSelectedCategory()
    {
        var document = AddDocument(ProjectDocumentCategory.Unclassified);
        document.Origin = ProjectDocumentOrigin.GoogleDrive;
        document.SourceType = ProjectDocumentSourceType.GoogleDriveImport;
        document.SyncStatus = ProjectDocumentSyncStatus.Synced;
        document.LocalPath = string.Empty;
        document.DriveFileId = "drive-import";
        await db.SaveChangesAsync();

        var result = await service.ClassifyAsync(project.Id, document.Id,
            new ClassifyProjectDocumentRequest { Category = ProjectDocumentCategory.ConstructionAcceptance }, 7, false);

        Assert.NotNull(result);
        Assert.Equal("ConstructionAcceptance", result.Category);
        Assert.Equal("Synced", result.SyncStatus);
        Assert.Equal(1, result.Generation);
        drive.Verify(item => item.MoveAsync("drive-import", "folder-ConstructionAcceptance",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_MetadataSaveFails_MovesDriveFileBackToPreviousFolder()
    {
        var document = AddDocument(ProjectDocumentCategory.Unclassified);
        document.Origin = ProjectDocumentOrigin.GoogleDrive;
        document.SourceType = ProjectDocumentSourceType.GoogleDriveImport;
        document.SyncStatus = ProjectDocumentSyncStatus.Synced;
        document.LocalPath = string.Empty;
        document.DriveFileId = "drive-import";
        document.DriveFolderId = "folder-unclassified";
        await db.SaveChangesAsync();
        db.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ClassifyAsync(
            project.Id, document.Id,
            new ClassifyProjectDocumentRequest { Category = ProjectDocumentCategory.ConstructionAcceptance },
            7, false));

        drive.Verify(item => item.MoveAsync("drive-import", "folder-ConstructionAcceptance",
            It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("drive-import", "folder-unclassified",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ResolveConflictAsync_LeavesAuthoritativeUpsertPendingAndReturnsPairToNone()
    {
        var authoritative = AddDocument(ProjectDocumentCategory.DesignBasic);
        authoritative.ConflictState = ProjectDocumentConflictState.PendingConfirmation;
        authoritative.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
        authoritative.SyncStatus = ProjectDocumentSyncStatus.Pending;
        var remoteCopy = AddDocument(ProjectDocumentCategory.DesignBasic);
        remoteCopy.Origin = ProjectDocumentOrigin.GoogleDrive;
        remoteCopy.ConflictWithDocumentId = authoritative.Id;
        remoteCopy.ConflictState = ProjectDocumentConflictState.PendingConfirmation;
        remoteCopy.SyncStatus = ProjectDocumentSyncStatus.Conflict;
        await db.SaveChangesAsync();

        await service.ResolveConflictAsync(project.Id, remoteCopy.Id,
            new ResolveProjectDocumentConflictRequest { ConfirmKeepBoth = true }, 7, false);

        Assert.Equal(ProjectDocumentConflictState.None, authoritative.ConflictState);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, authoritative.SyncStatus);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, authoritative.DesiredOperation);
        Assert.Equal(ProjectDocumentConflictState.None, remoteCopy.ConflictState);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, remoteCopy.SyncStatus);
    }

    [Fact]
    public async Task DeleteAsync_ExistingManagedFile_RejectsWithoutChangingSidecar()
    {
        var document = AddDocument(ProjectDocumentCategory.DesignBasic);
        document.SourceType = ProjectDocumentSourceType.ExistingManagedFile;
        document.SourceModule = ProjectDocumentSourceModule.Design;
        document.SourceEntityType = "BasicDesignDoc";
        document.SourceSlot = "file";
        document.SourceRecordId = 42;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() =>
            service.DeleteAsync(project.Id, document.Id, 7, false));

        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, document.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, document.SyncStatus);
        Assert.Null(document.DeletedAt);
    }

    [Fact]
    public async Task StageExistingManagedFileAsync_IsIdempotentWithoutSavingAndRevivesDeletedRow()
    {
        var path = "/files/design/drawing.pdf";
        storage.Setup(item => item.InspectExistingAsync(ProjectDocumentSourceModule.Design, path, "drawing.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredProjectDocument(path, "drawing.pdf", "application/pdf", 7, new string('b', 64)));

        var first = await service.StageExistingManagedFileAsync(project.Id, ProjectDocumentCategory.DesignBasic,
            ProjectDocumentSourceModule.Design, "DesignDocument", "drawing", 42, path, "drawing.pdf", null, null, 7);
        var second = await service.StageExistingManagedFileAsync(project.Id, ProjectDocumentCategory.DesignBasic,
            ProjectDocumentSourceModule.Design, "DesignDocument", "drawing", 42, path, "drawing.pdf", null, null, 7);

        Assert.Same(first, second);
        Assert.Equal(EntityState.Added, db.Entry(first).State);
        Assert.True(await service.StageExistingManagedFileDeleteAsync(project.Id, ProjectDocumentSourceModule.Design,
            "DesignDocument", "drawing", 42, path, 7));
        Assert.Equal(ProjectDocumentDesiredOperation.Delete, first.DesiredOperation);
        first.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
        first.SyncStatus = ProjectDocumentSyncStatus.Pending;
        first.DeletedAt = null;
        await db.SaveChangesAsync();
        first.SyncStatus = ProjectDocumentSyncStatus.Deleted;
        first.DesiredOperation = ProjectDocumentDesiredOperation.None;
        await db.SaveChangesAsync();

        var revived = await service.StageExistingManagedFileAsync(project.Id, ProjectDocumentCategory.DesignBasic,
            ProjectDocumentSourceModule.Design, "DesignDocument", "drawing", 42, path, "drawing.pdf", null, null, 7);

        Assert.Same(first, revived);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, revived.SyncStatus);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, revived.DesiredOperation);
        Assert.Equal(2, revived.Generation);
    }

    [Fact]
    public async Task StageExistingManagedFilesMoveAsync_CreatesDestinationAndDeletesOldSidecarOnly()
    {
        var destination = new OperationalProject
        {
            Code = "OP-002",
            Name = "Nhà mẫu mới",
            CustomerId = project.CustomerId,
        };
        db.OperationalProjects.Add(destination);
        var path = "/files/design/drawing.pdf";
        var oldSidecar = AddDocument(ProjectDocumentCategory.DesignBasic);
        oldSidecar.SourceType = ProjectDocumentSourceType.ExistingManagedFile;
        oldSidecar.SourceModule = ProjectDocumentSourceModule.Design;
        oldSidecar.SourceEntityType = "BasicDesignDoc";
        oldSidecar.SourceSlot = "file";
        oldSidecar.SourceRecordId = 42;
        oldSidecar.LocalPath = path;
        oldSidecar.SyncStatus = ProjectDocumentSyncStatus.Synced;
        oldSidecar.DesiredOperation = ProjectDocumentDesiredOperation.None;
        await db.SaveChangesAsync();
        storage.Setup(item => item.InspectExistingAsync(
                ProjectDocumentSourceModule.Design, path, "drawing.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredProjectDocument(path, "drawing.pdf", "application/pdf", 7, new string('b', 64)));

        await service.StageExistingManagedFilesMoveAsync(project.Id, destination.Id,
        [
            new(ProjectDocumentCategory.DesignBasic, ProjectDocumentSourceModule.Design,
                "BasicDesignDoc", "file", 42, path, "drawing.pdf", project.CustomerId, null),
        ], 7);
        await db.SaveChangesAsync();

        var sidecars = await db.ProjectDocuments.OrderBy(item => item.OperationalProjectId).ToListAsync();
        Assert.Equal(2, sidecars.Count);
        Assert.Equal(ProjectDocumentDesiredOperation.Delete, sidecars.Single(item =>
            item.OperationalProjectId == project.Id).DesiredOperation);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, sidecars.Single(item =>
            item.OperationalProjectId == destination.Id).DesiredOperation);
        Assert.All(sidecars, item => Assert.Equal(path, item.LocalPath));
        storage.Verify(item => item.DeleteOwned(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    private ProjectDocument AddDocument(ProjectDocumentCategory category)
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = category,
            LocalPath = $"/files/project-documents/{project.Id}/{Guid.NewGuid():N}.pdf",
            OriginalFileName = "drawing.pdf",
            ContentType = "application/pdf",
            Size = 7,
            Sha256 = new string('a', 64),
            Generation = 1,
            SyncStatus = ProjectDocumentSyncStatus.Pending,
        };
        db.ProjectDocuments.Add(document);
        db.SaveChanges();
        return document;
    }

    private static FormFile FormFile(string content, string fileName)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    public void Dispose() => db.Dispose();

    private sealed class FailingAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public bool FailNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("Forced metadata persistence failure.");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
