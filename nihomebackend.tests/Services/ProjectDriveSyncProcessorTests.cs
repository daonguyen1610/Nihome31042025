using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectDriveSyncProcessorTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.CreateRelational();
    private readonly Mock<IProjectDocumentStorageService> storage = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IProjectDriveClaimLease> claimLease = new();
    private readonly Mock<IProjectDriveFolderService> folders = new();
    private readonly Mock<INotificationService> notifications = new();
    private readonly OperationalProject project;
    private readonly ProjectDriveFolder folder;
    private readonly ProjectDriveSyncProcessor processor;
    private readonly GoogleDriveOptions options = new()
    {
        Enabled = true,
        InstanceId = "test-instance",
        RefreshToken = "stored-refresh-token",
    };
    private const string StoppedTitle = "Google Drive đã ngừng đồng bộ";

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
            Mock.Of<IAuditLogger>(), notifications.Object, NullLogger<ProjectDriveSyncProcessor>.Instance);
    }

    [Fact]
    public async Task ProjectRename_OlderRetryDoesNotRevertLatestCode()
    {
        project.Code = "NIC-CURRENT-C";
        project.Name = "NICON Factory";
        var rootName = "NIC-CURRENT-C_NICON Factory";
        var earlier = new OperationalProjectCodeChange
        {
            OperationalProjectId = project.Id,
            OldCode = "NIC-INITIAL-A",
            NewCode = "NIC-INTERMEDIATE-B",
            Reason = "Earlier confirmed project code change",
            ChangedByUserId = 7,
            ChangedAt = DateTime.UtcNow.AddHours(-2),
            DriveRenameStatus = ProjectDriveRenameStatus.Failed,
            DriveRenameAttemptCount = 1,
            NextDriveRenameAttemptAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.OperationalProjectCodeChanges.Add(earlier);
        db.OperationalProjectCodeChanges.Add(new OperationalProjectCodeChange
        {
            OperationalProjectId = project.Id,
            OldCode = "NIC-INTERMEDIATE-B",
            NewCode = project.Code,
            Reason = "Later confirmed project code change already synchronized",
            ChangedByUserId = 7,
            ChangedAt = DateTime.UtcNow.AddHours(-1),
            DriveRenameStatus = ProjectDriveRenameStatus.Succeeded,
            DriveRenameAttemptCount = 1,
            DriveRenamedAt = DateTime.UtcNow.AddMinutes(-30),
        });
        db.SaveChanges();
        drive.Setup(item => item.GetMetadataAsync("folder-design", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem("folder-design", "02_Thiet_ke",
                "application/vnd.google-apps.folder", null, null, null, null,
                new Dictionary<string, string>(), false, ["project-root"]));
        drive.Setup(item => item.GetMetadataAsync("project-root", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem("project-root", rootName,
                "application/vnd.google-apps.folder", null, null, null, null,
                new Dictionary<string, string>
                {
                    ["niconFolderKind"] = "project",
                    ["niconProjectId"] = project.Id.ToString(),
                }, false));
        drive.Setup(item => item.UpdateFileNameAsync("project-root",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, name, _) => rootName = name)
            .Returns(Task.CompletedTask);

        await processor.ProcessNextProjectRenameAsync();

        Assert.True(rootName == "NIC-CURRENT-C_NICON Factory",
            $"Old retry must preserve the latest confirmed project code. " +
            $"Database code={project.Code}; actual Drive root name={rootName}; " +
            $"earlier job status={earlier.DriveRenameStatus}.");
    }

    [Fact]
    public async Task Disabled_ClaimsAndReconciliationDoNothing()
    {
        var disabled = new ProjectDriveSyncProcessor(db, storage.Object, drive.Object, folders.Object,
            new TestGoogleDriveSettingsStore(new GoogleDriveOptions()), claimLease.Object,
            Mock.Of<IAuditLogger>(), notifications.Object, NullLogger<ProjectDriveSyncProcessor>.Instance);

        Assert.False(await disabled.ProcessNextProjectRenameAsync());
        Assert.False(await disabled.ProcessNextOutboundAsync());
        await disabled.ReconcileProjectAsync(project.Id);

        drive.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProjectRename_RenamesExistingRootAndKeepsFolderBinding()
    {
        project.Name = "Van phong NICON";
        var change = new OperationalProjectCodeChange
        {
            OperationalProjectId = project.Id,
            OldCode = project.Code,
            NewCode = "NIC-2026-002",
            Reason = "Chuẩn hóa mã dự án theo quyết định đã duyệt",
            ChangedByUserId = 7,
            DriveRenameStatus = ProjectDriveRenameStatus.Pending,
        };
        db.OperationalProjectCodeChanges.Add(change);
        project.Code = change.NewCode;
        db.SaveChanges();
        drive.Setup(item => item.GetMetadataAsync("folder-design", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem(
                "folder-design", "02_Thiet_ke", "application/vnd.google-apps.folder", null, null,
                null, null, new Dictionary<string, string>(), false, ["project-root"]));
        drive.Setup(item => item.GetMetadataAsync("project-root", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem(
                "project-root", "OP-002_Van phong NICON", "application/vnd.google-apps.folder", null,
                null, null, null, new Dictionary<string, string>
                {
                    ["niconFolderKind"] = "project",
                    ["niconProjectId"] = project.Id.ToString(),
                }, false));

        Assert.True(await processor.ProcessNextProjectRenameAsync());

        var persisted = db.OperationalProjectCodeChanges.Single(item => item.Id == change.Id);
        Assert.Equal(ProjectDriveRenameStatus.Succeeded, persisted.DriveRenameStatus);
        Assert.Equal(1, persisted.DriveRenameAttemptCount);
        Assert.NotNull(persisted.DriveRenamedAt);
        Assert.Equal("folder-design", db.ProjectDriveFolders.Single().DriveFolderId);
        drive.Verify(item => item.UpdateFileNameAsync(
            "project-root", "NIC-2026-002_Van phong NICON", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProjectRename_ActiveProcessingLease_IsNotClaimedAgain()
    {
        db.OperationalProjectCodeChanges.Add(new OperationalProjectCodeChange
        {
            OperationalProjectId = project.Id,
            OldCode = "OP-001",
            NewCode = project.Code,
            Reason = "Worker khác đang xử lý",
            ChangedByUserId = 7,
            DriveRenameStatus = ProjectDriveRenameStatus.Processing,
            NextDriveRenameAttemptAt = DateTime.UtcNow.AddMinutes(4),
        });
        db.SaveChanges();

        Assert.False(await processor.ProcessNextProjectRenameAsync());

        drive.Verify(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        drive.Verify(item => item.UpdateFileNameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProjectRename_DriveFailurePersistsRetryableStateWithoutChangingBinding()
    {
        var change = new OperationalProjectCodeChange
        {
            OperationalProjectId = project.Id,
            OldCode = "OP-001",
            NewCode = project.Code,
            Reason = "Kiểm tra lỗi Drive có thể retry",
            ChangedByUserId = 7,
            DriveRenameStatus = ProjectDriveRenameStatus.Pending,
        };
        db.OperationalProjectCodeChanges.Add(change);
        db.SaveChanges();
        drive.Setup(item => item.GetMetadataAsync("folder-design", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem(
                "folder-design", "02_Thiet_ke", "application/vnd.google-apps.folder", null, null,
                null, null, new Dictionary<string, string>(), false, ["project-root"]));
        drive.Setup(item => item.GetMetadataAsync("project-root", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveItem(
                "project-root", "OP-001_NICON", "application/vnd.google-apps.folder", null, null,
                null, null, new Dictionary<string, string>
                {
                    ["niconFolderKind"] = "project",
                    ["niconProjectId"] = project.Id.ToString(),
                }, false));
        drive.Setup(item => item.UpdateFileNameAsync(
                "project-root", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Drive temporarily unavailable"));

        Assert.True(await processor.ProcessNextProjectRenameAsync());

        var persisted = db.OperationalProjectCodeChanges.Single(item => item.Id == change.Id);
        Assert.Equal(ProjectDriveRenameStatus.Failed, persisted.DriveRenameStatus);
        Assert.Equal(1, persisted.DriveRenameAttemptCount);
        Assert.Contains("temporarily unavailable", persisted.DriveRenameError);
        Assert.True(persisted.NextDriveRenameAttemptAt > DateTime.UtcNow);
        Assert.Equal("folder-design", db.ProjectDriveFolders.Single().DriveFolderId);
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
    public async Task ProcessNextOutboundAsync_WebDeletePreservesDriveFileWhenDisabled()
    {
        options.DeleteDriveFilesOnWebDelete = false;
        var document = AddKnownDocument("remote-preserved", "1", DateTime.UtcNow.AddMinutes(-5));
        document.SourceType = ProjectDocumentSourceType.ExistingManagedFile;
        document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.NextSyncAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        db.SaveChanges();

        Assert.True(await processor.ProcessNextOutboundAsync());

        // The processor completes the delete with a set-based update, which does
        // not refresh the tracked instance, so read the persisted row instead.
        var persisted = await db.ProjectDocuments.AsNoTracking()
            .SingleAsync(item => item.Id == document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Deleted, persisted.SyncStatus);
        Assert.Null(persisted.DriveFileId);
        drive.Verify(item => item.DeleteAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        drive.Verify(item => item.CheckConnectionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task Timeout_does_not_use_up_an_attempt_and_retries_in_a_minute()
    {
        var document = AddPendingDocument();
        UploadThrows(new TaskCanceledException("request timed out"));

        for (var pass = 0; pass < 5; pass++)
        {
            await MakeDueAsync(document.Id);
            Assert.True(await processor.ProcessNextOutboundAsync());
        }

        var persisted = await LoadAsync(document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, persisted.SyncStatus);
        Assert.Equal(0, persisted.SyncAttemptCount);
        Assert.Equal("Google Drive tạm thời không phản hồi. Hệ thống sẽ tự thử lại.", persisted.SyncError);
        AssertRetryIn(TimeSpan.FromMinutes(1), persisted);
    }

    [Fact]
    public async Task Credential_failure_keeps_the_file_queued()
    {
        var document = AddPendingDocument();
        UploadThrows(new Google.Apis.Auth.OAuth2.Responses.TokenResponseException(
            new Google.Apis.Auth.OAuth2.Responses.TokenErrorResponse { Error = "invalid_grant" }));

        Assert.True(await processor.ProcessNextOutboundAsync());

        var persisted = await LoadAsync(document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, persisted.SyncStatus);
        Assert.Equal(0, persisted.SyncAttemptCount);
        Assert.Equal(
            "Google Drive cần kiểm tra cấu hình hoặc kết nối lại. Tệp sẽ tự đồng bộ sau khi kết nối lại.",
            persisted.SyncError);
        AssertRetryIn(TimeSpan.FromMinutes(5), persisted);
    }

    [Fact]
    public async Task Permanent_failure_still_fails_after_three_attempts_and_notifies_admins_once()
    {
        var document = AddPendingDocument();
        UploadThrows(new FileNotFoundException("source is gone"));

        for (var pass = 0; pass < 5; pass++)
        {
            await MakeDueAsync(document.Id);
            await processor.ProcessNextOutboundAsync();
        }

        var persisted = await LoadAsync(document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Failed, persisted.SyncStatus);
        Assert.Equal(3, persisted.SyncAttemptCount);
        const string error = "Không thể đồng bộ Google Drive. Vui lòng kiểm tra kết nối hoặc thử lại sau.";
        Assert.Equal(error, persisted.SyncError);
        notifications.Verify(item => item.CreateForAdminsAsync(
            "GoogleDrive", "Đồng bộ Google Drive thất bại: pending.pdf", $"OP-002 · {error}",
            $"/admin/operational-projects/{project.Id}"), Times.Once);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Retryable_failure_does_not_notify()
    {
        AddPendingDocument();
        UploadThrows(new HttpRequestException("network is down"));

        Assert.True(await processor.ProcessNextOutboundAsync());

        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Notification_error_does_not_undo_the_failed_state()
    {
        var document = AddPendingDocument();
        document.SyncAttemptCount = 2;
        db.SaveChanges();
        UploadThrows(new FileNotFoundException("source is gone"));
        notifications.Setup(item => item.CreateForAdminsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new DbUpdateException("notifications unavailable"));

        Assert.True(await processor.ProcessNextOutboundAsync());

        Assert.Equal(ProjectDocumentSyncStatus.Failed, (await LoadAsync(document.Id)).SyncStatus);
    }

    [Fact]
    public async Task Shutdown_cancellation_is_not_recorded_as_a_drive_failure()
    {
        var document = AddPendingDocument();
        using var shutdown = new CancellationTokenSource();
        UploadThrows(new OperationCanceledException(), shutdown.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.ProcessNextOutboundAsync(shutdown.Token));

        var persisted = await LoadAsync(document.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Processing, persisted.SyncStatus);
        Assert.Null(persisted.SyncError);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Survey_media_mirrors_a_failure_that_is_not_counted()
    {
        var (_, media, document) = AddSurveySidecar();
        UploadThrows(new TaskCanceledException("request timed out"));

        Assert.True(await processor.ProcessNextOutboundAsync());

        var persisted = await LoadAsync(document.Id);
        Assert.Equal(SurveyMediaSyncStatus.Pending, media.SyncStatus);
        Assert.Equal(0, media.SyncAttemptCount);
        Assert.Equal(persisted.NextSyncAttemptAt, media.NextSyncAttemptAt);
        Assert.Equal(persisted.SyncError, media.SyncError);
    }

    public static TheoryData<Exception?> FailuresOnlyAPersonCanFix() => new()
    {
        new Google.Apis.Auth.OAuth2.Responses.TokenResponseException(
            new Google.Apis.Auth.OAuth2.Responses.TokenErrorResponse { Error = "invalid_grant" }),
        new Google.GoogleApiException("Drive", "Drive API disabled")
        {
            HttpStatusCode = System.Net.HttpStatusCode.Forbidden,
        },
        null, // the check succeeds but the root is not a writable folder
    };

    [Theory]
    [MemberData(nameof(FailuresOnlyAPersonCanFix))]
    public async Task Stopped_sync_alerts_admins_once_across_passes(Exception? failure)
    {
        StoreAdminAlerts();
        AddPendingDocument();
        ConnectionCheckFails(failure);

        for (var pass = 0; pass < 3; pass++)
            Assert.False(await processor.ProcessNextOutboundAsync());

        var alert = Assert.Single(db.Notifications.AsNoTracking());
        Assert.Equal("GoogleDrive", alert.Module);
        Assert.Equal(StoppedTitle, alert.Title);
        Assert.Equal(
            "Cần kiểm tra cấu hình hoặc kết nối lại trong Cài đặt → Google Drive. Tệp đang chờ sẽ tự đồng bộ sau khi sửa.",
            alert.Body);
        Assert.Equal("/admin/settings?tab=drive", alert.LinkUrl);
    }

    [Theory]
    [InlineData(23, 1)]
    [InlineData(25, 2)]
    public async Task Stopped_sync_alert_repeats_only_after_24_hours(int hoursSinceLastAlert, int expectedAlerts)
    {
        StoreAdminAlerts();
        AddPendingDocument();
        ConnectionCheckFails(null);
        // Already stored by an earlier worker process.
        db.Notifications.Add(new Notification
        {
            UserId = 1,
            Module = "GoogleDrive",
            Title = StoppedTitle,
            CreatedAt = DateTime.UtcNow.AddHours(-hoursSinceLastAlert),
        });
        db.SaveChanges();

        await processor.ProcessNextOutboundAsync();

        Assert.Equal(expectedAlerts, db.Notifications.Count());
    }

    [Fact]
    public async Task Temporary_connection_failure_does_not_alert()
    {
        AddPendingDocument();
        ConnectionCheckFails(new TaskCanceledException("request timed out"));

        await processor.ProcessNextOutboundAsync();

        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Signed_out_drive_does_not_alert()
    {
        options.RefreshToken = string.Empty;
        AddPendingDocument();
        ConnectionCheckFails(new GoogleDriveReconnectRequiredException("not connected"));

        await processor.ProcessNextOutboundAsync();

        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_that_finds_the_drive_full_alerts_admins_once()
    {
        StoreAdminAlerts();
        var first = AddPendingDocument();
        UploadThrows(new Google.GoogleApiException("Drive", "storage full")
        {
            HttpStatusCode = System.Net.HttpStatusCode.Forbidden,
            Error = new Google.Apis.Requests.RequestError
            {
                Errors = [new Google.Apis.Requests.SingleError { Reason = "storageQuotaExceeded" }],
            },
        });

        for (var pass = 0; pass < 3; pass++)
        {
            await MakeDueAsync(first.Id);
            Assert.True(await processor.ProcessNextOutboundAsync());
        }

        Assert.Equal(StoppedTitle, Assert.Single(db.Notifications.AsNoTracking()).Title);
        Assert.Equal(ProjectDocumentSyncStatus.Pending, (await LoadAsync(first.Id)).SyncStatus);
    }

    [Fact]
    public async Task Alert_error_is_swallowed_and_leaves_nothing_unsaved()
    {
        AddPendingDocument();
        ConnectionCheckFails(null);
        notifications.Setup(item => item.CreateForAdminsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback(() => db.Notifications.Add(new Notification { UserId = 1, Title = StoppedTitle }))
            .ThrowsAsync(new DbUpdateException("notifications unavailable"));

        Assert.False(await processor.ProcessNextOutboundAsync());

        notifications.Verify(item => item.CreateForAdminsAsync(
            "GoogleDrive", StoppedTitle, It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        Assert.Empty(db.ChangeTracker.Entries<Notification>());
    }

    [Fact]
    public async Task Timed_out_replica_cleanup_is_deferred_and_does_not_block_uploads()
    {
        var replica = AddKnownDocument("remote-replica", "1", DateTime.UtcNow.AddMinutes(-5));
        replica.SourceType = ProjectDocumentSourceType.ManualUpload;
        replica.Origin = ProjectDocumentOrigin.Nicon;
        db.SaveChanges();
        var pending = AddPendingDocument();
        storage.Setup(item => item.StoreDriveImportAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<long>(), It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("readback timed out"));
        UploadThrows(new TaskCanceledException("request timed out"));

        Assert.True(await processor.ProcessNextOutboundAsync());

        var deferred = await LoadAsync(replica.Id);
        Assert.Equal("operationalProjects.documents.cleanupDeferred", deferred.SyncError);
        AssertRetryIn(TimeSpan.FromMinutes(5), deferred);
        Assert.NotNull((await LoadAsync(pending.Id)).LastSyncAttemptAt);
    }

    [Fact]
    public async Task Timed_out_folder_does_not_stop_reconciliation_of_the_others()
    {
        var bindings = AddReconciliationQueue(3);
        var visited = new List<string>();
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => visited.Add(id))
            .ThrowsAsync(new TaskCanceledException("request timed out"));

        await processor.ReconcileBoundProjectsAsync();

        Assert.Equal(bindings.Select(item => item.DriveFolderId), visited);
    }

    [Fact]
    public async Task Timed_out_project_does_not_stop_folder_initialization_of_the_others()
    {
        db.Add(new OperationalProject { Code = "NIC-FOLDER-1", Name = "NICON handoff", CustomerId = 10 });
        await db.SaveChangesAsync();
        var lastId = await db.OperationalProjects.MaxAsync(item => item.Id);
        folders.Setup(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("request timed out"));

        Assert.Equal(lastId, await processor.InitializeProjectFoldersAsync(0));

        folders.Verify(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Real notification rows, so the once-a-day rule is checked against what is stored.
    private void StoreAdminAlerts()
    {
        db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000001",
            Email = "admin@nicon.test",
            PasswordHash = "hash",
            Role = UserRole.ADMIN,
            IsActive = true,
        });
        db.SaveChanges();
        var stored = new NotificationService(db, Mock.Of<IEmailService>(), null!,
            NullLogger<NotificationService>.Instance);
        notifications.Setup(item => item.CreateForAdminsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string module, string title, string? body, string? link) =>
                stored.CreateForAdminsAsync(module, title, body, link));
    }

    private void ConnectionCheckFails(Exception? failure)
    {
        var check = drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()));
        if (failure is null)
            check.ReturnsAsync(new DriveConnection(
                "integration@nicon.test", "root", "https://drive.test/root", true, false, false, false));
        else
            check.ThrowsAsync(failure);
    }

    private void UploadThrows(Exception exception, Action? beforeThrow = null)
    {
        folders.Setup(item => item.EnsureAsync(It.IsAny<OperationalProject>(), It.IsAny<ProjectDocumentCategory>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        storage.Setup(item => item.OpenRead(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(() => new MemoryStream([1]));
        drive.Setup(item => item.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback(() => beforeThrow?.Invoke())
            .ThrowsAsync(exception);
    }

    private Task<int> MakeDueAsync(long documentId) =>
        db.ProjectDocuments.Where(item => item.Id == documentId && item.NextSyncAttemptAt != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.NextSyncAttemptAt, DateTime.UtcNow.AddMinutes(-1)));

    private Task<ProjectDocument> LoadAsync(long documentId) =>
        db.ProjectDocuments.AsNoTracking().SingleAsync(item => item.Id == documentId);

    private static void AssertRetryIn(TimeSpan delay, ProjectDocument document) =>
        Assert.InRange(document.NextSyncAttemptAt!.Value - DateTime.UtcNow,
            delay - TimeSpan.FromSeconds(30), delay);

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
        var codeChange = db.Model.FindEntityType(typeof(OperationalProjectCodeChange))!;

        Assert.True(document.FindProperty(nameof(ProjectDocument.RowVersion))!.IsConcurrencyToken);
        Assert.True(driveFolder.FindProperty(nameof(ProjectDriveFolder.RowVersion))!.IsConcurrencyToken);
        Assert.True(codeChange.FindProperty(nameof(OperationalProjectCodeChange.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(document.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[]
            {
                nameof(ProjectDocument.ConflictWithDocumentId),
                nameof(ProjectDocument.ConflictObservedDriveFileId),
                nameof(ProjectDocument.ConflictObservedDriveVersion),
            }));
    }

    [Fact]
    public async Task PropagateSurveySuccessAsync_UpdatesMediaAndAggregate()
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
    public async Task PropagateSurveyFailureAsync_UpdatesMediaAndAggregate()
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
    public async Task PropagateSurveyDeleteAsync_ClearsDriveMetadataAndAggregate()
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
        survey.OperationalProjectId = newProject.Id;
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
    public async Task PropagateSurveyDeleteAsync_StaleFileCallback_DoesNotClearNewMetadata()
    {
        var (_, media, oldDocument) = AddSurveySidecar();
        oldDocument.DriveFileId = "old-project-file";
        media.DriveFileId = "new-current-file";
        media.DriveFolderId = "new-current-folder";
        media.DriveFolderLink = "https://drive.test/new-current-folder";
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        db.SaveChanges();

        await processor.PropagateSurveyDeleteAsync(oldDocument, CancellationToken.None);

        Assert.Equal("new-current-file", media.DriveFileId);
        Assert.Equal("new-current-folder", media.DriveFolderId);
        Assert.Equal(SurveyMediaSyncStatus.Synced, media.SyncStatus);
    }

    [Fact]
    public async Task ReconcileProjectAsync_KnownRemoteDeletion_MarksWebRecordDeletedWhenEnabled()
    {
        var document = AddKnownDocument("remote-missing", "1", DateTime.UtcNow.AddMinutes(-5));
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Null(document.DriveFileId);
        Assert.Equal(1, document.Generation);
        Assert.Equal(ProjectDocumentDesiredOperation.None, document.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Deleted, document.SyncStatus);
        Assert.NotNull(document.DeletedAt);
    }

    [Fact]
    public async Task ReconcileProjectAsync_KnownRemoteDeletion_PreservesWebRecordWhenDisabled()
    {
        options.DeleteWebRecordsOnDriveDelete = false;
        var document = AddKnownDocument("remote-missing", "1", DateTime.UtcNow.AddMinutes(-5));
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileProjectAsync(project.Id);

        Assert.Equal("remote-missing", document.DriveFileId);
        Assert.Equal(ProjectDocumentDesiredOperation.None, document.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Synced, document.SyncStatus);
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
        Assert.Null(document.DriveFileId);
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
        drive.Setup(item => item.GetMetadataAsync(document.DriveFileId!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(remote);
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
    public async Task ReconcileProjectAsync_PreservedManagedReplica_DoesNotReviveDeletedWebRecord()
    {
        var document = AddKnownDocument("preserved-remote", "1", DateTime.UtcNow.AddMinutes(-5));
        document.DriveFileId = null;
        document.DriveWebViewLink = null;
        document.SyncStatus = ProjectDocumentSyncStatus.Deleted;
        document.DeletedAt = DateTime.UtcNow.AddMinutes(-1);
        db.SaveChanges();
        var remote = Remote("preserved-remote", "2", DateTime.UtcNow) with
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
        Assert.Equal(ProjectDocumentSyncStatus.Deleted, document.SyncStatus);
        Assert.NotNull(document.DeletedAt);
        Assert.Null(document.DriveFileId);
        drive.Verify(item => item.DeleteAsync(remote.Id, It.IsAny<CancellationToken>()), Times.Never);
        drive.Verify(item => item.DownloadAsync(remote.Id, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task ReconcileBoundProjectsAsync_FailedFirstBatchDoesNotStarveLaterProjects()
    {
        var bindings = AddReconciliationQueue(12);
        var visited = new List<string>();
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => visited.Add(id))
            .ThrowsAsync(new IOException("Remote folder temporarily unavailable"));

        await processor.ReconcileBoundProjectsAsync();

        Assert.Equal(bindings.Take(10).Select(item => item.DriveFolderId), visited);
        Assert.All(bindings.Take(10), item =>
        {
            Assert.Null(item.LastReconciledAt);
            Assert.Null(item.ReconciliationClaimToken);
            Assert.True(item.UpdatedAt > bindings[10].UpdatedAt);
        });
        visited.Clear();

        await processor.ReconcileBoundProjectsAsync();

        Assert.Equal(bindings.Skip(10).Select(item => item.DriveFolderId), visited.Take(2));
        Assert.Equal(10, visited.Count);
        Assert.Empty(db.ProjectDocuments);
        drive.Verify(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileBoundProjectsAsync_ActiveLeasesDoNotConsumeBatchCapacity()
    {
        var bindings = AddReconciliationQueue(12);
        var tokens = new Dictionary<long, Guid>();
        foreach (var binding in bindings.Take(10))
        {
            var token = Guid.NewGuid();
            tokens[binding.Id] = token;
            binding.ReconciliationClaimToken = token;
            binding.ReconciliationClaimExpiresAt = DateTime.UtcNow.AddMinutes(2);
        }
        db.SaveChanges();
        var visited = new List<string>();
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => visited.Add(id))
            .ReturnsAsync([]);

        await processor.ReconcileBoundProjectsAsync();

        Assert.Equal(bindings.Skip(10).Select(item => item.DriveFolderId), visited);
        Assert.All(bindings.Take(10), item => Assert.Equal(tokens[item.Id], item.ReconciliationClaimToken));
        Assert.All(bindings.Skip(10), item => Assert.NotNull(item.LastReconciledAt));
    }

    [Fact]
    public async Task ReconcileBoundProjectsAsync_SuccessfulBatchMovesToRemainingFolders()
    {
        var bindings = AddReconciliationQueue(12);
        var visited = new List<string>();
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => visited.Add(id))
            .ReturnsAsync([]);

        await processor.ReconcileBoundProjectsAsync();
        await processor.ReconcileBoundProjectsAsync();

        Assert.Equal(bindings.Select(item => item.DriveFolderId), visited);
        Assert.All(bindings, item => Assert.NotNull(item.LastReconciledAt));
        Assert.All(bindings, item => Assert.Null(item.ReconciliationClaimToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReconcileBoundProjectsAsync_ExpiredOrIncompleteLeaseCanRecover(bool hasExpiry)
    {
        folder.ReconciliationClaimToken = Guid.NewGuid();
        folder.ReconciliationClaimExpiresAt = hasExpiry ? DateTime.UtcNow.AddMinutes(-1) : null;
        db.SaveChanges();
        drive.Setup(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await processor.ReconcileBoundProjectsAsync();

        Assert.NotNull(folder.LastReconciledAt);
        Assert.Null(folder.ReconciliationClaimToken);
        Assert.Null(folder.ReconciliationClaimExpiresAt);
        drive.Verify(item => item.ListChildrenAsync(folder.DriveFolderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private List<ProjectDriveFolder> AddReconciliationQueue(int count)
    {
        var touchedAt = DateTime.UtcNow.AddHours(-1);
        folder.UpdatedAt = touchedAt;
        var bindings = new List<ProjectDriveFolder> { folder };
        for (var index = 1; index < count; index++)
        {
            var queuedProject = new OperationalProject
            {
                Code = $"NICON-DRIVE-QUEUE-{index:00}",
                Name = $"NICON Drive reconciliation project {index:00}",
                CustomerId = project.CustomerId,
            };
            db.OperationalProjects.Add(queuedProject);
            db.SaveChanges();
            var binding = new ProjectDriveFolder
            {
                OperationalProjectId = queuedProject.Id,
                Category = ProjectDocumentCategory.DesignConcept,
                DriveFolderId = $"nicon-drive-queue-{index:00}",
                UpdatedAt = touchedAt.AddSeconds(index),
            };
            db.ProjectDriveFolders.Add(binding);
            bindings.Add(binding);
        }
        db.SaveChanges();
        return bindings;
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
            OperationalProjectId = project.Id,
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

    [Fact]
    public async Task FolderInitialization_BuildsEveryProjectTreeOnceWithoutFilesOrDesign()
    {
        folders.Setup(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(async (OperationalProject root, int? actor, CancellationToken ct) =>
            {
                foreach (var category in Enum.GetValues<ProjectDocumentCategory>().Skip(1))
                    await EnsureBindingAsync(root, category, ct);
            });

        var cursor = await processor.InitializeProjectFoldersAsync(0);
        Assert.Equal(project.Id, cursor);
        Assert.Equal(9, await db.ProjectDriveFolders.CountAsync(item => item.OperationalProjectId == project.Id));
        Assert.Empty(db.ProjectDocuments);
        Assert.Equal(0, await processor.InitializeProjectFoldersAsync(cursor));
        Assert.Equal(0, await processor.InitializeProjectFoldersAsync(0));
        folders.Verify(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderInitialization_SkipsProjectsBeingDeleted()
    {
        db.HardDeleteOperations.Add(new HardDeleteOperation
        {
            Id = Guid.NewGuid(),
            ResourceType = EntityTypes.OperationalProject,
            ResourceId = project.Id.ToString(),
            Status = HardDeleteOperationStatus.Processing,
        });
        await db.SaveChangesAsync();

        Assert.Equal(0, await processor.InitializeProjectFoldersAsync(0));
        folders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FolderInitialization_RotatesFailuresAndRetriesAfterWrap()
    {
        for (var index = 0; index < 7; index++)
            db.Add(new OperationalProject { Code = $"NIC-FOLDER-{index}", Name = "NICON handoff", CustomerId = 10 });
        await db.SaveChangesAsync();
        var ids = await db.OperationalProjects.OrderBy(item => item.Id).Select(item => item.Id).ToListAsync();
        folders.Setup(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Temporary Drive outage"));

        var cursor = await processor.InitializeProjectFoldersAsync(0);
        Assert.Equal(ids[4], cursor);
        var next = await processor.InitializeProjectFoldersAsync(cursor);
        Assert.Equal(ids[^1], next);
        Assert.Equal(0, await processor.InitializeProjectFoldersAsync(next));
        Assert.Equal(ids[4], await processor.InitializeProjectFoldersAsync(0));
        folders.Verify(item => item.EnsureTreeAsync(It.Is<OperationalProject>(root => root.Id == ids.Last()),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderInitialization_DisabledDriveDoesNotCallProviderOrChangeCursor()
    {
        options.Enabled = false;
        Assert.Equal(123, await processor.InitializeProjectFoldersAsync(123));
        drive.VerifyNoOtherCalls();
        folders.VerifyNoOtherCalls();
    }

    private async Task<ProjectDriveFolder> EnsureBindingAsync(
        OperationalProject root, ProjectDocumentCategory category, CancellationToken ct)
    {
        var binding = await db.ProjectDriveFolders.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == root.Id && item.Category == category, ct);
        if (binding is not null) return binding;
        binding = new ProjectDriveFolder { OperationalProjectId = root.Id, Category = category, DriveFolderId = $"folder-{category}" };
        db.Add(binding);
        await db.SaveChangesAsync(ct);
        return binding;
    }

    private static DriveItem Remote(string id, string version, DateTime modifiedAt) => new(
        id, "drawing.pdf", "application/pdf", 3, version, modifiedAt,
        $"https://drive.test/{id}", new Dictionary<string, string>(), false);

    public void Dispose() => db.Dispose();
}
