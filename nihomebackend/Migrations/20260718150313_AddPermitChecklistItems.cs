using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPermitChecklistItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permit_checklist_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    PermitTypeCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IssuingAgency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    TargetDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuedFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permit_checklist_items_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permit_checklist_items_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_DesignProjectId",
                table: "permit_checklist_items",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_DesignProjectId_PermitTypeCode",
                table: "permit_checklist_items",
                columns: new[] { "DesignProjectId", "PermitTypeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_ExpiresAt",
                table: "permit_checklist_items",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_OwnerUserId",
                table: "permit_checklist_items",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_Status",
                table: "permit_checklist_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_permit_checklist_items_TargetDeadline",
                table: "permit_checklist_items",
                column: "TargetDeadline");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permit_checklist_items");
        }
    }
}
