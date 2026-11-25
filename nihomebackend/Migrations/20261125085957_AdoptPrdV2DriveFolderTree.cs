using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Moves untouched default Drive folder settings to the PRD v2 eight-folder tree. Only runs while no
    /// project folder is bound yet, so live bindings and files are never re-routed (AGENTS.md topology rule).
    /// </summary>
    public partial class AdoptPrdV2DriveFolderTree : Migration
    {
        private static readonly string[] Columns =
        [
            "SurveyMediaFolder", "CrmPreDesignFolder", "DesignConceptFolder", "DesignBasicFolder",
            "DesignShopDrawingFolder", "LegalPermitsFolder", "ConstructionAcceptanceFolder",
            "ProcurementFolder", "FinanceContractsFolder",
        ];

        private static readonly string[] OldPaths =
        [
            "01_Khao_sat", "01_CRM_PreDesign", "02_Thiet_ke/01_So_bo_Concept", "02_Thiet_ke/02_Co_so",
            "02_Thiet_ke/03_Chi_tiet_ShopDrawing", "03_Xin_phep_Phap_ly", "04_Thi_cong_Nghiem_thu",
            "05_Cung_ung_Vat_tu", "06_Tai_chinh_Hop_dong",
        ];

        private static readonly string[] NewPaths =
        [
            "01_CoHoi_BaoGia/01_KhaoSat", "01_CoHoi_BaoGia", "03_ThietKe/01_Concept", "03_ThietKe/02_CoSo",
            "03_ThietKe/04_ShopDrawing", "04_PhapLy", "05_ThiCong/03_ChatLuong_NghiemThu",
            "06_CungUng", "02_HopDong",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(Switch(OldPaths, NewPaths));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(Switch(NewPaths, OldPaths));

        private static string Switch(string[] from, string[] to) =>
            $"UPDATE google_drive_credentials SET {string.Join(", ", Columns.Select((column, i) => $"{column} = N'{to[i]}'"))} " +
            $"WHERE {string.Join(" AND ", Columns.Select((column, i) => $"{column} = N'{from[i]}'"))} " +
            "AND NOT EXISTS (SELECT 1 FROM project_drive_folders);";
    }
}
