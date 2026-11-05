using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105180000_SimplifyQuoteEmailWording")]
public partial class SimplifyQuoteEmailWording : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Email content', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.bodyLabel' AND LanguageCode = N'en' AND Value = N'Email HTML body';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'邮件内容', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.bodyLabel' AND LanguageCode = N'zh' AND Value = N'邮件 HTML 正文';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'メール本文', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.bodyLabel' AND LanguageCode = N'ja' AND Value = N'メール HTML 本文';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'Preview the message on the right. Keep the item list and total in the email. Only quote managers may edit before sending.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.hint' AND LanguageCode = N'en' AND Value = N'Edit HTML and preview it on the right. Keep {{quoteLines}} and {{grandTotal}}; use basic HTML/inline CSS only. Only quote managers may edit before sending.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'请在右侧预览。邮件须保留项目明细和总金额。只有报价管理员可在发送前修改。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.hint' AND LanguageCode = N'zh' AND Value = N'可编辑 HTML 并在右侧预览。请保留 {{quoteLines}} 和 {{grandTotal}}；仅使用基础 HTML/内联 CSS。只有报价管理员可在发送前修改。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'右側で内容を確認してください。明細と合計金額を残してください。送信前に編集できるのは見積管理者のみです。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.hint' AND LanguageCode = N'ja' AND Value = N'HTML を編集して右側でプレビューできます。{{quoteLines}} と {{grandTotal}} を残し、基本的な HTML とインライン CSS のみ使用してください。送信前の編集は見積管理者のみ可能です。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'The quote message format is invalid. Check the content and try again.', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.htmlRequired' AND LanguageCode = N'en' AND Value = N'The quote template body must be valid HTML and start with an HTML tag.';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'报价内容格式不正确。请检查后重试。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.htmlRequired' AND LanguageCode = N'zh' AND Value = N'报价模板正文必须是有效的 HTML，并以 HTML 标签开头。';""");
        migrationBuilder.Sql("""UPDATE translations SET Value = N'見積メールの形式が正しくありません。内容を確認して再度お試しください。', UpdatedAt = SYSUTCDATETIME() WHERE [Key] = N'emailTemplate.quote.htmlRequired' AND LanguageCode = N'ja' AND Value = N'見積テンプレート本文は有効な HTML で、HTML タグから始めてください。';""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain clearer wording and later administrator edits.
    }
}
