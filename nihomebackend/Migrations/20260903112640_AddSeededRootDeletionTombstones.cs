using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSeededRootDeletionTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seeded_root_deletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seeded_root_deletions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seeded_root_deletions_ResourceType_ResourceKey",
                table: "seeded_root_deletions",
                columns: new[] { "ResourceType", "ResourceKey" },
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE design_projects
                SET OperationalProjectId = contracts.OperationalProjectId
                FROM design_projects
                INNER JOIN contracts ON contracts.Id = design_projects.ContractId
                WHERE design_projects.ProjectCode LIKE 'DP-SAMPLE-%'
                    AND design_projects.OperationalProjectId IS NULL
                    AND contracts.OperationalProjectId IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM design_projects occupied
                        WHERE occupied.OperationalProjectId = contracts.OperationalProjectId)
                    AND NOT EXISTS (
                        SELECT 1
                        FROM design_projects earlier
                        INNER JOIN contracts earlier_contract ON earlier_contract.Id = earlier.ContractId
                        WHERE earlier.ProjectCode LIKE 'DP-SAMPLE-%'
                            AND earlier.OperationalProjectId IS NULL
                            AND earlier_contract.OperationalProjectId = contracts.OperationalProjectId
                            AND earlier.Id < design_projects.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seeded_root_deletions");
        }
    }
}
