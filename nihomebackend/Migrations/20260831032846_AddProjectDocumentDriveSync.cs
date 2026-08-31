using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDocumentDriveSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_documents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceSlot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    LocalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    DesiredOperation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SyncAttemptCount = table.Column<int>(type: "int", nullable: false),
                    SyncError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NextSyncAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DriveFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DriveFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DriveWebViewLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DriveVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DriveModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDownloadable = table.Column<bool>(type: "bit", nullable: false),
                    UnsupportedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConflictObservedDriveFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConflictObservedDriveVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConflictState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConflictWithDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_documents_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_documents_project_documents_ConflictWithDocumentId",
                        column: x => x.ConflictWithDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "project_drive_folders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DriveFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DriveWebViewLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciliationClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciliationClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_drive_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_drive_folders_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_ClaimToken",
                table: "project_documents",
                column: "ClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_ConflictWithDocumentId_ConflictObservedDriveFileId_ConflictObservedDriveVersion",
                table: "project_documents",
                columns: new[] { "ConflictWithDocumentId", "ConflictObservedDriveFileId", "ConflictObservedDriveVersion" },
                unique: true,
                filter: "[ConflictWithDocumentId] IS NOT NULL AND [ConflictObservedDriveFileId] IS NOT NULL AND [ConflictObservedDriveVersion] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_DriveFileId",
                table: "project_documents",
                column: "DriveFileId",
                unique: true,
                filter: "[DriveFileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_OperationalProjectId_Category_UpdatedAt",
                table: "project_documents",
                columns: new[] { "OperationalProjectId", "Category", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_OperationalProjectId_SourceModule_SourceEntityType_SourceSlot_SourceRecordId_LocalPath",
                table: "project_documents",
                columns: new[] { "OperationalProjectId", "SourceModule", "SourceEntityType", "SourceSlot", "SourceRecordId", "LocalPath" },
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_documents_SyncStatus_NextSyncAttemptAt",
                table: "project_documents",
                columns: new[] { "SyncStatus", "NextSyncAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_drive_folders_DriveFolderId",
                table: "project_drive_folders",
                column: "DriveFolderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_drive_folders_OperationalProjectId_Category",
                table: "project_drive_folders",
                columns: new[] { "OperationalProjectId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_drive_folders_ReconciliationClaimExpiresAt",
                table: "project_drive_folders",
                column: "ReconciliationClaimExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_documents");

            migrationBuilder.DropTable(
                name: "project_drive_folders");
        }
    }
}
