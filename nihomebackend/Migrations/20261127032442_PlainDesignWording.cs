using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>Replaces English terms in four design messages with the product's Vietnamese wording.</summary>
    public partial class PlainDesignWording : Migration
    {
        internal static readonly (string Key, string Lang, string Old, string New)[] Changes =
        [
            ("basicDesign.readiness.clientTitle", "vi",
                "Khách hàng duyệt hồ sơ trình Basic Design",
                "Khách hàng duyệt hồ sơ trình Thiết kế cơ sở"),
            ("concepts.dossier.finalizeHint", "vi",
                "Cần khách duyệt revision mới nhất và TK-M03 đã ký. Ghi nhận tại tab Trình duyệt.",
                "Cần khách duyệt phiên bản mới nhất và đã ký biên bản nghiệm thu hồ sơ (TK-M03). Ghi nhận tại tab Trình duyệt."),
            ("concepts.dossier.finalizeHint", "en",
                "Record client approval of the latest revision and signed TK-M03 in Reviews before finalizing.",
                "Before finalizing, record in Reviews the client approval of the latest version and the signed dossier acceptance record (TK-M03)."),
            ("concepts.dossier.finalizeHint", "zh",
                "请先在评审页记录最新版本的客户批准和已签署的TK-M03。",
                "定稿前，请在“审批”页记录客户对最新版本的批准，以及已签署的资料验收记录（TK-M03）。"),
            ("concepts.dossier.finalizeHint", "ja",
                "確定前にレビュー画面で最新版の顧客承認と署名済みTK-M03を登録してください。",
                "確定前に、承認タブで最新版のお客様承認と署名済みの図書検収書（TK-M03）を登録してください。"),
            ("concepts.dossier.supersededDraft", "vi",
                "Revision này đã được trình trong đợt khác. Bản nháp này được giữ để tham khảo và không chặn quyết định khách hàng.",
                "Phiên bản này đã được trình trong đợt khác. Bản nháp được giữ để tham khảo và không chặn quyết định của khách hàng."),
            ("shopDrawing.confirmReject", "vi",
                "Chuyển \"{title}\" sang Từ chối? Đây là trạng thái cuối, muốn làm tiếp phải tạo bản vẽ hoặc revision mới.",
                "Chuyển \"{title}\" sang Từ chối? Đây là trạng thái cuối, muốn làm tiếp phải tạo bản vẽ hoặc phiên bản mới."),
        ];

        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: true);

        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: false);

        // Only untouched default text changes; wording an administrator edited stays.
        private static void Apply(MigrationBuilder migrationBuilder, bool forward)
        {
            foreach (var (key, lang, oldValue, newValue) in Changes)
            {
                var (from, to) = forward ? (oldValue, newValue) : (newValue, oldValue);
                migrationBuilder.Sql($"""
                    UPDATE translations SET Value = {Sql(to)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Sql(key)} AND LanguageCode = {Sql(lang)} AND Value COLLATE Latin1_General_100_BIN2 = {Sql(from)};
                    """);
            }
        }

        private static string Sql(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
