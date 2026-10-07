using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Restores the shared ProjectManager role label after the design-specific
    /// manager was given its own scope-aware translation key.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261007113000_DistinguishDesignManagerRole")]
    public partial class DistinguishDesignManagerRole : Migration
    {
        private sealed record Rename(string LanguageCode, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("vi", "Chủ nhiệm thiết kế", "Quản lý dự án"),
            new("en", "Design manager", "Project manager"),
            new("zh", "设计经理", "项目经理"),
            new("ja", "デザインマネージャー", "プロジェクトマネージャー"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, restoreProjectManager: true);

        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, restoreProjectManager: false);

        private static void Apply(MigrationBuilder migrationBuilder, bool restoreProjectManager)
        {
            foreach (var rename in Renames)
            {
                var expected = restoreProjectManager ? rename.PreviousValue : rename.Value;
                var replacement = restoreProjectManager ? rename.Value : rename.PreviousValue;
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(replacement)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = N'designProjects.team.role.ProjectManager'
                      AND LanguageCode = {Literal(rename.LanguageCode)}
                      AND Value = {Literal(expected)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
