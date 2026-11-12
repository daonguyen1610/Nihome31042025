using System.Net;
using System.Text.RegularExpressions;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record QuoteApprovalTemplate(string Subject, string Body);

public static class QuoteApprovalEmailTemplates
{
    public static readonly string[] Tokens = ["recipientName", "senderName", "quoteCode", "quoteVersion", "grandTotal", "currency", "validUntil", "quoteUrl"];
    public const string DefaultSubject = "NICON – Duyệt báo giá {{quoteCode}}";
    public static string DefaultBody => EmailTemplateBaseline.Wrap(
        "YÊU CẦU DUYỆT BÁO GIÁ · {{quoteCode}}",
        """
        <p style="margin:0 0 16px;font-size:15px;line-height:24px">Kính gửi <strong>{{recipientName}}</strong>,</p>
        <p style="margin:0 0 18px;font-size:14px;line-height:22px"><strong>{{senderName}}</strong> đã gửi báo giá để anh/chị kiểm tra và phê duyệt.</p>
        <div style="padding:16px;background:#f8fafc;border:1px solid #e5e7eb">
          <div style="font-size:12px;font-weight:700;color:#64748b">THÔNG TIN BÁO GIÁ</div>
          <p style="margin:8px 0 0;font-size:16px;font-weight:700;line-height:24px">{{quoteCode}} · Phiên bản {{quoteVersion}}</p>
          <p style="margin:16px 0 0;font-size:13px;line-height:20px;color:#64748b">Tổng giá trị</p>
          <p style="margin:4px 0 0;font-size:18px;font-weight:700;line-height:26px">{{grandTotal}} {{currency}}</p>
          <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#64748b">Hiệu lực đến</p>
          <p style="margin:4px 0 0;font-size:14px;font-weight:700;line-height:22px">{{validUntil}}</p>
        </div>
        <div style="margin:18px 0 0;padding:16px;background:#fff1f2;color:#9f1239;font-size:14px;line-height:22px">Vui lòng đăng nhập NICON, kiểm tra báo giá và ghi nhận quyết định trong hệ thống.</div>
        <p style="margin:16px 0 0;font-size:14px;line-height:22px"><a href="{{quoteUrl}}">Mở báo giá để duyệt hoặc từ chối</a></p>
        <p style="margin:16px 0 0;font-size:13px;line-height:20px;color:#64748b">Email này chỉ thông báo; quyết định phê duyệt không được thực hiện qua email.</p>
        <p style="margin:22px 0 0;font-size:14px;line-height:22px">Trân trọng,<br /><strong>NICON</strong></p>
        """,
        "NICON · Thông báo phê duyệt báo giá");

    public static QuoteApprovalTemplate Get(SiteSettings? settings) => new(
        settings?.QuoteApprovalEmailSubjectTemplate ?? DefaultSubject,
        EmailTemplateBaseline.Apply(
            settings?.QuoteApprovalEmailBodyTemplate ?? DefaultBody,
            "YÊU CẦU DUYỆT BÁO GIÁ · {{quoteCode}}", "NICON · Thông báo phê duyệt báo giá"));

    public static string Validate(string value, bool body)
    {
        var text = value.Trim();
        var matches = Regex.Matches(text, @"\{\{(\w+)\}\}");
        var remainder = Regex.Replace(text, @"\{\{(\w+)\}\}", "");
        if (text.Length < 1 || text.Length > (body ? 8000 : 200) ||
            (!body && (text.Contains('\r') || text.Contains('\n'))) ||
            matches.Any(match => !Tokens.Contains(match.Groups[1].Value, StringComparer.OrdinalIgnoreCase)) ||
            remainder.Contains("{{") || remainder.Contains("}}") ||
            body && (!WorkPackageApprovalEmailTemplates.IsSafeApprovalHtml(text.Replace("{{quoteUrl}}", "{{workPackageUrl}}")) ||
                !text.Contains("{{quoteCode}}", StringComparison.OrdinalIgnoreCase) ||
                !text.Contains("<a href=\"{{quoteUrl}}\">", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Mẫu email cần tiêu đề 1–200 ký tự và HTML an toàn 1–8.000 ký tự. Nội dung phải có {{quoteCode}} và liên kết <a href=\"{{quoteUrl}}\">; chỉ dùng biến được hỗ trợ.");
        return text;
    }

    public static QuoteApprovalTemplate Render(QuoteApprovalTemplate template, Dictionary<string, string> tokens)
    {
        var subject = EmailTemplateFormatter.ReplaceTokens(template.Subject, tokens);
        var body = EmailTemplateFormatter.ReplaceTokens(template.Body,
            tokens.ToDictionary(pair => pair.Key, pair => WebUtility.HtmlEncode(pair.Value), StringComparer.OrdinalIgnoreCase));
        if (subject.Length > 200 || subject.Contains('\r') || subject.Contains('\n') || body.Length > 16000)
            throw new ArgumentException("Email sau khi điền dữ liệu quá dài. Vui lòng rút ngắn mẫu hoặc dữ liệu báo giá.");
        return new(subject, body);
    }
}
