namespace NihomeBackend.Models;

public class GoogleDriveCredential : IConcurrencyTracked
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? ProtectedClientSecret { get; set; }
    public string? ProtectedRefreshToken { get; set; }
    public string OAuthRedirectUri { get; set; } = string.Empty;
    public string FrontendReturnUrl { get; set; } = "/admin/settings?tab=drive";
    public string RootFolderId { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "Nicon Google Drive Integration";
    public string SurveyMediaFolder { get; set; } = "01_Khao_sat";
    public string CrmPreDesignFolder { get; set; } = "01_CRM_PreDesign";
    public string DesignConceptFolder { get; set; } = "02_Thiet_ke/01_So_bo_Concept";
    public string DesignBasicFolder { get; set; } = "02_Thiet_ke/02_Co_so";
    public string DesignShopDrawingFolder { get; set; } = "02_Thiet_ke/03_Chi_tiet_ShopDrawing";
    public string LegalPermitsFolder { get; set; } = "03_Xin_phep_Phap_ly";
    public string ConstructionAcceptanceFolder { get; set; } = "04_Thi_cong_Nghiem_thu";
    public string ProcurementFolder { get; set; } = "05_Cung_ung_Vat_tu";
    public string FinanceContractsFolder { get; set; } = "06_Tai_chinh_Hop_dong";
    public bool SupportsAllDrives { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public string? AccountEmail { get; set; }
    public int? ConnectedByUserId { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public int UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}