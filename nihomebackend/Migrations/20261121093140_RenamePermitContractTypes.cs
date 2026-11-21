using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// NICON names its permit contracts "Xin phép" (feedback row 25): Xin phép, Thiết kế - Xin phép,
    /// Thi công - Xin phép, Thiết kế - Xin phép - Thi công. The seeder only inserts keys, so rows
    /// are updated here, only where they still hold the seeded wording; admin edits are kept.
    /// </summary>
    public partial class RenamePermitContractTypes : Migration
    {
        private static readonly (string Key, string Language, string Old, string New)[] Labels =
        [
            (@"contracts.type.Permit", "vi", @"Pháp lý", @"Xin phép"),
            (@"contracts.type.DesignAndPermit", "vi", @"Thiết kế và Pháp lý", @"Thiết kế - Xin phép"),
            (@"contracts.type.PermitAndConstruction", "vi", @"Pháp lý và Thi công", @"Thi công - Xin phép"),
            (@"contracts.type.DesignPermitAndConstruction", "vi", @"Thiết kế, Pháp lý và Thi công", @"Thiết kế - Xin phép - Thi công"),
        ];

        private static void Apply(MigrationBuilder migrationBuilder, bool forward)
        {
            foreach (var (key, language, oldValue, newValue) in Labels)
            {
                var from = (forward ? oldValue : newValue).Replace("'", "''");
                var to = (forward ? newValue : oldValue).Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE translations SET Value = N'{to}', UpdatedAt = SYSUTCDATETIME() " +
                    $"WHERE [Key] = N'{key}' AND LanguageCode = N'{language}' AND Value = N'{from}';");
            }
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: false);
    }
}
