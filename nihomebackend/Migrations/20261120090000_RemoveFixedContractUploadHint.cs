using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations;

/// <summary>
/// contracts.documents.uploadHint said "up to 20 MB" although the contract limit is set in
/// Settings. Its replacement is seeded as a new key; the translation seeder never deletes,
/// so the old rows are removed here.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261120090000_RemoveFixedContractUploadHint")]
public partial class RemoveFixedContractUploadHint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DELETE FROM translations WHERE [Key] = N'contracts.documents.uploadHint';");

    // The text is reseeded only if the key returns to the seed files; nothing to restore here.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
