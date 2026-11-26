using Google;
using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DriveTreeMigrationServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.CreateRelational();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IProjectDriveFolderService> folders = new();
    private readonly GoogleDriveOptions options = new();

    private DriveTreeMigrationService Service() =>
        new(db, drive.Object, new TestGoogleDriveSettingsStore(options), folders.Object);

    public DriveTreeMigrationServiceTests()
    {
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection("integration@nicon.test", "root", "https://drive.test/root",
                true, false, false, true));
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        drive.Setup(item => item.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => Item(id));
    }

    private static DriveItem Item(string id) =>
        new(id, id + ".pdf", "application/pdf", 1, null, null, null, new Dictionary<string, string>(), false);

    private void UseLegacyFolders() => options.Folders = new GoogleDriveFolderOptions
    {
        SurveyMedia = "01_Khao_sat",
        CrmPreDesign = "01_CRM_PreDesign",
        DesignConcept = "02_Thiet_ke/01_So_bo_Concept",
        DesignBasic = "02_Thiet_ke/02_Co_so",
        DesignShopDrawing = "02_Thiet_ke/03_Chi_tiet_ShopDrawing",
        LegalPermits = "03_Xin_phep_Phap_ly",
        ConstructionAcceptance = "04_Thi_cong_Nghiem_thu",
        Procurement = "05_Cung_ung_Vat_tu",
        FinanceContracts = "06_Tai_chinh_Hop_dong",
    };

    private OperationalProject AddProject(string code)
    {
        var project = new OperationalProject { Code = code, Name = code, CustomerId = 1 };
        db.OperationalProjects.Add(project);
        db.SaveChanges();
        return project;
    }

    private void Bind(OperationalProject project, ProjectDocumentCategory category, string folderId)
    {
        db.ProjectDriveFolders.Add(new ProjectDriveFolder
        {
            OperationalProjectId = project.Id,
            Category = category,
            DriveFolderId = folderId,
        });
        db.SaveChanges();
    }

    private ProjectDocument AddDocument(OperationalProject project, ProjectDocumentCategory category,
        string fileId, string folderId, DateTime? deletedAt = null,
        ProjectDocumentSyncStatus status = ProjectDocumentSyncStatus.Synced)
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            Category = category,
            DriveFileId = fileId,
            DriveFolderId = folderId,
            SyncStatus = status,
            DeletedAt = deletedAt,
        };
        db.ProjectDocuments.Add(document);
        db.SaveChanges();
        return document;
    }

    [Fact]
    public async Task Preview_lists_each_category_whose_path_differs_from_prd_v2()
    {
        UseLegacyFolders();

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Equal(9, preview.Changes.Count);
        Assert.Contains(new CategoryChange(ProjectDocumentCategory.Survey, "01_Khao_sat", "01_CoHoi_BaoGia/01_KhaoSat"), preview.Changes);
        Assert.Contains(new CategoryChange(ProjectDocumentCategory.FinanceContracts, "06_Tai_chinh_Hop_dong", "02_HopDong"), preview.Changes);
        Assert.Empty(preview.Blockers);
    }

    [Fact]
    public async Task Preview_counts_projects_documents_and_untracked_files()
    {
        UseLegacyFolders();
        var first = AddProject("OP-1");
        var second = AddProject("OP-2");
        Bind(first, ProjectDocumentCategory.Survey, "f-survey-1");
        Bind(first, ProjectDocumentCategory.DesignBasic, "f-basic-1");
        Bind(second, ProjectDocumentCategory.Survey, "f-survey-2");
        AddDocument(first, ProjectDocumentCategory.Survey, "file-1", "f-survey-1");
        AddDocument(first, ProjectDocumentCategory.DesignBasic, "file-2", "f-basic-1");
        AddDocument(second, ProjectDocumentCategory.Survey, "file-3", "f-survey-2");
        AddDocument(second, ProjectDocumentCategory.Survey, "file-4", "f-survey-2", deletedAt: DateTime.UtcNow);
        drive.Setup(item => item.ListChildrenAsync("f-survey-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DriveItem("file-1", "a.pdf", "application/pdf", 1, null, null, null, new Dictionary<string, string>(), false),
                new DriveItem("stray", "b.pdf", "application/pdf", 1, null, null, null, new Dictionary<string, string>(), false),
            ]);

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Equal(2, preview.ProjectCount);
        Assert.Equal(3, preview.DocumentCount);
        Assert.Equal(1, preview.UntrackedFileCount);
    }

    [Fact]
    public async Task Preview_blocks_while_sync_is_enabled_or_documents_are_processing()
    {
        UseLegacyFolders();
        options.Enabled = true;
        var project = AddProject("OP-1");
        Bind(project, ProjectDocumentCategory.Survey, "f-survey");
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "f-survey", status: ProjectDocumentSyncStatus.Processing);

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Contains("DRIVE_SYNC_ENABLED", preview.Blockers);
        Assert.Contains("DRIVE_DOCUMENTS_PROCESSING", preview.Blockers);
    }

    [Fact]
    public async Task Preview_blocks_when_drive_is_not_connected_or_a_migration_is_running()
    {
        UseLegacyFolders();
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no token"));
        db.DriveTreeMigrations.Add(new DriveTreeMigration
        {
            Status = DriveTreeMigrationStatus.Running,
            OldFoldersJson = "{}",
            NewFoldersJson = "{}",
        });
        db.SaveChanges();

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Contains("DRIVE_NOT_CONNECTED", preview.Blockers);
        Assert.Contains("DRIVE_TREE_MIGRATION_RUNNING", preview.Blockers);
        drive.Verify(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Preview_reports_already_standard()
    {
        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Empty(preview.Changes);
        Assert.Contains("DRIVE_TREE_ALREADY_STANDARD", preview.Blockers);
    }

    [Fact]
    public async Task Preview_is_not_standard_while_a_document_sits_outside_its_category_folder()
    {
        var project = AddProject("OP-1");
        Bind(project, ProjectDocumentCategory.Handover, "f-handover");
        Bind(project, ProjectDocumentCategory.ConstructionAcceptance, "f-acceptance");
        AddDocument(project, ProjectDocumentCategory.Handover, "file-1", "f-acceptance");

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Empty(preview.Changes);
        Assert.DoesNotContain("DRIVE_TREE_ALREADY_STANDARD", preview.Blockers);
        Assert.Equal(1, preview.DocumentCount);
    }

    [Fact]
    public async Task Preview_ignores_unclassified_documents_when_checking_placement()
    {
        var project = AddProject("OP-1");
        AddDocument(project, ProjectDocumentCategory.Unclassified, "file-1", "anywhere");

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Contains("DRIVE_TREE_ALREADY_STANDARD", preview.Blockers);
    }

    [Fact]
    public async Task Preview_skips_an_old_folder_that_no_longer_exists()
    {
        UseLegacyFolders();
        var project = AddProject("OP-1");
        Bind(project, ProjectDocumentCategory.Survey, "f-gone");
        Bind(project, ProjectDocumentCategory.Procurement, "f-procurement");
        drive.Setup(item => item.GetMetadataAsync("f-gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveItem?)null);
        drive.Setup(item => item.ListChildrenAsync("f-gone", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "File not found") { HttpStatusCode = System.Net.HttpStatusCode.NotFound });
        drive.Setup(item => item.ListChildrenAsync("f-procurement", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);

        var preview = await Service().PreviewAsync(CancellationToken.None);

        Assert.Equal(1, preview.UntrackedFileCount);
    }

    [Fact]
    public async Task Preview_summarises_the_latest_migration()
    {
        Assert.Null((await Service().PreviewAsync(CancellationToken.None)).LatestMigration);
        db.DriveTreeMigrations.AddRange(
            new DriveTreeMigration { Status = DriveTreeMigrationStatus.RolledBack, UndoStartedAt = DateTime.UtcNow },
            new DriveTreeMigration { Status = DriveTreeMigrationStatus.Failed, Error = "boom" });
        db.SaveChanges();
        var latest = db.DriveTreeMigrations.OrderBy(m => m.Id).Last();

        Assert.Equal(new DriveTreeMigrationSummary(latest.Id, DriveTreeMigrationStatus.Failed, false, "boom"),
            (await Service().PreviewAsync(CancellationToken.None)).LatestMigration);

        latest.UndoStartedAt = DateTime.UtcNow;
        db.SaveChanges();

        Assert.True((await Service().PreviewAsync(CancellationToken.None)).LatestMigration!.UndoStarted);
    }

    [Fact]
    public async Task Run_and_undo_are_refused_without_touching_drive_while_another_move_is_executing()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        var reachedDrive = new TaskCompletionSource();
        var releaseDrive = new TaskCompletionSource();
        drive.Setup(item => item.MoveAsync("file-survey", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                reachedDrive.TrySetResult();
                return releaseDrive.Task;
            });
        var first = Service().RunAsync(7, CancellationToken.None);
        await reachedDrive.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var untouched = new Mock<IGoogleDriveAdapter>(MockBehavior.Strict);
        var second = new DriveTreeMigrationService(db, untouched.Object,
            new TestGoogleDriveSettingsStore(options), folders.Object);

        var run = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            second.RunAsync(7, CancellationToken.None));
        var undo = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            second.UndoAsync(1, 7, CancellationToken.None));

        Assert.Equal("DRIVE_TREE_MIGRATION_RUNNING", run.Code);
        Assert.Equal("DRIVE_TREE_MIGRATION_RUNNING", undo.Code);
        releaseDrive.SetResult();
        var finished = await first;
        Assert.Equal(DriveTreeMigrationStatus.Completed, finished.Status);
        // The gate is released afterwards, also for the other operation.
        Assert.Equal(DriveTreeMigrationStatus.RolledBack,
            (await Service().UndoAsync(finished.MigrationId, 7, CancellationToken.None)).Status);
    }

    private GoogleDriveCredential AddLegacyCredential()
    {
        UseLegacyFolders();
        var credential = new GoogleDriveCredential
        {
            Id = 1,
            SurveyMediaFolder = "01_Khao_sat",
            CrmPreDesignFolder = "01_CRM_PreDesign",
            DesignConceptFolder = "02_Thiet_ke/01_So_bo_Concept",
            DesignBasicFolder = "02_Thiet_ke/02_Co_so",
            DesignShopDrawingFolder = "02_Thiet_ke/03_Chi_tiet_ShopDrawing",
            LegalPermitsFolder = "03_Xin_phep_Phap_ly",
            ConstructionAcceptanceFolder = "04_Thi_cong_Nghiem_thu",
            ProcurementFolder = "05_Cung_ung_Vat_tu",
            FinanceContractsFolder = "06_Tai_chinh_Hop_dong",
            HandoverFolder = "08_BanGiao_BaoHanh",
            RowVersion = [1],
        };
        db.GoogleDriveCredentials.Add(credential);
        db.SaveChanges();
        return credential;
    }

    /// <summary>One project on the legacy tree; EnsureTreeAsync binds new-{category} folders.</summary>
    private OperationalProject LegacyProject()
    {
        AddLegacyCredential();
        var project = AddProject("OP-1");
        Bind(project, ProjectDocumentCategory.Survey, "old-survey");
        Bind(project, ProjectDocumentCategory.FinanceContracts, "old-contract");
        Bind(project, ProjectDocumentCategory.ConstructionAcceptance, "old-acceptance");
        Bind(project, ProjectDocumentCategory.Handover, "old-handover");
        folders.Setup(item => item.EnsureTreeAsync(It.IsAny<OperationalProject>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>(), false))
            .Returns((OperationalProject root, int? _, CancellationToken _, bool _) =>
            {
                foreach (var (category, id) in new[]
                         {
                             (ProjectDocumentCategory.Survey, "new-survey"),
                             (ProjectDocumentCategory.FinanceContracts, "new-contract"),
                             (ProjectDocumentCategory.ConstructionAcceptance, "new-acceptance"),
                             (ProjectDocumentCategory.Handover, "new-handover"),
                         })
                    if (!db.ProjectDriveFolders.Any(f => f.OperationalProjectId == root.Id && f.Category == category))
                        db.ProjectDriveFolders.Add(new ProjectDriveFolder
                        {
                            OperationalProjectId = root.Id,
                            Category = category,
                            DriveFolderId = id,
                        });
                db.SaveChanges();
                return Task.CompletedTask;
            });
        return project;
    }

    [Fact]
    public async Task Run_rebinds_every_project_and_moves_each_file_into_its_prd_v2_folder()
    {
        var project = LegacyProject();
        var survey = AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        var contract = AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-contract", "old-contract");
        // Re-categorised handover document still in the acceptance folder.
        var handover = AddDocument(project, ProjectDocumentCategory.Handover, "file-handover", "old-acceptance");
        AddDocument(project, ProjectDocumentCategory.Unclassified, "file-loose", "old-survey");

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        Assert.Equal(3, result.Moved);
        drive.Verify(item => item.MoveAsync("file-survey", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-contract", "new-contract", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-handover", "new-handover", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-loose", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.ChangeTracker.Clear();
        Assert.Equal("new-survey", db.ProjectDocuments.Single(d => d.Id == survey.Id).DriveFolderId);
        Assert.Equal("new-contract", db.ProjectDocuments.Single(d => d.Id == contract.Id).DriveFolderId);
        Assert.Equal("new-handover", db.ProjectDocuments.Single(d => d.Id == handover.Id).DriveFolderId);
        var settings = db.GoogleDriveCredentials.Single();
        var standard = new GoogleDriveCredential();
        Assert.Equal(standard.SurveyMediaFolder, settings.SurveyMediaFolder);
        Assert.Equal(standard.FinanceContractsFolder, settings.FinanceContractsFolder);
        Assert.Equal(standard.ConstructionAcceptanceFolder, settings.ConstructionAcceptanceFolder);
        Assert.False(settings.Enabled);
        var bindings = db.DriveTreeMigrationItems.Where(i => i.Kind == DriveTreeMigrationItemKind.Binding).ToList();
        Assert.Equal(4, bindings.Count);
        Assert.Contains(bindings, i => i.FromFolderId == "old-survey" && i.ToFolderId == "new-survey");
        Assert.Equal(7, db.DriveTreeMigrations.Single().StartedByUserId);
    }

    [Fact]
    public async Task Run_moves_untracked_files_from_old_folders()
    {
        LegacyProject();
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        drive.Verify(item => item.MoveAsync("stray", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(db.DriveTreeMigrationItems, i => i.Kind == DriveTreeMigrationItemKind.UntrackedFile
            && i.DriveFileId == "stray" && i.MovedAt != null);
    }

    [Fact]
    public async Task Run_records_a_missing_old_folder_and_continues()
    {
        LegacyProject();
        drive.Setup(item => item.GetMetadataAsync("old-contract", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveItem?)null);
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        Assert.Equal("DRIVE_FOLDER_MISSING", db.DriveTreeMigrationItems.Single(i =>
            i.Kind == DriveTreeMigrationItemKind.Binding && i.FromFolderId == "old-contract").SkipReason);
        drive.Verify(item => item.MoveAsync("stray", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_skips_a_file_deleted_on_drive_and_continues()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "gone", "old-survey");
        AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        drive.Setup(item => item.GetMetadataAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveItem?)null);

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Moved);
        drive.Verify(item => item.MoveAsync("gone", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        drive.Verify(item => item.MoveAsync("file-survey", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("DRIVE_FILE_MISSING", db.DriveTreeMigrationItems.Single(i => i.DriveFileId == "gone").SkipReason);
    }

    [Fact]
    public async Task Run_resumes_after_a_drive_failure_without_moving_twice()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-2", "old-contract");
        drive.SetupSequence(item => item.MoveAsync("file-2", "new-contract", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Backend error"))
            .Returns(Task.CompletedTask);

        var first = await Service().RunAsync(7, CancellationToken.None);
        Assert.Equal(DriveTreeMigrationStatus.Failed, first.Status);
        Assert.NotNull(first.Error);

        var second = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, second.Status);
        Assert.Equal(first.MigrationId, second.MigrationId);
        Assert.Equal(2, second.Moved);
        drive.Verify(item => item.MoveAsync("file-1", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-2", "new-contract", It.IsAny<CancellationToken>()), Times.Exactly(2));
        db.ChangeTracker.Clear();
        Assert.All(db.ProjectDocuments, d => Assert.StartsWith("new-", d.DriveFolderId));
    }

    [Fact]
    public async Task Run_refuses_while_sync_is_enabled()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        options.Enabled = true;

        var error = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().RunAsync(7, CancellationToken.None));

        Assert.Equal("DRIVE_SYNC_ENABLED", error.Code);
        drive.Verify(item => item.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        folders.VerifyNoOtherCalls();
        db.ChangeTracker.Clear();
        Assert.Equal("01_Khao_sat", db.GoogleDriveCredentials.Single().SurveyMediaFolder);
        Assert.Equal(4, db.ProjectDriveFolders.Count());
        Assert.Empty(db.DriveTreeMigrations);
    }

    [Fact]
    public async Task Run_moves_a_subfolder_of_an_old_folder_as_one_item()
    {
        LegacyProject();
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("anh-hien-truong") with { MimeType = "application/vnd.google-apps.folder" }]);

        var preview = await Service().PreviewAsync(CancellationToken.None);
        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(1, preview.UntrackedFileCount);
        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        drive.Verify(item => item.MoveAsync("anh-hien-truong", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(db.DriveTreeMigrationItems, i => i.Kind == DriveTreeMigrationItemKind.UntrackedFile
            && i.DriveFileId == "anh-hien-truong");
    }

    [Fact]
    public async Task Run_resume_marks_an_untracked_move_that_was_not_recorded()
    {
        LegacyProject();
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);
        drive.Setup(item => item.MoveAsync("stray", "new-survey", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Backend error"));
        Assert.Equal(DriveTreeMigrationStatus.Failed, (await Service().RunAsync(7, CancellationToken.None)).Status);
        // The file did reach the new folder, so the old folder no longer lists it.
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        drive.Setup(item => item.MoveAsync("stray", "new-survey", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        db.ChangeTracker.Clear();
        Assert.NotNull(db.DriveTreeMigrationItems.Single(i => i.DriveFileId == "stray").MovedAt);
    }

    [Fact]
    public async Task Run_records_the_drive_parent_of_a_document_without_a_folder()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-known", null!);
        AddDocument(project, ProjectDocumentCategory.Survey, "file-orphan", null!);
        drive.Setup(item => item.GetMetadataAsync("file-known", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Item("file-known") with { Parents = ["old-survey"] });

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        Assert.Equal("old-survey", db.DriveTreeMigrationItems.Single(i => i.DriveFileId == "file-known").FromFolderId);
        drive.Verify(item => item.MoveAsync("file-known", "new-survey", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("DRIVE_PARENT_UNKNOWN", db.DriveTreeMigrationItems.Single(i => i.DriveFileId == "file-orphan").SkipReason);
        drive.Verify(item => item.MoveAsync("file-orphan", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Undo_moves_files_back_and_restores_old_paths_and_bindings()
    {
        var project = LegacyProject();
        var survey = AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);
        var run = await Service().RunAsync(7, CancellationToken.None);
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.RolledBack, result.Status);
        drive.Verify(item => item.MoveAsync("file-survey", "old-survey", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("stray", "old-survey", It.IsAny<CancellationToken>()), Times.Once);
        db.ChangeTracker.Clear();
        Assert.Equal("old-survey", db.ProjectDocuments.Single(d => d.Id == survey.Id).DriveFolderId);
        var settings = db.GoogleDriveCredentials.Single();
        Assert.Equal("01_Khao_sat", settings.SurveyMediaFolder);
        Assert.Equal("01_CRM_PreDesign", settings.CrmPreDesignFolder);
        Assert.Equal("02_Thiet_ke/01_So_bo_Concept", settings.DesignConceptFolder);
        Assert.Equal("02_Thiet_ke/02_Co_so", settings.DesignBasicFolder);
        Assert.Equal("02_Thiet_ke/03_Chi_tiet_ShopDrawing", settings.DesignShopDrawingFolder);
        Assert.Equal("03_Xin_phep_Phap_ly", settings.LegalPermitsFolder);
        Assert.Equal("04_Thi_cong_Nghiem_thu", settings.ConstructionAcceptanceFolder);
        Assert.Equal("05_Cung_ung_Vat_tu", settings.ProcurementFolder);
        Assert.Equal("06_Tai_chinh_Hop_dong", settings.FinanceContractsFolder);
        Assert.Equal("08_BanGiao_BaoHanh", settings.HandoverFolder);
        var bindings = db.ProjectDriveFolders.ToDictionary(f => f.Category, f => f.DriveFolderId);
        Assert.Equal(4, bindings.Count);
        Assert.Equal("old-survey", bindings[ProjectDocumentCategory.Survey]);
        Assert.Equal("old-handover", bindings[ProjectDocumentCategory.Handover]);
        Assert.Equal("old-contract", bindings[ProjectDocumentCategory.FinanceContracts]);
        Assert.Equal("old-acceptance", bindings[ProjectDocumentCategory.ConstructionAcceptance]);
        Assert.Equal(DriveTreeMigrationStatus.RolledBack, db.DriveTreeMigrations.Single().Status);
        drive.Verify(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Undo_refuses_while_sync_is_enabled()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        options.Enabled = true;
        drive.Invocations.Clear();

        var error = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().UndoAsync(run.MigrationId, 7, CancellationToken.None));

        Assert.Equal("DRIVE_SYNC_ENABLED", error.Code);
        drive.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Undo_refuses_a_migration_that_is_not_the_latest_completed_one()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-survey", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        var error = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().UndoAsync(run.MigrationId, 7, CancellationToken.None));

        Assert.Equal("DRIVE_TREE_MIGRATION_NOT_COMPLETED", error.Code);
    }

    [Fact]
    public async Task Undo_leaves_files_that_were_not_moved_by_the_migration()
    {
        var project = LegacyProject();
        var run = await Service().RunAsync(7, CancellationToken.None);
        AddDocument(project, ProjectDocumentCategory.Survey, "file-later", "new-survey");

        await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        drive.Verify(item => item.MoveAsync("file-later", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Undo_resumes_after_a_drive_failure_and_skips_a_file_gone_from_drive()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-2", "old-contract");
        AddDocument(project, ProjectDocumentCategory.Survey, "gone", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        drive.Setup(item => item.GetMetadataAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveItem?)null);
        drive.SetupSequence(item => item.MoveAsync("file-2", "old-contract", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Backend error"))
            .Returns(Task.CompletedTask);

        var first = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);
        Assert.Equal(DriveTreeMigrationStatus.Failed, first.Status);
        db.ChangeTracker.Clear();
        Assert.Equal("02_HopDong", db.GoogleDriveCredentials.Single().FinanceContractsFolder);
        Assert.Equal("DRIVE_TREE_UNDO_IN_PROGRESS", (await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().RunAsync(7, CancellationToken.None))).Code);

        var second = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.RolledBack, second.Status);
        drive.Verify(item => item.MoveAsync("file-1", "old-survey", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-2", "old-contract", It.IsAny<CancellationToken>()), Times.Exactly(2));
        drive.Verify(item => item.MoveAsync("gone", "old-survey", It.IsAny<CancellationToken>()), Times.Never);
        db.ChangeTracker.Clear();
        Assert.All(db.DriveTreeMigrationItems.Where(i => i.MovedAt != null), i => Assert.NotNull(i.RevertedAt));
        Assert.Equal("new-survey", db.ProjectDocuments.Single(d => d.DriveFileId == "gone").DriveFolderId);
    }

    [Fact]
    public async Task Undo_cancelled_on_a_completed_run_blocks_run_and_can_be_finished()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-2", "old-contract");
        var run = await Service().RunAsync(7, CancellationToken.None);
        drive.SetupSequence(item => item.MoveAsync("file-2", "old-contract", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Service().UndoAsync(run.MigrationId, 7, CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal(DriveTreeMigrationStatus.Completed, db.DriveTreeMigrations.Single().Status);

        Assert.Equal("DRIVE_TREE_UNDO_IN_PROGRESS", (await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().RunAsync(7, CancellationToken.None))).Code);
        Assert.Contains("DRIVE_TREE_UNDO_IN_PROGRESS", (await Service().PreviewAsync(CancellationToken.None)).Blockers);
        var result = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);
        Assert.Equal(DriveTreeMigrationStatus.RolledBack, result.Status);
        db.ChangeTracker.Clear();
        Assert.All(db.ProjectDocuments, d => Assert.StartsWith("old-", d.DriveFolderId));
    }

    [Fact]
    public async Task Undo_that_fails_on_its_first_return_still_blocks_run()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        // Drive applied the move but the response was lost.
        drive.Setup(item => item.MoveAsync("file-1", "old-survey", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Timeout"));

        Assert.Equal(DriveTreeMigrationStatus.Failed,
            (await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None)).Status);

        Assert.Equal("DRIVE_TREE_UNDO_IN_PROGRESS", (await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().RunAsync(7, CancellationToken.None))).Code);
    }

    [Fact]
    public async Task Undo_of_a_failed_run_returns_a_move_that_drive_applied_but_the_log_missed()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-2", "old-contract");
        AddDocument(project, ProjectDocumentCategory.ConstructionAcceptance, "file-3", "old-acceptance");
        drive.Setup(item => item.MoveAsync("file-2", "new-contract", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Timeout"));
        var run = await Service().RunAsync(7, CancellationToken.None);
        Assert.Equal(DriveTreeMigrationStatus.Failed, run.Status);
        // file-2 did reach the new folder; file-3 was never recorded or moved.
        drive.Setup(item => item.GetMetadataAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Item("file-2") with { Parents = ["new-contract"] });

        var result = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.RolledBack, result.Status);
        drive.Verify(item => item.MoveAsync("file-1", "old-survey", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-2", "old-contract", It.IsAny<CancellationToken>()), Times.Once);
        drive.Verify(item => item.MoveAsync("file-3", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.ChangeTracker.Clear();
        Assert.All(db.ProjectDocuments, d => Assert.StartsWith("old-", d.DriveFolderId));
    }

    [Fact]
    public async Task Undo_leaves_a_recorded_move_that_drive_never_applied()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.FinanceContracts, "file-2", "old-contract");
        drive.Setup(item => item.MoveAsync("file-2", "new-contract", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Backend error"));
        var run = await Service().RunAsync(7, CancellationToken.None);
        drive.Setup(item => item.GetMetadataAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Item("file-2") with { Parents = ["old-contract"] });

        var result = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.RolledBack, result.Status);
        drive.Verify(item => item.MoveAsync("file-2", "old-contract", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Undo_skips_bindings_of_a_project_deleted_after_the_run()
    {
        var project = LegacyProject();
        var other = AddProject("OP-2");
        Bind(other, ProjectDocumentCategory.Survey, "old-survey-2");
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        db.ProjectDriveFolders.RemoveRange(db.ProjectDriveFolders.Where(f => f.OperationalProjectId == other.Id));
        db.OperationalProjects.Remove(db.OperationalProjects.Single(p => p.Id == other.Id));
        db.SaveChanges();
        db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");

        var result = await Service().UndoAsync(run.MigrationId, 7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.RolledBack, result.Status);
        db.ChangeTracker.Clear();
        Assert.All(db.ProjectDriveFolders, f => Assert.Equal(project.Id, f.OperationalProjectId));
        Assert.Equal("old-survey", db.ProjectDocuments.Single().DriveFolderId);
    }

    [Fact]
    public async Task Undo_refuses_when_drive_is_not_connected()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        var run = await Service().RunAsync(7, CancellationToken.None);
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no token"));
        drive.Invocations.Clear();

        var error = await Assert.ThrowsAsync<DriveTreeMigrationBlockedException>(() =>
            Service().UndoAsync(run.MigrationId, 7, CancellationToken.None));

        Assert.Equal("DRIVE_NOT_CONNECTED", error.Code);
        drive.Verify(item => item.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        drive.Verify(item => item.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.ChangeTracker.Clear();
        Assert.Null(db.DriveTreeMigrations.Single().UndoStartedAt);
    }

    [Fact]
    public async Task Run_after_a_successful_undo_starts_a_new_migration()
    {
        var project = LegacyProject();
        AddDocument(project, ProjectDocumentCategory.Survey, "file-1", "old-survey");
        var first = await Service().RunAsync(7, CancellationToken.None);
        await Service().UndoAsync(first.MigrationId, 7, CancellationToken.None);

        var second = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, second.Status);
        Assert.NotEqual(first.MigrationId, second.MigrationId);
        drive.Verify(item => item.MoveAsync("file-1", "new-survey", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Run_resume_skips_an_untracked_file_deleted_between_runs()
    {
        LegacyProject();
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Item("stray")]);
        drive.Setup(item => item.MoveAsync("stray", "new-survey", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleApiException("drive", "Backend error"));
        Assert.Equal(DriveTreeMigrationStatus.Failed, (await Service().RunAsync(7, CancellationToken.None)).Status);
        drive.Setup(item => item.ListChildrenAsync("old-survey", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        drive.Setup(item => item.GetMetadataAsync("stray", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveItem?)null);

        var result = await Service().RunAsync(7, CancellationToken.None);

        Assert.Equal(DriveTreeMigrationStatus.Completed, result.Status);
        db.ChangeTracker.Clear();
        Assert.Equal("DRIVE_FILE_MISSING", db.DriveTreeMigrationItems.Single(i => i.DriveFileId == "stray").SkipReason);
    }

    public void Dispose() => db.Dispose();
}
