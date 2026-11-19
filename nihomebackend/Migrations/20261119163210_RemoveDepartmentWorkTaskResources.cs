using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentWorkTaskResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Resources",
                table: "department_work_tasks");
            // The seeder only inserts or updates, so remove the field's label rows here.
            migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'workPackages.field.resources';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Resources",
                table: "department_work_tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
