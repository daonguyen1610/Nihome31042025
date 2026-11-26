using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

// The background check: a file that is gone or broken on Drive must not keep saying "Synced",
// and the check itself never removes a record: the operator decides what happens next.
public sealed partial class ProjectDriveSyncProcessorTests
{
    private const string ProblemKey = "operationalProjects.documents.driveProblem.";

    private ProjectDocument AddCheckableDocument(string driveFileId = "checked-file")
    {
        var document = AddKnownDocument(driveFileId, "1", DateTime.UtcNow.AddDays(-1));
        document.LocalPath = string.Empty;
        db.SaveChanges();
        return document;
    }

    private void DriveReturns(string fileId, DriveItem? item) =>
        drive.Setup(adapter => adapter.GetMetadataAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

    private static DriveItem Live(string id, long? size = 3, string? sha256 = null) =>
        Remote(id, "1", DateTime.UtcNow) with { Size = size, Sha256 = sha256 };

    private ProjectDocument Reload(ProjectDocument document)
    {
        db.Entry(document).Reload();
        return document;
    }

    private void HasServerCopy(bool exists)
    {
        var open = storage.Setup(item => item.OpenRead(It.IsAny<int>(), It.IsAny<string>()));
        if (exists) open.Returns(() => new MemoryStream([1, 2, 3]));
        else open.Throws(new FileNotFoundException());
    }

    private void VerifyAudited(string action) => audit.Verify(item => item.Log(
        It.Is<AuditEvent>(entry => entry.Action == action)), Times.Once);

    [Theory]
    [InlineData(true, "trashed")]
    [InlineData(false, "notFound")]
    public async Task FileCheck_GoneFromDrive_MarksMissingAndKeepsTheRecord(bool inTrash, string reason)
    {
        // The delete-sync setting is ON (the default): the check still only changes the status.
        Assert.True(options.DeleteWebRecordsOnDriveDelete);
        StoreAdminAlerts();
        var document = AddCheckableDocument();
        DriveReturns("checked-file", inTrash ? Live("checked-file") with { IsTrashed = true } : null);

        Assert.Equal(1, await processor.VerifyDriveFilesAsync());

        Reload(document);
        Assert.Equal(ProjectDocumentSyncStatus.Missing, document.SyncStatus);
        Assert.Equal(ProblemKey + reason, document.SyncError);
        Assert.Equal("checked-file", document.DriveFileId);
        Assert.Null(document.DeletedAt);
        Assert.NotNull(document.DriveCheckedAt);
        VerifyAudited("project-document.drive_missing");
        var alert = Assert.Single(db.Notifications);
        Assert.Equal($"/admin/operational-projects/{project.Id}", alert.LinkUrl);
        Assert.Contains("drawing.pdf", alert.Body);
        drive.Verify(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FileCheck_ManyLostFiles_RaiseOneAdminAlertPerDay()
    {
        StoreAdminAlerts();
        AddCheckableDocument("lost-1");
        AddCheckableDocument("lost-2");

        await processor.VerifyDriveFilesAsync();
        AddCheckableDocument("lost-3");
        await processor.VerifyDriveFilesAsync();

        Assert.Single(db.Notifications);
    }

    [Fact]
    public async Task FileCheck_HealthyFile_StaysSyncedUntilTheAdminIntervalPasses()
    {
        var document = AddCheckableDocument();
        DriveReturns("checked-file", Live("checked-file"));

        Assert.Equal(1, await processor.VerifyDriveFilesAsync());
        Assert.Equal(0, await processor.VerifyDriveFilesAsync());

        Reload(document);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, document.SyncStatus);
        document.DriveCheckedAt = DateTime.UtcNow.AddHours(-3);
        db.SaveChanges();
        Assert.Equal(0, await processor.VerifyDriveFilesAsync());
        options.FileCheckIntervalHours = 2;
        Assert.Equal(1, await processor.VerifyDriveFilesAsync());
    }

    [Theory]
    [InlineData(ProjectDocumentSyncStatus.Missing)]
    [InlineData(ProjectDocumentSyncStatus.Damaged)]
    public async Task FileCheck_FileBackAndIntact_ReturnsToSynced(ProjectDocumentSyncStatus problem)
    {
        var document = AddCheckableDocument();
        document.SyncStatus = problem;
        document.SyncError = ProblemKey + "trashed";
        db.SaveChanges();
        DriveReturns("checked-file", Live("checked-file"));

        await processor.VerifyDriveFilesAsync();

        Reload(document);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, document.SyncStatus);
        Assert.Null(document.SyncError);
        VerifyAudited("project-document.drive_recovered");
    }

    [Fact]
    public async Task FileCheck_EmptyFileOnDrive_IsDamaged()
    {
        var document = AddCheckableDocument();
        DriveReturns("checked-file", Live("checked-file", size: 0));

        await processor.VerifyDriveFilesAsync();

        Assert.Equal(ProjectDocumentSyncStatus.Damaged, Reload(document).SyncStatus);
        Assert.Equal(ProblemKey + "empty", document.SyncError);
        VerifyAudited("project-document.drive_damaged");
    }

    // In a linked folder, edits made on Drive are two-way sync and reconciliation owns them.
    [Theory]
    [InlineData("an-unlinked-folder", ProjectDocumentSyncStatus.Damaged)]
    [InlineData("folder-design", ProjectDocumentSyncStatus.Synced)]
    public async Task FileCheck_DifferentContent_IsDamagedOnlyOutsideLinkedFolders(
        string driveFolderId, ProjectDocumentSyncStatus expected)
    {
        var document = AddCheckableDocument();
        document.DriveFolderId = driveFolderId;
        db.SaveChanges();
        DriveReturns("checked-file", Live("checked-file", sha256: new string('b', 64)));

        await processor.VerifyDriveFilesAsync();

        Assert.Equal(expected, Reload(document).SyncStatus);
    }

    [Theory]
    [InlineData(true, ProjectDocumentSyncStatus.Pending)]
    [InlineData(false, ProjectDocumentSyncStatus.Missing)]
    public async Task FileCheck_SyncedButNeverUploaded_IsQueuedOrMissing(
        bool serverCopy, ProjectDocumentSyncStatus expected)
    {
        var document = AddCheckableDocument();
        document.DriveFileId = null;
        document.LocalPath = "/files/project-documents/never-uploaded.pdf";
        db.SaveChanges();
        HasServerCopy(serverCopy);

        await processor.VerifyDriveFilesAsync();

        Assert.Equal(expected, Reload(document).SyncStatus);
        if (serverCopy) Assert.Equal(ProjectDocumentDesiredOperation.Upsert, document.DesiredOperation);
        // Say what is known: it never reached Drive, so "deleted or no permission" would be a guess.
        else Assert.Equal(ProblemKey + "neverUploaded", document.SyncError);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("server")]
    [InlineData("forbidden")]
    public async Task FileCheck_DriveCannotAnswer_NeverChangesTheStatus(string failure)
    {
        var document = AddCheckableDocument();
        drive.Setup(item => item.GetMetadataAsync("checked-file", It.IsAny<CancellationToken>())).ThrowsAsync(failure switch
        {
            "timeout" => new TaskCanceledException("request timed out"),
            "server" => new HttpRequestException("503", null, System.Net.HttpStatusCode.ServiceUnavailable),
            _ => new Google.GoogleApiException("drive", "forbidden") { HttpStatusCode = System.Net.HttpStatusCode.Forbidden },
        });

        await processor.VerifyDriveFilesAsync();

        Assert.Equal(ProjectDocumentSyncStatus.Synced, Reload(document).SyncStatus);
        Assert.Null(document.SyncError);
        Assert.False(await processor.VerifyDocumentAsync(document.Id));
    }

    [Fact]
    public async Task FileCheck_DriveNotConnected_ChecksNothing()
    {
        var document = AddCheckableDocument();
        ConnectionCheckFails(new GoogleDriveReconnectRequiredException("revoked"));

        Assert.Equal(0, await processor.VerifyDriveFilesAsync());

        Assert.Equal(ProjectDocumentSyncStatus.Synced, Reload(document).SyncStatus);
        drive.Verify(item => item.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FileCheck_SkipsWorkThatIsQueuedClaimedOrDeleted()
    {
        var queued = AddCheckableDocument("queued");
        queued.SyncStatus = ProjectDocumentSyncStatus.Pending;
        queued.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
        var claimed = AddCheckableDocument("claimed");
        claimed.ClaimToken = Guid.NewGuid();
        claimed.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);
        var deleted = AddCheckableDocument("deleted");
        deleted.DeletedAt = DateTime.UtcNow;
        db.SaveChanges();

        Assert.Equal(0, await processor.VerifyDriveFilesAsync());
    }

    [Fact]
    public async Task FileCheck_OneNow_IgnoresTheInterval()
    {
        var document = AddCheckableDocument();
        document.DriveCheckedAt = DateTime.UtcNow;
        db.SaveChanges();
        DriveReturns("checked-file", null);

        Assert.True(await processor.VerifyDocumentAsync(document.Id));

        Assert.Equal(ProjectDocumentSyncStatus.Missing, Reload(document).SyncStatus);
    }

    [Fact]
    public async Task FileCheck_LostSurveyPhoto_ShowsFailedOnTheSurvey()
    {
        var (_, media, document) = AddSurveySidecar();
        document.SyncStatus = ProjectDocumentSyncStatus.Synced;
        document.DesiredOperation = ProjectDocumentDesiredOperation.None;
        document.DriveFileId = "survey-file";
        document.LocalPath = string.Empty;
        db.SaveChanges();
        DriveReturns("survey-file", null);

        await processor.VerifyDriveFilesAsync();

        db.Entry(media).Reload();
        Assert.Equal(SurveyMediaSyncStatus.Failed, media.SyncStatus);
    }

    // A trashed file can still be downloaded, so "it verifies" is not proof it is safe to drop the server copy.
    [Fact]
    public async Task LocalCleanup_TrashedReplica_KeepsTheServerCopyAndMarksMissing()
    {
        var document = AddKnownDocument("trashed-replica", "1", DateTime.UtcNow.AddMinutes(-5));
        DriveReturns("trashed-replica", Live("trashed-replica") with { IsTrashed = true });

        await processor.ProcessNextOutboundAsync();

        Reload(document);
        Assert.Equal(ProjectDocumentSyncStatus.Missing, document.SyncStatus);
        Assert.NotEqual(string.Empty, document.LocalPath);
        storage.Verify(item => item.DeleteOwned(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_FileListedAgain_ReturnsToSynced(bool drivePrimary)
    {
        var document = AddKnownDocument("restored", "1", DateTime.UtcNow.AddMinutes(-5));
        if (drivePrimary) document.LocalPath = string.Empty;
        document.SyncStatus = ProjectDocumentSyncStatus.Missing;
        document.SyncError = ProblemKey + "trashed";
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Remote("restored", "1", document.DriveModifiedAt!.Value)]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal(ProjectDocumentSyncStatus.Synced, Reload(document).SyncStatus);
        Assert.Null(document.SyncError);
    }

    [Fact]
    public async Task Reconcile_FileEmptiedOnDrive_IsDamagedAndKeepsTheRecordedSize()
    {
        var document = AddCheckableDocument("emptied");
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Remote("emptied", "2", DateTime.UtcNow) with { Size = 0 }]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal(ProjectDocumentSyncStatus.Damaged, Reload(document).SyncStatus);
        Assert.Equal(3, document.Size);
    }

    [Fact]
    public async Task Upload_IntoDeletedFolder_WaitsWithoutUsingAttemptsAndAlertsAdmins()
    {
        StoreAdminAlerts();
        var pending = AddPendingDocument();
        folders.Setup(item => item.EnsureAsync(It.IsAny<OperationalProject>(), It.IsAny<ProjectDocumentCategory>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProjectDriveFolderMissingException());

        await processor.ProcessNextOutboundAsync();

        var waiting = await LoadAsync(pending.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, waiting.SyncStatus);
        Assert.Equal(0, waiting.SyncAttemptCount);
        Assert.Equal(ProblemKey + "folderMissing", waiting.SyncError);
        var alert = Assert.Single(db.Notifications);
        Assert.Equal($"/admin/operational-projects/{project.Id}", alert.LinkUrl);
        Assert.NotEqual(StoppedTitle, alert.Title);
        drive.Verify(item => item.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FileCheck_LostWithItsFolder_FlagsTheFolderForRepair()
    {
        var document = AddCheckableDocument();
        DriveReturns("checked-file", Live("checked-file") with { IsTrashed = true });
        DriveReturns(folder.DriveFolderId, Live(folder.DriveFolderId) with { IsTrashed = true });

        await processor.VerifyDriveFilesAsync();

        Assert.Equal(ProjectDocumentSyncStatus.Missing, Reload(document).SyncStatus);
        db.Entry(folder).Reload();
        Assert.NotNull(folder.MissingSince);
    }

    [Fact]
    public async Task FileCheck_OneFileThatKeepsFailing_DoesNotStarveTheOthers()
    {
        var stuck = AddCheckableDocument("stuck");
        var other = AddCheckableDocument("other");
        drive.Setup(item => item.GetMetadataAsync("stuck", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("request timed out"));
        DriveReturns("other", null);

        await processor.VerifyDriveFilesAsync();
        await processor.VerifyDriveFilesAsync();

        Assert.Equal(ProjectDocumentSyncStatus.Synced, Reload(stuck).SyncStatus);
        Assert.Equal(ProjectDocumentSyncStatus.Missing, Reload(other).SyncStatus);
    }

    [Fact]
    public async Task FileCheck_RestoredFile_ClearsTheDeletedFolderFlag()
    {
        var document = AddCheckableDocument();
        document.SyncStatus = ProjectDocumentSyncStatus.Missing;
        folder.MissingSince = DateTime.UtcNow.AddHours(-1);
        db.SaveChanges();
        DriveReturns("checked-file", Live("checked-file"));

        await processor.VerifyDriveFilesAsync();

        db.Entry(folder).Reload();
        Assert.Null(folder.MissingSince);
    }

    // The local copy keeps this row out of "Drive primary", so reconciliation must not heal an emptied file.
    [Fact]
    public async Task Reconcile_DamagedFileThatIsStillEmpty_StaysDamaged()
    {
        var document = AddKnownDocument("still-empty", "1", DateTime.UtcNow.AddMinutes(-5));
        document.SyncStatus = ProjectDocumentSyncStatus.Damaged;
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Remote("still-empty", "1", document.DriveModifiedAt!.Value) with { Size = 0 }]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal(ProjectDocumentSyncStatus.Damaged, Reload(document).SyncStatus);
    }
}
