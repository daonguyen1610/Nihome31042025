using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services.GoogleDrive;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DriveTreeMigrationServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.CreateRelational();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly GoogleDriveOptions options = new();

    private DriveTreeMigrationService Service() =>
        new(db, drive.Object, new TestGoogleDriveSettingsStore(options));

    public DriveTreeMigrationServiceTests()
    {
        drive.Setup(item => item.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection("integration@nicon.test", "root", "https://drive.test/root",
                true, false, false, true));
        drive.Setup(item => item.ListChildrenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

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

    public void Dispose() => db.Dispose();
}
