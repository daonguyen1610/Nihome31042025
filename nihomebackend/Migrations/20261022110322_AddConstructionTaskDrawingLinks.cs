using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionTaskDrawingLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DrawingLinksVersion",
                table: "construction_tasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "construction_task_drawings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConstructionTaskId = table.Column<int>(type: "int", nullable: false),
                    IfcReleaseItemId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_construction_task_drawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_construction_task_drawings_construction_tasks_ConstructionTaskId",
                        column: x => x.ConstructionTaskId,
                        principalTable: "construction_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_construction_task_drawings_ifc_release_items_IfcReleaseItemId",
                        column: x => x.IfcReleaseItemId,
                        principalTable: "ifc_release_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_construction_task_drawings_ConstructionTaskId_IfcReleaseItemId",
                table: "construction_task_drawings",
                columns: new[] { "ConstructionTaskId", "IfcReleaseItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_construction_task_drawings_IfcReleaseItemId",
                table: "construction_task_drawings",
                column: "IfcReleaseItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "construction_task_drawings");

            migrationBuilder.DropColumn(
                name: "DrawingLinksVersion",
                table: "construction_tasks");
        }
    }
}
