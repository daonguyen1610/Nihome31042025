using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqBidComparison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grant only this new capability; preserve every existing permission assignment.
            migrationBuilder.Sql("""
                INSERT INTO permissions (Module, Action, DescriptionKey, IsActive, CreatedAt)
                SELECT 'proc.rfqs', source.Action, CONCAT('rbac.perm.proc.rfqs.', source.Action), 1, SYSUTCDATETIME()
                FROM (VALUES ('view'), ('manage'), ('export'), ('award')) source(Action)
                WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.Module = 'proc.rfqs' AND p.Action = source.Action);

                INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
                SELECT r.Id, p.Id, SYSUTCDATETIME() FROM roles r CROSS JOIN permissions p
                WHERE p.Module = 'proc.rfqs' AND (
                    r.Code IN ('SUPER_ADMIN', 'ADMIN') OR
                    r.Code = 'PROCUREMENT' AND p.Action IN ('view', 'manage', 'export') OR
                    r.Code = 'BGD' AND p.Action IN ('view', 'award', 'export') OR
                    r.Code = 'PM' AND p.Action = 'view')
                AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
                """);

            migrationBuilder.CreateTable(
                name: "rfq_bid_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqBidId = table.Column<int>(type: "int", nullable: false),
                    RfqLineId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_bid_lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rfq_bids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfq_bids_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfq_bids_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "rfqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceBoqRevisionId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SelectedBidId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AwardedByUserId = table.Column<int>(type: "int", nullable: true),
                    AwardReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AwardSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverdueNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfqs_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfqs_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfqs_project_boq_revisions_SourceBoqRevisionId",
                        column: x => x.SourceBoqRevisionId,
                        principalTable: "project_boq_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfqs_rfq_bids_SelectedBidId",
                        column: x => x.SelectedBidId,
                        principalTable: "rfq_bids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_rfqs_users_AwardedByUserId",
                        column: x => x.AwardedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_rfqs_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "rfq_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    RfqBidId = table.Column<int>(type: "int", nullable: true),
                    ProjectDocumentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfq_documents_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfq_documents_rfq_bids_RfqBidId",
                        column: x => x.RfqBidId,
                        principalTable: "rfq_bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfq_documents_rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfq_events_rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rfq_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "rfq_invitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfq_invitations_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfq_invitations_rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rfq_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfqId = table.Column<int>(type: "int", nullable: false),
                    ProjectBoqLineId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    BudgetUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfq_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rfq_lines_project_boq_lines_ProjectBoqLineId",
                        column: x => x.ProjectBoqLineId,
                        principalTable: "project_boq_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rfq_lines_rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rfq_bid_lines_RfqBidId_RfqLineId",
                table: "rfq_bid_lines",
                columns: new[] { "RfqBidId", "RfqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_bid_lines_RfqLineId",
                table: "rfq_bid_lines",
                column: "RfqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_bids_RfqId_VendorId_Revision",
                table: "rfq_bids",
                columns: new[] { "RfqId", "VendorId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_bids_SubmittedByUserId",
                table: "rfq_bids",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_bids_VendorId",
                table: "rfq_bids",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_documents_ProjectDocumentId",
                table: "rfq_documents",
                column: "ProjectDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_documents_RfqBidId",
                table: "rfq_documents",
                column: "RfqBidId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_documents_RfqId",
                table: "rfq_documents",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_events_ActorUserId",
                table: "rfq_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_events_RfqId",
                table: "rfq_events",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_invitations_RfqId_VendorId",
                table: "rfq_invitations",
                columns: new[] { "RfqId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_invitations_VendorId",
                table: "rfq_invitations",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_lines_ProjectBoqLineId",
                table: "rfq_lines",
                column: "ProjectBoqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_rfq_lines_RfqId_ProjectBoqLineId",
                table: "rfq_lines",
                columns: new[] { "RfqId", "ProjectBoqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_AwardedByUserId",
                table: "rfqs",
                column: "AwardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_Code",
                table: "rfqs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_ContractId",
                table: "rfqs",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_OperationalProjectId_Status_DueAt",
                table: "rfqs",
                columns: new[] { "OperationalProjectId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_OwnerUserId",
                table: "rfqs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_SelectedBidId",
                table: "rfqs",
                column: "SelectedBidId");

            migrationBuilder.CreateIndex(
                name: "IX_rfqs_SourceBoqRevisionId",
                table: "rfqs",
                column: "SourceBoqRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_rfq_bid_lines_rfq_bids_RfqBidId",
                table: "rfq_bid_lines",
                column: "RfqBidId",
                principalTable: "rfq_bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rfq_bid_lines_rfq_lines_RfqLineId",
                table: "rfq_bid_lines",
                column: "RfqLineId",
                principalTable: "rfq_lines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_rfq_bids_rfqs_RfqId",
                table: "rfq_bids",
                column: "RfqId",
                principalTable: "rfqs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp FROM role_permissions rp INNER JOIN permissions p ON p.Id = rp.PermissionId WHERE p.Module = 'proc.rfqs';
                DELETE FROM permissions WHERE Module = 'proc.rfqs';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_rfqs_rfq_bids_SelectedBidId",
                table: "rfqs");

            migrationBuilder.DropTable(
                name: "rfq_bid_lines");

            migrationBuilder.DropTable(
                name: "rfq_documents");

            migrationBuilder.DropTable(
                name: "rfq_events");

            migrationBuilder.DropTable(
                name: "rfq_invitations");

            migrationBuilder.DropTable(
                name: "rfq_lines");

            migrationBuilder.DropTable(
                name: "rfq_bids");

            migrationBuilder.DropTable(
                name: "rfqs");
        }
    }
}
