using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Adds a setting for each PRD v2 tree folder that had none, so NICON can rename any of them. Settings still on the old default Drive paths switch to the PRD v2 tree, even when project
    /// folders are already linked (AdoptPrdV2DriveFolderTree skipped those). Like saving new paths in
    /// Settings, it drops the folder links so sync builds the new tree; nothing on Drive is moved or deleted.
    /// No production data existed when this was decided (2026-11-26). Custom paths are left alone.
    /// As-built and handover files move to the Handover category (08_BanGiao_BaoHanh), and FinanceContracts is
    /// relabelled "Contracts" now that Finance (07_TaiChinh) is its own category.
    /// </summary>
    public partial class SwitchDriveFoldersToPrdV2 : Migration
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

        private static readonly (string Column, string Path)[] AddedFolders =
        [
            ("DesignIfcFolder", "03_ThietKe/03_ChiTiet_IFC"),
            ("ConstructionPreparationFolder", "05_ThiCong/01_ChuanBi"),
            ("ConstructionDiaryFolder", "05_ThiCong/02_NhatKy_BaoCao"),
            ("HseFolder", "05_ThiCong/04_ATLD_HSE"),
            ("QuantitySurveyFolder", "05_ThiCong/05_QS_KhoiLuong"),
            ("FinanceFolder", "07_TaiChinh"),
            ("HandoverFolder", "08_BanGiao_BaoHanh"),
        ];

        // TranslationSeeder only inserts missing keys. A row changes only while it still holds the seed text.
        private static readonly (string Key, string Lang, string Old, string New)[] Labels =
        [
            ("operationalProjects.documents.category.FinanceContracts", "vi", "Tài chính và hợp đồng", "Hợp đồng"),
            ("operationalProjects.documents.category.FinanceContracts", "en", "Finance and contracts", "Contracts"),
            ("operationalProjects.documents.category.FinanceContracts", "zh", "财务与合同", "合同"),
            ("operationalProjects.documents.category.FinanceContracts", "ja", "財務・契約", "契約"),
            ("settings.drive.configuration.folder.financeContracts", "vi", "Tài chính / Hợp đồng", "Hợp đồng"),
            ("settings.drive.configuration.folder.financeContracts", "en", "Finance / Contracts", "Contracts"),
            ("settings.drive.configuration.folder.financeContracts", "zh", "财务 / 合同", "合同"),
            ("settings.drive.configuration.folder.financeContracts", "ja", "財務 / 契約", "契約"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, path) in AddedFolders)
                migrationBuilder.AddColumn<string>(
                    name: column, table: "google_drive_credentials", type: "nvarchar(500)",
                    maxLength: 500, nullable: false, defaultValue: path);
            Switch(migrationBuilder, OldPaths, NewPaths);
            // Category is stored as its enum name.
            Recategorize(migrationBuilder, "ConstructionAcceptance", "Handover");
            foreach (var label in Labels) Relabel(migrationBuilder, label.Key, label.Lang, label.Old, label.New);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Switch(migrationBuilder, NewPaths, OldPaths);
            Recategorize(migrationBuilder, "Handover", "ConstructionAcceptance");
            foreach (var label in Labels) Relabel(migrationBuilder, label.Key, label.Lang, label.New, label.Old);
            foreach (var (column, _) in AddedFolders)
                migrationBuilder.DropColumn(name: column, table: "google_drive_credentials");
        }

        private static void Switch(MigrationBuilder migrationBuilder, string[] from, string[] to)
        {
            var matches = string.Join(" AND ", Columns.Select((column, i) => $"{column} = N'{from[i]}'"));
            // Links first: the condition reads the paths the next statement replaces.
            migrationBuilder.Sql(
                $"DELETE FROM project_drive_folders WHERE EXISTS (SELECT 1 FROM google_drive_credentials WHERE {matches});");
            migrationBuilder.Sql(
                $"UPDATE google_drive_credentials SET {string.Join(", ", Columns.Select((column, i) => $"{column} = N'{to[i]}'"))} WHERE {matches};");
        }

        private static void Recategorize(MigrationBuilder migrationBuilder, string from, string to) =>
            migrationBuilder.Sql(
                $"UPDATE project_documents SET Category = N'{to}' WHERE Category = N'{from}' " +
                "AND SourceEntityType IN (N'HandoverRecord', N'AsBuiltDocument');");

        private static void Relabel(MigrationBuilder migrationBuilder, string key, string lang, string from, string to) =>
            migrationBuilder.Sql(
                $"UPDATE translations SET Value = N'{to}' WHERE [Key] = N'{key}' AND LanguageCode = N'{lang}' AND Value = N'{from}';");
    }
}
