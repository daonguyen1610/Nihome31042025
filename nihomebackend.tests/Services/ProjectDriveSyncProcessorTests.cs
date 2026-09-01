using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectDriveSyncProcessorTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IProjectDocumentStorageService> storage = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IProjectDriveClaimLease> claimLease = new();
    private readonly Mock<IProjectDriveFolderService> folders = new();
    private readonly OperationalProject project;
    private readonly ProjectDriveFolder folder;
    private readonly ProjectDriveSyncProcessor processor;
    private readonly GoogleDriveOptions options = new() { Enabled = true, InstanceId = "test-instance" };

    public ProjectDriveSyncProcessorTests()
    {
        project = new OperationalProject { Code = "OP-002", Name = "Văn phòng", CustomerId = 10 };
        db.OperationalProjects.Add(project);
        db.SaveChanges();
        folder = new ProjectDriveFolder
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.DesignBasic,
            DriveFolderId = "folder-design",
            DriveWebViewLink = "https://drive.test/folder-design",
        };
        db.ProjectDriveFolders.Add(folder);
        db.SaveChanges();
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "integration@nicon.test", "root", "https://drive.test/root",
                true, false, false, true));
        claimLease.Setup(item => item.RunAsync(
                It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<Func<CancellationToken, Task<DriveUpload>>>(), It.IsAny<CancellationToken>()))
            .Returns((long _, Guid _, long _, Func<CancellationToken, Task<DriveUpload>> operation, CancellationToken ct) =>
                operation(ct));
        processor = new ProjectDriveSyncProcessor(db, storage.Object, drive.Object, folders.Object,
            new TestGoogleDriveSettingsStore(options), claimLease.Object,
            Mock.Of<IAuditLogger>(), NullLogger<ProjectDriveSyncProcessor>.Instance);
    }

    [Fact]
    public async Task Disabled_ClaimsAndReconciliationDoNothing()
    {
        var disabled = new ProjectDriveSyncProcessor(db, storage.Object, drive.Object, folders.Object,
            new TestGoogleDriveSettingsStore(new GoogleDriveOptions()), claimLease.Object,
            Mock.Of<IAuditLogger>(), NullLogger<ProjectDriveSyncProcessor>.Instance);

        Assert.False(await disabled.ProcessNextOutboundAsync());
        await disabled.ReconcileProjectAsync(project.Id);

        drive.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoDueWork_DoesNotCheckDriveConnection()
    {
        Assert.False(await processor.ProcessNextOutboundAsync());

        drive.Verify(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task NonWritableConnection_DoesNotConsumePendingAttempt(
        bool isFolder,
        bool isTrashed)
    {
        var document = AddPendingDocument();
        var nextAttemptAt = document.NextSyncAttemptAt;
        var updatedAt = document.UpdatedAt;
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "integration@nicon.test", "root", "https://drive.test/root",
                isFolder, isTrashed, false, false));

        Assert.False(await processor.ProcessNextOutboundAsync());

        var persisted = db.ProjectDocuments.Single(item => item.Id == document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, persisted.SyncStatus);
        Assert.Equal(0, persisted.SyncAttemptCount);
        Assert.Null(persisted.LastSyncAttemptAt);
        Assert.Equal(nextAttemptAt, persisted.NextSyncAttemptAt);
        Assert.Equal(updatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task UnavailableConnection_DoesNotConsumePendingAttempt()
    {
        var document = AddPendingDocument();
        var nextAttemptAt = document.NextSyncAttemptAt;
        var updatedAt = document.UpdatedAt;
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("credential details stay private"));

        Assert.False(await processor.ProcessNextOutboundAsync());

        var persisted = db.ProjectDocuments.Single(item => item.Id == document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, persisted.SyncStatus);
        Assert.Equal(0, persisted.SyncAttemptCount);
        Assert.Null(persisted.LastSyncAttemptAt);
        Assert.Equal(nextAttemptAt, persisted.NextSyncAttemptAt);
        Assert.Equal(updatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public void ClaimRules_EnforceAttemptLimitAndTokenGenerationFence()
    {
        var now = DateTime.UtcNow;
        var due = ProjectDriveSyncProcessor.IsDueForClaim(now).Compile();
        Assert.True(due(new ProjectDocument
        {
            SyncStatus = ProjectDocumentSyncStatus.Pending,
            SyncAttemptCount = 2,
            DesiredOperation = ProjectDocumentDesiredOperation.Upsert,
        }));
        Assert.False(due(new ProjectDocument
        {
            SyncStatus = ProjectDocumentSyncStatus.Pending,
            SyncAttemptCount = 3,
            DesiredOperation = ProjectDocumentDesiredOperation.Upsert,
        }));

        var token = Guid.NewGuid();
        var fence = ProjectDriveSyncProcessor.MatchesFence(8, token, 4).Compile();
        Assert.True(fence(new ProjectDocument { Id = 8, ClaimToken = token, Generation = 4 }));
        Assert.False(fence(new ProjectDocument { Id = 8, ClaimToken = token, Generation = 5 }));
        Assert.False(fence(new ProjectDocument { Id = 8, ClaimToken = Guid.NewGuid(), Generation = 4 }));
    }

    [Fact]
    public void ReplicaKey_IsStableAndIndependentFromGeneration()
    {
        Assert.Equal("project-document:42", ProjectDriveSyncProcessor.ReplicaKey(42));
        Assert.Equal(ProjectDriveSyncProcessor.ReplicaKey(42), ProjectDriveSyncProcessor.ReplicaKey(42));
    }

    private ProjectDocument AddPendingDocument()
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.DesignBasic,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceType = ProjectDocumentSourceType.ExistingManagedFile,
            SourceEntityType = "BasicDesignDoc",
            SourceSlot = "file",
            SourceRecordId = 1,
            LocalPath = "/files/design/basic/pending.pdf",
            OriginalFileName = "pending.pdf",
            ContentType = "application/pdf",
            Size = 10,
            Sha256 = "sha256",
            Generation = 1,
            DesiredOperation = ProjectDocumentDesiredOperation.Upsert,
            SyncStatus = ProjectDocumentSyncStatus.Pending,
            NextSyncAttemptAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.ProjectDocuments.Add(document);
        db.SaveChanges();
        return document;
    }

    [Fact]
    public async Task MoveToFolderIfNeededAsync_ExistingReplicaInWrongCategory_MovesOnce()
    {
        var document = new ProjectDocument
        {
            DriveFileId = "survey-file",
            DriveFolderId = "folder-crm",
        };
        var surveyFolder = new ProjectDriveFolder { DriveFolderId = "folder-survey" };

        await processor.MoveToFolderIfNeededAsync(document, surveyFolder, CancellationToken.None);
        document.DriveFolderId = surveyFolder.DriveFolderId;
        await processor.MoveToFolderIfNeededAsync(document, surveyFolder, CancellationToken.None);

        drive.Verify(item => item.MoveAsync("survey-file", "folder-survey", CancellationToken.None), Times.Once);
    }

    [Fact]
    public void Model_UsesRowVersionAndUniqueConflictObservationIdentity()
    {
        var document = db.Model.FindEntityType(typeof(ProjectDocument))!;
        var driveFolder = db.Model.FindEntityType(typeof(ProjectDriveFolder))!;

        Assert.True(document.FindProperty(nameof(ProjectDocument.RowVersion))!.IsConcurrencyToken);
        Assert.True(driveFolder.FindProperty(nameof(ProjectDriveFolder.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(document.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[]
            {
                nameof(ProjectDocument.ConflictWithDocumentId),
                nameof(ProjectDocument.ConflictObservedDriveFileId),
                nameof(ProjectDocument.ConflictObservedDriveVersion),
            }));
    }

    [Fact]
    public async Task PropagateSurveySuccessAsync_UpdatesLegacyMediaAndAggregate()
    {
        var (survey, media, document) = AddSurveySidecar();
        document.SyncAttemptCount = 1;
        document.LastSyncAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        db.SaveChanges();
        var completedAt = DateTime.UtcNow;

        await processor.PropagateSurveySuccessAsync(document, folder,
            new DriveUpload("survey-drive-file", "2", completedAt, "https://drive.test/survey-drive-file"),
            completedAt, CancellationToken.None);

        Assert.Equal(SurveyMediaSyncStatus.Synced, media.SyncStatus);
        Assert.Equal("survey-drive-file", media.DriveFileId);
        Assert.Equal(folder.DriveFolderId, media.DriveFolderId);
        Assert.Equal(folder.DriveWebViewLink, media.DriveFolderLink);
        Assert.Equal(1, media.SyncAttemptCount);
        Assert.Equal(completedAt, media.SyncedAt);
        Assert.Equal(SurveyDriveSyncStatus.Synced, survey.DriveSyncStatus);
    }

    [Fact]
    public async Task PropagateSurveyFailureAsync_UpdatesLegacyMediaAndAggregate()
    {
        var (survey, media, document) = AddSurveySidecar();
        document.SyncAttemptCount = 3;
        document.LastSyncAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        db.SaveChanges();
        var failedAt = DateTime.UtcNow;

        await processor.PropagateSurveyFailureAsync(document.Id, 3, true, "Drive unavailable", failedAt,
            CancellationToken.None);

        Assert.Equal(SurveyMediaSyncStatus.Failed, media.SyncStatus);
        Assert.Equal(3, media.SyncAttemptCount);
        Assert.Equal("Drive unavailable", media.SyncError);
        Assert.Null(media.NextSyncAttemptAt);
        Assert.Equal(document.LastSyncAttemptAt, media.LastSyncAttemptAt);
        Assert.Equal(SurveyDriveSyncStatus.Failed, survey.DriveSyncStatus);
        Assert.Equal("Drive unavailable", survey.DriveSyncError);
    }

    [Fact]
    public async Task PropagateSurveyDeleteAsync_ClearsLegacyDriveMetadataAndAggregate()
    {
        var (survey, media, document) = AddSurveySidecar();
        var syncedAt = DateTime.UtcNow.AddMinutes(-1);
        media.DriveFileId = "survey-drive-file";
        document.DriveFileId = media.DriveFileId;
        media.DriveFolderId = "survey-folder";
        media.DriveFolderLink = "https://drive.test/survey-folder";
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        media.SyncStartedAt = syncedAt.AddSeconds(-1);
        media.SyncedAt = syncedAt;
        survey.DriveSyncStatus = SurveyDriveSyncStatus.Synced;
        survey.LastSyncedAt = syncedAt;
        db.SaveChanges();

        await processor.PropagateSurveyDeleteAsync(document, CancellationToken.None);

        Assert.Null(media.DriveFileId);
        Assert.Null(media.DriveFolderId);
        Assert.Null(media.DriveFolderLink);
        Assert.Equal(SurveyMediaSyncStatus.Pending, media.SyncStatus);
        Assert.Null(media.SyncStartedAt);
        Assert.Null(media.SyncedAt);
        Assert.Equal(SurveyDriveSyncStatus.NotSynced, survey.DriveSyncStatus);
        Assert.Null(survey.LastSyncedAt);
        storage.Verify(item => item.DeleteOwned(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PropagateSurveyDeleteAsync_OldProjectCallback_DoesNotClearNewProjectMetadata()
    {
        var (survey, media, oldDocument) = AddSurveySidecar();
        var customer = new Customer { Name = "Moved survey", Type = CustomerType.Company };
        db.Customers.Add(customer);
        db.SaveChanges();
        var newProject = new OperationalProject { Code = "OP-MOVED", Name = "Moved", CustomerId = customer.Id };
        db.OperationalProjects.Add(newProject);
        db.SaveChanges();
        var opportunity = new Opportunity
        {
            Name = "Moved opportunity",
            CustomerId = customer.Id,
            OperationalProjectId = newProject.Id,
        };
        db.Opportunities.Add(opportunity);
        db.SaveChanges();
        survey.LinkedOpportunityId = opportunity.Id;
        media.DriveFileId = "new-project-file";
        media.DriveFolderId = "new-project-folder";
        media.DriveFolderLink = "https://drive.test/new-project-folder";
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        db.SaveChanges();

        await processor.PropagateSurveyDeleteAsync(oldDocument, CancellationToken.None);

        Assert.Equal("new-project-file", media.DriveFileId);
        Assert.Equal("new-project-folder", media.DriveFolderId);
        Assert.Equal(SurveyMediaSyncStatus.Synced, media.SyncStatus);
    }

    [Fact]
    public async Task PropagateSurveyDeleteAsync_UnlinkedLegacyUpload_DoesNotClearNewMetadata()
    {
        var (_, media, oldDocument) = AddSurveySidecar();
        oldDocument.DriveFileId = "old-project-file";
        media.DriveFileId = "new-legacy-file";
        media.DriveFolderId = "new-legacy-folder";
        media.DriveFolderLink = "https://drive.test/new-legacy-folder";
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        db.SaveChanges();

        await processor.PropagateSurveyDeleteAsync(oldDocument, CancellationToken.None);

        Assert.Equal("new-legacy-file", media.DriveFileId);
        Assert.Equal("new-legacy-folder", media.DriveFolderId);
        Assert.Equal(SurveyMediaSyncStatus.Synced, media.SyncStatus);
    }

    [Fact]
    public async Task ReconcileProjectAsync_KnownRemoteDeletion_QueuesRestorationWithNewGeneration()
    {
        var document = AddKnownDocument("remote-missing", "1", DateTime.UtcNow.AddMinutes(-5));
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Null(document.DriveFileId);
        Assert.Equal(2, document.Generation);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, document.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, document.SyncStatus);
    }

    [Fact]
    public async Task ReconcileProjectAsync_DrivePrimaryDeletion_MarksCatalogEntryDeleted()
    {
        var document = AddKnownDocument("drive-primary-missing", "1", DateTime.UtcNow.AddMinutes(-5));
        document.LocalPath = string.Empty;
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal(ProjectDocumentSyncStatus.Deleted, document.SyncStatus);
        Assert.Equal(ProjectDocumentDesiredOperation.None, document.DesiredOperation);
        Assert.Equal(1, document.Generation);
    }

    [Fact]
    public async Task ReconcileProjectAsync_DrivePrimaryModification_RefreshesMetadataWithoutConflict()
    {
        var originalModifiedAt = DateTime.UtcNow.AddMinutes(-10);
        var document = AddKnownDocument("drive-primary-edited", "1", originalModifiedAt);
        document.LocalPath = string.Empty;
        db.SaveChanges();
        var remote = Remote(document.DriveFileId!, "2", originalModifiedAt.AddMinutes(5)) with
        {
            Name = "edited-on-drive.pdf",
            Size = 9,
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal("edited-on-drive.pdf", document.OriginalFileName);
        Assert.Equal(remote.Version, document.DriveVersion);
        Assert.Equal(9, document.Size);
        Assert.Equal(string.Empty, document.Sha256);
        Assert.Equal(ProjectDocumentConflictState.None, document.ConflictState);
        Assert.Single(db.ProjectDocuments);
    }

    [Fact]
    public async Task ReconcileProjectAsync_DrivePrimaryMove_RebindsExistingCatalogRowToDestinationCategory()
    {
        var document = AddKnownDocument("drive-primary-moved", "1", DateTime.UtcNow.AddMinutes(-5));
        document.LocalPath = string.Empty;
        var destination = new ProjectDriveFolder
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.ConstructionAcceptance,
            DriveFolderId = "folder-acceptance",
        };
        db.ProjectDriveFolders.Add(destination);
        db.SaveChanges();
        var remote = Remote(document.DriveFileId!, "2", DateTime.UtcNow);
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        drive.Setup(item => item.ListChildrenAsync(destination.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Single(db.ProjectDocuments);
        Assert.Equal(ProjectDocumentCategory.ConstructionAcceptance, document.Category);
        Assert.Equal(destination.DriveFolderId, document.DriveFolderId);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, document.SyncStatus);
        Assert.Null(document.DeletedAt);
    }

    [Fact]
    public async Task ReconcileProjectAsync_IntentionalDeleteMissingRemotely_DoesNotQueueRestoration()
    {
        var document = AddKnownDocument("remote-delete", "1", DateTime.UtcNow.AddMinutes(-5));
        document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal("remote-delete", document.DriveFileId);
        Assert.Equal(1, document.Generation);
        Assert.Equal(ProjectDocumentDesiredOperation.Delete, document.DesiredOperation);
    }

    [Fact]
    public async Task ReconcileProjectAsync_ActiveClaimManagedReplica_DoesNotRebindOrImport()
    {
        var document = AddKnownDocument("old-remote", "1", DateTime.UtcNow.AddMinutes(-5));
        document.DriveFileId = null;
        document.SyncStatus = ProjectDocumentSyncStatus.Processing;
        document.ClaimToken = Guid.NewGuid();
        document.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);
        db.SaveChanges();
        var remote = Remote("recovered-remote", "2", DateTime.UtcNow) with
        {
            AppProperties = new Dictionary<string, string>
            {
                ["nihomeInstance"] = options.InstanceId,
                ["nihomeReplicaKey"] = ProjectDriveSyncProcessor.ReplicaKey(document.Id),
                ["nihomeGeneration"] = document.Generation.ToString(),
            },
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Single(db.ProjectDocuments);
        Assert.Null(document.DriveFileId);
        Assert.Equal(ProjectDocumentSyncStatus.Processing, document.SyncStatus);
        drive.Verify(item => item.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_UnclaimedManagedReplica_RebindsWithoutImportingDuplicate()
    {
        var document = AddKnownDocument("old-remote", "1", DateTime.UtcNow.AddMinutes(-5));
        document.DriveFileId = null;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        db.SaveChanges();
        var remote = Remote("recovered-remote", "2", DateTime.UtcNow) with
        {
            AppProperties = new Dictionary<string, string>
            {
                ["nihomeInstance"] = options.InstanceId,
                ["nihomeReplicaKey"] = ProjectDriveSyncProcessor.ReplicaKey(document.Id),
                ["nihomeGeneration"] = document.Generation.ToString(),
            },
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Single(db.ProjectDocuments);
        Assert.Equal(remote.Id, document.DriveFileId);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, document.SyncStatus);
    }

    [Fact]
    public async Task ReconcileProjectAsync_DuplicateManagedReplicas_KeepsBoundFileAndTrashesExtra()
    {
        var document = AddKnownDocument("bound-remote", "1", DateTime.UtcNow.AddMinutes(-5));
        var properties = new Dictionary<string, string>
        {
            ["nihomeInstance"] = options.InstanceId,
            ["nihomeReplicaKey"] = ProjectDriveSyncProcessor.ReplicaKey(document.Id),
            ["nihomeGeneration"] = document.Generation.ToString(),
        };
        var bound = Remote("bound-remote", "1", document.DriveModifiedAt!.Value) with
        {
            AppProperties = properties,
        };
        var duplicate = Remote("duplicate-remote", "1", document.DriveModifiedAt.Value) with
        {
            AppProperties = properties,
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate, bound]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal("bound-remote", document.DriveFileId);
        Assert.Single(db.ProjectDocuments);
        drive.Verify(item => item.DeleteAsync("duplicate-remote", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_UnknownRemoteFile_CatalogsMetadataInCurrentCategory()
    {
        var remote = Remote("remote-new", "1", DateTime.UtcNow);
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);
        await processor.ReconcileProjectAsync(project.Id);
        await processor.ReconcileProjectAsync(project.Id);

        var imported = Assert.Single(db.ProjectDocuments);
        Assert.Equal(folder.Category, imported.Category);
        Assert.Equal(ProjectDocumentOrigin.GoogleDrive, imported.Origin);
        Assert.Equal(remote.Id, imported.DriveFileId);
        Assert.Equal(string.Empty, imported.LocalPath);
        drive.Verify(item => item.DownloadAsync(remote.Id, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_RemoteModification_PreservesAuthoritativeAndCreatesConflictCopy()
    {
        var originalModifiedAt = DateTime.UtcNow.AddMinutes(-10);
        var authoritative = AddKnownDocument("remote-known", "1", originalModifiedAt);
        var remote = Remote(authoritative.DriveFileId!, "2", originalModifiedAt.AddMinutes(5));
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);
        await processor.ReconcileProjectAsync(project.Id);
        await processor.ReconcileProjectAsync(project.Id);

        var documents = db.ProjectDocuments.OrderBy(item => item.Id).ToList();
        Assert.Equal(2, documents.Count);
        Assert.Equal(authoritative.LocalPath, documents[0].LocalPath);
        Assert.Equal(ProjectDocumentConflictState.PendingConfirmation, documents[0].ConflictState);
        Assert.Null(documents[0].DriveFileId);
        Assert.Equal(2, documents[0].Generation);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, documents[0].DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, documents[0].SyncStatus);
        Assert.Equal(authoritative.Id, documents[1].ConflictWithDocumentId);
        Assert.Equal(remote.Id, documents[1].DriveFileId);
        Assert.Equal(remote.Version, documents[1].DriveVersion);
        Assert.Equal(remote.Link, documents[1].DriveWebViewLink);
        Assert.Equal(ProjectDocumentSyncStatus.Conflict, documents[1].SyncStatus);
    }

    [Fact]
    public async Task CanDeleteRemoteAsync_RemoteModification_PreservesConflictAndCancelsDelete()
    {
        var originalModifiedAt = DateTime.UtcNow.AddMinutes(-10);
        var authoritative = AddKnownDocument("remote-delete-edited", "1", originalModifiedAt);
        var token = Guid.NewGuid();
        authoritative.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        authoritative.SyncStatus = ProjectDocumentSyncStatus.Processing;
        authoritative.ClaimToken = token;
        authoritative.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync();
        var generation = authoritative.Generation;
        var remote = Remote(authoritative.DriveFileId!, "2", originalModifiedAt.AddMinutes(5));
        drive.Setup(item => item.GetMetadataAsync(authoritative.DriveFileId!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(remote);
        var canDelete = await processor.CanDeleteRemoteAsync(authoritative, token, generation, CancellationToken.None);

        Assert.False(canDelete);
        Assert.Equal(ProjectDocumentDesiredOperation.Upsert, authoritative.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, authoritative.SyncStatus);
        Assert.Equal(ProjectDocumentConflictState.PendingConfirmation, authoritative.ConflictState);
        Assert.Null(authoritative.ClaimToken);
        Assert.Equal(generation + 1, authoritative.Generation);
        var conflict = Assert.Single(db.ProjectDocuments.Where(item => item.ConflictWithDocumentId == authoritative.Id));
        Assert.Equal(remote.Version, conflict.ConflictObservedDriveVersion);
        drive.Verify(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_ActiveFolderLease_SkipsRemoteIo()
    {
        var token = Guid.NewGuid();
        folder.ReconciliationClaimToken = token;
        folder.ReconciliationClaimExpiresAt = DateTime.UtcNow.AddMinutes(2);
        db.SaveChanges();

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal(token, folder.ReconciliationClaimToken);
        drive.Verify(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_ActiveOutboundClaim_DoesNotMutateMissingBinding()
    {
        var document = AddKnownDocument("remote-active", "1", DateTime.UtcNow.AddMinutes(-5));
        document.SyncStatus = ProjectDocumentSyncStatus.Processing;
        document.ClaimToken = Guid.NewGuid();
        document.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal("remote-active", document.DriveFileId);
        Assert.Equal(1, document.Generation);
        Assert.Equal(ProjectDocumentSyncStatus.Processing, document.SyncStatus);
    }

    [Fact]
    public async Task ReconcileProjectAsync_OversizedMetadata_CatalogsWithoutDownloading()
    {
        var remote = Remote("remote-large", "1", DateTime.UtcNow) with
        {
            Size = ProjectDocumentStorageService.MaxFileSize + 1,
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([remote]);

        await processor.ReconcileProjectAsync(project.Id);

        var imported = Assert.Single(db.ProjectDocuments);
        Assert.Equal(remote.Id, imported.DriveFileId);
        Assert.Equal(remote.Size, imported.Size);
        Assert.True(imported.IsDownloadable);
        drive.Verify(item => item.DownloadAsync(remote.Id, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(item => item.StoreDriveImportAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long?>(), It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileProjectAsync_NativeWorkspaceFile_ImportsUnsupportedMetadataAndContinues()
    {
        var native = Remote("native-doc", "1", DateTime.UtcNow) with
        {
            Name = "Kế hoạch",
            MimeType = "application/vnd.google-apps.document",
            Size = null,
        };
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([native]);

        await processor.ReconcileProjectAsync(project.Id);

        var imported = Assert.Single(db.ProjectDocuments);
        Assert.False(imported.IsDownloadable);
        Assert.NotNull(imported.UnsupportedReason);
        Assert.Equal(native.Id, imported.DriveFileId);
        drive.Verify(item => item.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ProjectDocument AddKnownDocument(string driveFileId, string version, DateTime modifiedAt)
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.DesignBasic,
            LocalPath = $"/files/project-documents/{project.Id}/authoritative.pdf",
            OriginalFileName = "drawing.pdf",
            ContentType = "application/pdf",
            Size = 3,
            Sha256 = new string('a', 64),
            Generation = 1,
            DesiredOperation = ProjectDocumentDesiredOperation.None,
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            DriveFileId = driveFileId,
            DriveFolderId = folder.DriveFolderId,
            DriveVersion = version,
            DriveModifiedAt = modifiedAt,
        };
        db.ProjectDocuments.Add(document);
        db.SaveChanges();
        return document;
    }

    private (Survey Survey, SurveyMedia Media, ProjectDocument Document) AddSurveySidecar()
    {
        var survey = new Survey
        {
            Code = $"SV-{Guid.NewGuid():N}",
            Location = "Project sync status",
            SurveyDate = DateTime.UtcNow,
        };
        db.Surveys.Add(survey);
        db.SaveChanges();
        var media = new SurveyMedia
        {
            SurveyId = survey.Id,
            OriginalFileName = "survey.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            Extension = ".jpg",
            Size = 3,
            RelativePath = $"/files/survey-media/{survey.Id}/stored.jpg",
        };
        db.SurveyMedia.Add(media);
        db.SaveChanges();
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.Survey,
            SourceModule = ProjectDocumentSourceModule.Survey,
            SourceType = ProjectDocumentSourceType.ExistingManagedFile,
            SourceEntityType = "SurveyMedia",
            SourceSlot = SurveyMediaService.ProjectDocumentSlot,
            SourceRecordId = media.Id,
            LocalPath = media.RelativePath,
            OriginalFileName = media.OriginalFileName,
            ContentType = media.ContentType,
            Size = media.Size,
            Sha256 = new string('s', 64),
        };
        db.ProjectDocuments.Add(document);
        db.SaveChanges();
        return (survey, media, document);
    }

    private static DriveItem Remote(string id, string version, DateTime modifiedAt) => new(
        id, "drawing.pdf", "application/pdf", 3, version, modifiedAt,
        $"https://drive.test/{id}", new Dictionary<string, string>(), false);

    public void Dispose() => db.Dispose();
}
