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
    [InlineData(ProjectDocumentCategory.Survey, "01_CoHoi_BaoGia/01_KhaoSat")]
    [InlineData(ProjectDocumentCategory.CrmPreDesign, "01_CoHoi_BaoGia")]
    [InlineData(ProjectDocumentCategory.DesignConcept, "03_ThietKe/01_Concept")]
    [InlineData(ProjectDocumentCategory.DesignBasic, "03_ThietKe/02_CoSo")]
    [InlineData(ProjectDocumentCategory.DesignShopDrawing, "03_ThietKe/04_ShopDrawing")]
    [InlineData(ProjectDocumentCategory.LegalPermits, "04_PhapLy")]
    [InlineData(ProjectDocumentCategory.ConstructionAcceptance, "05_ThiCong/03_ChatLuong_NghiemThu")]
    [InlineData(ProjectDocumentCategory.Procurement, "06_CungUng")]
    [InlineData(ProjectDocumentCategory.FinanceContracts, "02_HopDong")]
    public void ProjectCategoryPaths_MatchPrdV2Tree(ProjectDocumentCategory category, string expected)
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
        Assert.Equal("03_ThietKe", calls[0][1].Name);
        Assert.Equal(calls[0][1].AppProperties, calls[1][1].AppProperties);
        Assert.NotEqual(calls[0][2].AppProperties, calls[1][2].AppProperties);
    }

    [Fact]
    public async Task ProjectFolderService_TreeBuildsEightTopFoldersOnceAndRepeatsWithoutDuplicates()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var project = new OperationalProject { Code = "PJ-2026-0001", Name = "Nhà xưởng", CustomerId = 1 };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var paths = new List<string>();
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.EnsureFolderPathAsync(
                It.IsAny<IReadOnlyList<DriveFolderSegment>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<DriveFolderSegment>, CancellationToken>((segments, _) =>
                paths.Add(string.Join('/', segments.Skip(1).Select(segment => segment.Name))))
            .ReturnsAsync((IReadOnlyList<DriveFolderSegment> segments, CancellationToken _) =>
                new DriveFolder("id-" + segments[^1].AppProperties["niconFolderPath"], null!));
        var service = new ProjectDriveFolderService(db, drive.Object, new TestGoogleDriveSettingsStore(
            new GoogleDriveOptions { Enabled = true, RefreshToken = "token", RootFolderId = "root-folder", InstanceId = "test" }));

        await service.EnsureTreeAsync(project);
        await service.EnsureTreeAsync(project);

        string[] prdV2Tree =
        [
            "01_CoHoi_BaoGia", "01_CoHoi_BaoGia/01_KhaoSat", "02_HopDong",
            "03_ThietKe/01_Concept", "03_ThietKe/02_CoSo", "03_ThietKe/03_ChiTiet_IFC", "03_ThietKe/04_ShopDrawing",
            "04_PhapLy",
            "05_ThiCong/01_ChuanBi", "05_ThiCong/02_NhatKy_BaoCao", "05_ThiCong/03_ChatLuong_NghiemThu",
            "05_ThiCong/04_ATLD_HSE", "05_ThiCong/05_QS_KhoiLuong",
            "06_CungUng", "07_TaiChinh", "08_BanGiao_BaoHanh",
        ];
        Assert.Equal(prdV2Tree.Order(StringComparer.Ordinal), paths.Order(StringComparer.Ordinal));
        Assert.Equal(8, paths.Select(path => path.Split('/')[0]).Distinct().Count());
        var bindings = await db.ProjectDriveFolders.ToListAsync();
        Assert.Equal(prdV2Tree.Length, bindings.Count);
        Assert.Equal("id-03_ThietKe/02_CoSo",
            bindings.Single(binding => binding.Category == ProjectDocumentCategory.DesignBasic).DriveFolderId);
        drive.Verify(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProjectFolderService_TreeFailsClearlyWhenDriveIsNotConnected()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var drive = new Mock<IGoogleDriveAdapter>();
        var service = new ProjectDriveFolderService(db, drive.Object,
            new TestGoogleDriveSettingsStore(new GoogleDriveOptions { Enabled = true, InstanceId = "test" }));

        await Assert.ThrowsAsync<ProjectDriveNotConnectedException>(() =>
            service.EnsureTreeAsync(new OperationalProject { Id = 1, Code = "PJ-1", Name = "P" }));
        drive.VerifyNoOtherCalls();
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
        Assert.Contains("queued", uploaded.GetProperty("en").GetString());
        Assert.Contains("Drive", uploaded.GetProperty("en").GetString());
    }

}
