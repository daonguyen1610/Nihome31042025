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
    public string SurveyMediaFolder { get; set; } = "01_CoHoi_BaoGia/01_KhaoSat";
    public string CrmPreDesignFolder { get; set; } = "01_CoHoi_BaoGia";
    public string DesignConceptFolder { get; set; } = "03_ThietKe/01_Concept";
    public string DesignBasicFolder { get; set; } = "03_ThietKe/02_CoSo";
    public string DesignShopDrawingFolder { get; set; } = "03_ThietKe/04_ShopDrawing";
    public string LegalPermitsFolder { get; set; } = "04_PhapLy";
    public string ConstructionAcceptanceFolder { get; set; } = "05_ThiCong/03_ChatLuong_NghiemThu";
    public string ProcurementFolder { get; set; } = "06_CungUng";
    public string FinanceContractsFolder { get; set; } = "02_HopDong";
    public string DesignIfcFolder { get; set; } = "03_ThietKe/03_ChiTiet_IFC";
    public string ConstructionPreparationFolder { get; set; } = "05_ThiCong/01_ChuanBi";
    public string ConstructionDiaryFolder { get; set; } = "05_ThiCong/02_NhatKy_BaoCao";
    public string HseFolder { get; set; } = "05_ThiCong/04_ATLD_HSE";
    public string QuantitySurveyFolder { get; set; } = "05_ThiCong/05_QS_KhoiLuong";
    public string FinanceFolder { get; set; } = "07_TaiChinh";
    public string HandoverFolder { get; set; } = "08_BanGiao_BaoHanh";
    public bool SupportsAllDrives { get; set; } = true;
    public bool DeleteDriveFilesOnWebDelete { get; set; }
    public bool DeleteWebRecordsOnDriveDelete { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int FileCheckIntervalHours { get; set; } = 6;
    public string? AccountEmail { get; set; }
    public int? ConnectedByUserId { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public int UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
