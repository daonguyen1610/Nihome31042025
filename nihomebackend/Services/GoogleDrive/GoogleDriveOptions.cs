using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

/// <summary>
/// Runtime snapshot of the Google Drive settings stored through the Admin configuration service.
/// </summary>
public sealed class GoogleDriveOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string OAuthRedirectUri { get; set; } = string.Empty;
    public string FrontendReturnUrl { get; set; } = "/admin/settings?tab=drive";
    public string RootFolderId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "Nicon Google Drive Integration";
    public string InstanceId { get; set; } = string.Empty;
    public GoogleDriveFolderOptions Folders { get; set; } = new();
    public bool SupportsAllDrives { get; set; } = true;
    public bool DeleteDriveFilesOnWebDelete { get; set; }
    public bool DeleteWebRecordsOnDriveDelete { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public string ConfigurationVersion { get; set; } = string.Empty;
}

public sealed class GoogleDriveFolderOptions
{
    public string SurveyMedia { get; set; } = "01_CoHoi_BaoGia/01_KhaoSat";
    public string CrmPreDesign { get; set; } = "01_CoHoi_BaoGia";
    public string DesignConcept { get; set; } = "03_ThietKe/01_Concept";
    public string DesignBasic { get; set; } = "03_ThietKe/02_CoSo";
    public string DesignShopDrawing { get; set; } = "03_ThietKe/04_ShopDrawing";
    public string LegalPermits { get; set; } = "04_PhapLy";
    public string ConstructionAcceptance { get; set; } = "05_ThiCong/03_ChatLuong_NghiemThu";
    public string Procurement { get; set; } = "06_CungUng";
    public string FinanceContracts { get; set; } = "02_HopDong";

    public string For(ProjectDocumentCategory category) => category switch
    {
        ProjectDocumentCategory.Survey => SurveyMedia,
        ProjectDocumentCategory.CrmPreDesign => CrmPreDesign,
        ProjectDocumentCategory.DesignConcept => DesignConcept,
        ProjectDocumentCategory.DesignBasic => DesignBasic,
        ProjectDocumentCategory.DesignShopDrawing => DesignShopDrawing,
        ProjectDocumentCategory.LegalPermits => LegalPermits,
        ProjectDocumentCategory.ConstructionAcceptance => ConstructionAcceptance,
        ProjectDocumentCategory.Procurement => Procurement,
        ProjectDocumentCategory.FinanceContracts => FinanceContracts,
        _ => throw new ArgumentOutOfRangeException(nameof(category), "Danh mục chưa phân loại không có thư mục Drive đích."),
    };

    public IReadOnlyList<string> SegmentsFor(ProjectDocumentCategory category) => For(category)
        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>PRD v2 section 7: the eight numbered top-level project folders and their subfolders (leaf paths).</summary>
    public static readonly IReadOnlyList<string> StandardTree =
    [
        "01_CoHoi_BaoGia/01_KhaoSat", "02_HopDong",
        "03_ThietKe/01_Concept", "03_ThietKe/02_CoSo", "03_ThietKe/03_ChiTiet_IFC", "03_ThietKe/04_ShopDrawing",
        "04_PhapLy",
        "05_ThiCong/01_ChuanBi", "05_ThiCong/02_NhatKy_BaoCao", "05_ThiCong/03_ChatLuong_NghiemThu",
        "05_ThiCong/04_ATLD_HSE", "05_ThiCong/05_QS_KhoiLuong",
        "06_CungUng", "07_TaiChinh", "08_BanGiao_BaoHanh",
    ];

    /// <summary>
    /// Folders a project tree needs beyond its category folders. Empty while an older custom topology is
    /// configured, so a project never gets a mixed tree.
    /// </summary>
    public IEnumerable<string> ExtraTreePaths()
    {
        var categoryPaths = Enum.GetValues<ProjectDocumentCategory>()
            .Where(category => category != ProjectDocumentCategory.Unclassified).Select(For).ToList();
        var standard = categoryPaths.All(path => StandardTree.Any(leaf => leaf == path || leaf.StartsWith(path + "/", StringComparison.Ordinal)));
        return standard ? StandardTree.Except(categoryPaths) : [];
    }
}
