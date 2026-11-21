using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// The project list is now a table (NICON feedback row 34), so the English opportunity count
/// label is a column header and reads "Opportunities" instead of the card's short "Opps". The
/// seeder only inserts keys, so the row is updated here, only while it still holds the seeded
/// wording; an administrator's edit is kept.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261121160000_RenameProjectOpportunityCountLabel")]
public partial class RenameProjectOpportunityCountLabel : Migration
{
    private static string Rename(string from, string to) =>
        $"UPDATE translations SET Value = N'{to}', UpdatedAt = SYSUTCDATETIME() " +
        $"WHERE [Key] = N'operationalProjects.count.opportunities' AND LanguageCode = N'en' AND Value = N'{from}';";

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Rename("Opps", "Opportunities"));

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Rename("Opportunities", "Opps"));
}
