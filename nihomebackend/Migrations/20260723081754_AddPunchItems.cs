using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "punch_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    PunchCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: true),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_punch_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_punch_items_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_punch_items_users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_punch_items_users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_AssigneeUserId",
                table: "punch_items",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_DesignProjectId",
                table: "punch_items",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_DesignProjectId_PunchCode",
                table: "punch_items",
                columns: new[] { "DesignProjectId", "PunchCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_Severity",
                table: "punch_items",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_Status",
                table: "punch_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_VerifiedByUserId",
                table: "punch_items",
                column: "VerifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "punch_items");
        }
    }
}
