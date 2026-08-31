using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class StoreGoogleDriveSettingsInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProtectedRefreshToken",
                table: "google_drive_credentials",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<int>(
                name: "ConnectedByUserId",
                table: "google_drive_credentials",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ConnectedAt",
                table: "google_drive_credentials",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationName",
                table: "google_drive_credentials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Nicon Google Drive Integration");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "google_drive_credentials",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConstructionAcceptanceFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "04_Thi_cong_Nghiem_thu");

            migrationBuilder.AddColumn<string>(
                name: "CrmPreDesignFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "01_CRM_PreDesign");

            migrationBuilder.AddColumn<string>(
                name: "DesignBasicFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "02_Thiet_ke/02_Co_so");

            migrationBuilder.AddColumn<string>(
                name: "DesignConceptFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "02_Thiet_ke/01_So_bo_Concept");

            migrationBuilder.AddColumn<string>(
                name: "DesignShopDrawingFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "02_Thiet_ke/03_Chi_tiet_ShopDrawing");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "google_drive_credentials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FinanceContractsFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "06_Tai_chinh_Hop_dong");

            migrationBuilder.AddColumn<string>(
                name: "FrontendReturnUrl",
                table: "google_drive_credentials",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "/admin/settings?tab=drive");

            migrationBuilder.AddColumn<string>(
                name: "InstanceId",
                table: "google_drive_credentials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalPermitsFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "03_Xin_phep_Phap_ly");

            migrationBuilder.AddColumn<string>(
                name: "OAuthRedirectUri",
                table: "google_drive_credentials",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PollIntervalSeconds",
                table: "google_drive_credentials",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "ProcurementFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "05_Cung_ung_Vat_tu");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedClientSecret",
                table: "google_drive_credentials",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootFolderId",
                table: "google_drive_credentials",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SupportsAllDrives",
                table: "google_drive_credentials",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SurveyMediaFolder",
                table: "google_drive_credentials",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "01_Khao_sat");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "google_drive_credentials",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationName",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "ConstructionAcceptanceFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "CrmPreDesignFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "DesignBasicFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "DesignConceptFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "DesignShopDrawingFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "FinanceContractsFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "FrontendReturnUrl",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "LegalPermitsFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "OAuthRedirectUri",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "PollIntervalSeconds",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "ProcurementFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "ProtectedClientSecret",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "RootFolderId",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "SupportsAllDrives",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "SurveyMediaFolder",
                table: "google_drive_credentials");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "google_drive_credentials");

            migrationBuilder.AlterColumn<string>(
                name: "ProtectedRefreshToken",
                table: "google_drive_credentials",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ConnectedByUserId",
                table: "google_drive_credentials",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ConnectedAt",
                table: "google_drive_credentials",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
