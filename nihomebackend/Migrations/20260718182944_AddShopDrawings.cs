using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddShopDrawings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_drawings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignProjectId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ConstructionItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DrawingCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_drawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_drawings_design_projects_DesignProjectId",
                        column: x => x.DesignProjectId,
                        principalTable: "design_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shop_drawings_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_shop_drawings_DesignProjectId",
                table: "shop_drawings",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_drawings_DesignProjectId_DrawingCode",
                table: "shop_drawings",
                columns: new[] { "DesignProjectId", "DrawingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_drawings_DisciplineCode",
                table: "shop_drawings",
                column: "DisciplineCode");

            migrationBuilder.CreateIndex(
                name: "IX_shop_drawings_OwnerUserId",
                table: "shop_drawings",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_drawings_Status",
                table: "shop_drawings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_drawings");
        }
    }
}
