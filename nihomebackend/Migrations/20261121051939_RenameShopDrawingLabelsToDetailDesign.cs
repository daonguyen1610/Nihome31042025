using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// NICON calls the detail design stage "Detail Design" (feedback row 13): labels that still
    /// said "… / Shop Drawing" now say "Thiết kế chi tiết", and zh/ja drawing messages of that
    /// stage no longer call them construction drawings. The seeder only inserts keys, so rows are
    /// updated here, only where they still hold the seeded wording; admin edits are kept.
    /// </summary>
    public partial class RenameShopDrawingLabelsToDetailDesign : Migration
    {
        private static readonly (string Key, string Language, string Old, string New)[] Labels =
        [
            (@"settings.drive.configuration.folder.designShopDrawing", "vi", @"Thiết kế chi tiết / Shop Drawing", @"Thiết kế chi tiết"),
            (@"settings.drive.configuration.folder.designShopDrawing", "en", @"Detailed design / Shop drawing", @"Detail design"),
            (@"settings.drive.configuration.folder.designShopDrawing", "zh", @"详细设计 / 施工图", @"详细设计"),
            (@"settings.drive.configuration.folder.designShopDrawing", "ja", @"詳細設計 / 施工図", @"詳細設計"),
            (@"operationalProjects.documents.category.DesignShopDrawing", "vi", @"Thiết kế chi tiết / Shop Drawing", @"Thiết kế chi tiết"),
            (@"operationalProjects.documents.category.DesignShopDrawing", "en", @"Detailed design / Shop drawings", @"Detail design"),
            (@"operationalProjects.documents.category.DesignShopDrawing", "zh", @"深化设计 / 施工图", @"详细设计"),
            (@"operationalProjects.documents.category.DesignShopDrawing", "ja", @"詳細設計・施工図", @"詳細設計"),
            (@"shopDrawing.new", "zh", @"新建施工图", @"新建图纸"),
            (@"shopDrawing.new", "ja", @"新規施工図", @"新規図面"),
            (@"shopDrawing.edit", "zh", @"编辑施工图", @"编辑图纸"),
            (@"shopDrawing.edit", "ja", @"施工図を編集", @"図面を編集"),
            (@"shopDrawing.created", "zh", @"已创建施工图", @"已创建图纸"),
            (@"shopDrawing.created", "ja", @"施工図を作成しました", @"図面を作成しました"),
            (@"shopDrawing.updated", "zh", @"已更新施工图", @"已更新图纸"),
            (@"shopDrawing.updated", "ja", @"施工図を更新しました", @"図面を更新しました"),
            (@"shopDrawing.deleted", "zh", @"已删除施工图", @"已删除图纸"),
            (@"shopDrawing.deleted", "ja", @"施工図を削除しました", @"図面を削除しました"),
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
