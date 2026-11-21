using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSiteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessSectorCode",
                table: "operational_projects",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConstructionKind",
                table: "operational_projects",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteAddress",
                table: "operational_projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SiteAreaM2",
                table: "operational_projects",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessSectorCode",
                table: "operational_projects");

            migrationBuilder.DropColumn(
                name: "ConstructionKind",
                table: "operational_projects");

            migrationBuilder.DropColumn(
                name: "SiteAddress",
                table: "operational_projects");

            migrationBuilder.DropColumn(
                name: "SiteAreaM2",
                table: "operational_projects");
        }
    }
}
