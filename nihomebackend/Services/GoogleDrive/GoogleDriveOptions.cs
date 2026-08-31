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
    public int PollIntervalSeconds { get; set; } = 15;
    public string ConfigurationVersion { get; set; } = string.Empty;
}

public sealed class GoogleDriveFolderOptions
{
    public string SurveyMedia { get; set; } = "01_Khao_sat";
    public string CrmPreDesign { get; set; } = "01_CRM_PreDesign";
    public string DesignConcept { get; set; } = "02_Thiet_ke/01_So_bo_Concept";
    public string DesignBasic { get; set; } = "02_Thiet_ke/02_Co_so";
    public string DesignShopDrawing { get; set; } = "02_Thiet_ke/03_Chi_tiet_ShopDrawing";
    public string LegalPermits { get; set; } = "03_Xin_phep_Phap_ly";
    public string ConstructionAcceptance { get; set; } = "04_Thi_cong_Nghiem_thu";
    public string Procurement { get; set; } = "05_Cung_ung_Vat_tu";
    public string FinanceContracts { get; set; } = "06_Tai_chinh_Hop_dong";

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
}