using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Renames the design-specific manager without changing the underlying
    /// project team role or its authorization behavior. Updates are guarded so
    /// administrator-customized translations remain unchanged.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261007100000_RenameDesignProjectManager")]
    public partial class RenameDesignProjectManager : Migration
    {
        private sealed record Rename(string Key, string LanguageCode, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("designProjects.field.pm", "vi", "Quản lý dự án", "Chủ nhiệm thiết kế"),
            new("designProjects.field.pm", "en", "Project manager", "Design manager"),
            new("designProjects.field.pm", "zh", "项目经理", "设计经理"),
            new("designProjects.field.pm", "ja", "PM", "デザインマネージャー"),
            new("designProjects.team.role.ProjectManager", "vi", "Quản lý dự án", "Chủ nhiệm thiết kế"),
            new("designProjects.team.role.ProjectManager", "en", "Project manager", "Design manager"),
            new("designProjects.team.role.ProjectManager", "zh", "项目经理", "设计经理"),
            new("designProjects.team.role.ProjectManager", "ja", "プロジェクトマネージャー", "デザインマネージャー"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, useNewValue: true);

        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, useNewValue: false);

        private static void Apply(MigrationBuilder migrationBuilder, bool useNewValue)
        {
            foreach (var rename in Renames)
            {
                var expected = useNewValue ? rename.PreviousValue : rename.Value;
                var replacement = useNewValue ? rename.Value : rename.PreviousValue;
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(replacement)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.LanguageCode)}
                      AND Value = {Literal(expected)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
