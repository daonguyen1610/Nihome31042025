using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddIfcReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ifc_releases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    ReleaseNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ifc_releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ifc_releases_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ifc_releases_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ifc_release_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IfcReleaseId = table.Column<int>(type: "int", nullable: false),
                    ShopDrawingId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ifc_release_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ifc_release_items_ifc_releases_IfcReleaseId",
                        column: x => x.IfcReleaseId,
                        principalTable: "ifc_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ifc_release_items_shop_drawings_ShopDrawingId",
                        column: x => x.ShopDrawingId,
                        principalTable: "shop_drawings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ifc_release_recipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IfcReleaseId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientTypeCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgementNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ifc_release_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ifc_release_recipients_ifc_releases_IfcReleaseId",
                        column: x => x.IfcReleaseId,
                        principalTable: "ifc_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_items_IfcReleaseId_ShopDrawingId",
                table: "ifc_release_items",
                columns: new[] { "IfcReleaseId", "ShopDrawingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_items_ShopDrawingId",
                table: "ifc_release_items",
                column: "ShopDrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_recipients_IfcReleaseId",
                table: "ifc_release_recipients",
                column: "IfcReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_releases_DesignProjectId",
                table: "ifc_releases",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_releases_DesignProjectId_ReleaseNumber",
                table: "ifc_releases",
                columns: new[] { "DesignProjectId", "ReleaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ifc_releases_IssuedByUserId",
                table: "ifc_releases",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_releases_Status",
                table: "ifc_releases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ifc_release_items");

            migrationBuilder.DropTable(
                name: "ifc_release_recipients");

            migrationBuilder.DropTable(
                name: "ifc_releases");
        }
    }
}
