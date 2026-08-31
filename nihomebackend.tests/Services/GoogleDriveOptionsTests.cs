using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveOptionsTests
{
    [Fact]
    public void Defaults_AreDisabledWithoutDeploymentIdentity()
    {
        var options = new GoogleDriveOptions();

        Assert.False(options.Enabled);
        Assert.Empty(options.InstanceId);
    }

    [Theory]
    [InlineData(ProjectDocumentCategory.Survey, "01_Khao_sat")]
    [InlineData(ProjectDocumentCategory.CrmPreDesign, "01_CRM_PreDesign")]
    [InlineData(ProjectDocumentCategory.DesignConcept, "02_Thiet_ke/01_So_bo_Concept")]
    [InlineData(ProjectDocumentCategory.DesignBasic, "02_Thiet_ke/02_Co_so")]
    [InlineData(ProjectDocumentCategory.DesignShopDrawing, "02_Thiet_ke/03_Chi_tiet_ShopDrawing")]
    [InlineData(ProjectDocumentCategory.LegalPermits, "03_Xin_phep_Phap_ly")]
    [InlineData(ProjectDocumentCategory.ConstructionAcceptance, "04_Thi_cong_Nghiem_thu")]
    [InlineData(ProjectDocumentCategory.Procurement, "05_Cung_ung_Vat_tu")]
    [InlineData(ProjectDocumentCategory.FinanceContracts, "06_Tai_chinh_Hop_dong")]
    public void ProjectCategoryPaths_MatchJiraHierarchy(ProjectDocumentCategory category, string expected)
    {
        var folders = new GoogleDriveFolderOptions();

        Assert.Equal(expected, folders.For(category));
        Assert.Equal(expected.Split('/'), folders.SegmentsFor(category));
    }

    [Fact]
    public async Task ProjectFolderService_DesignCategoriesShareSameParentIdentity()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var project = new OperationalProject { Code = "OP-1", Name = "Project", CustomerId = 1 };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var calls = new List<IReadOnlyList<DriveFolderSegment>>();
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.EnsureFolderPathAsync(
                It.IsAny<IReadOnlyList<DriveFolderSegment>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DriveFolderSegment>, CancellationToken>((segments, _) => calls.Add(segments))
            .ReturnsAsync(() => new DriveFolder($"folder-{calls.Count}", null!));
        var service = new ProjectDriveFolderService(db, drive.Object,
            new TestGoogleDriveSettingsStore(new GoogleDriveOptions { InstanceId = "test" }));

        await service.EnsureAsync(project, ProjectDocumentCategory.DesignConcept);
        await service.EnsureAsync(project, ProjectDocumentCategory.DesignBasic);

        Assert.Equal(2, calls.Count);
        Assert.Equal("02_Thiet_ke", calls[0][1].Name);
        Assert.Equal(calls[0][1].AppProperties, calls[1][1].AppProperties);
        Assert.NotEqual(calls[0][2].AppProperties, calls[1][2].AppProperties);
    }

    [Fact]
    public void ProjectDocumentTranslations_PopulateAllSupportedLanguages()
    {
        var assembly = typeof(GoogleDriveOptions).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("operational-projects.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var document = JsonDocument.Parse(stream);
        var entries = document.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("key").GetString()!
                .StartsWith("operationalProjects.documents.", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            foreach (var language in new[] { "vi", "en", "zh", "ja" })
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty(language).GetString()));
        });
        var uploaded = entries.Single(entry =>
            entry.GetProperty("key").GetString() == "operationalProjects.documents.uploaded");
        Assert.Contains("Google Drive", uploaded.GetProperty("en").GetString());
    }

}
