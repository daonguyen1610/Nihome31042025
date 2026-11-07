using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107103000_AddDesignScheduleDisciplineCode")]
public partial class AddDesignScheduleDisciplineCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisciplineCode",
            table: "design_schedule_tasks",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_design_schedule_tasks_OperationalProjectId_DisciplineCode",
            table: "design_schedule_tasks",
            columns: new[] { "OperationalProjectId", "DisciplineCode" });

        // DepartmentCode now stores an active RBAC role-group code. The old
        // "project-department" master-data codes collapse into the DESIGN group,
        // while the discipline they encoded is preserved in DisciplineCode.
        migrationBuilder.Sql("""
            UPDATE design_schedule_tasks
            SET DisciplineCode = CASE LOWER(LTRIM(RTRIM(DepartmentCode)))
                WHEN N'architecture' THEN N'architecture'
                WHEN N'structural' THEN N'structure'
                WHEN N'mep' THEN N'mep'
                WHEN N'interior' THEN N'interior'
                ELSE NULL
            END;

            UPDATE design_schedule_tasks
            SET DepartmentCode = N'DESIGN'
            WHERE LOWER(LTRIM(RTRIM(DepartmentCode))) IN
                (N'design', N'architecture', N'structural', N'mep', N'interior');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rewriting DepartmentCode from role-group codes back to the old
        // "project-department" master-data values is not reversible.
        migrationBuilder.DropIndex(
            name: "IX_design_schedule_tasks_OperationalProjectId_DisciplineCode",
            table: "design_schedule_tasks");

        migrationBuilder.DropColumn(
            name: "DisciplineCode",
            table: "design_schedule_tasks");
    }
}
