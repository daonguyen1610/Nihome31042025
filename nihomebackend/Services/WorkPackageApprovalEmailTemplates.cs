using System.Net;
using System.Text.RegularExpressions;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record WorkPackageApprovalTemplate(string Subject, string Body);
public sealed record WorkPackageApprovalPreview(string PreviewToken, string ToEmail, string Subject, string Body,
    IReadOnlyList<string>? CcEmails = null);

public static class WorkPackageApprovalEmailTemplates
{
    public static readonly string[] Tokens = ["recipientName", "workPackageCode", "workPackageName", "recordType", "recordReference", "senderName"];
    public const string DefaultSubject = "NICON – Yêu cầu duyệt {{workPackageCode}}";
    public const string LegacyDefaultBody = "<div style='font-family:Arial,sans-serif;max-width:640px;margin:auto;padding:24px;color:#17233b'><p style='color:#ed1c24;font-size:20px'><strong>NICON – Yêu cầu phê duyệt</strong></p><p>Kính gửi {{recipientName}},</p><p>{{senderName}} đã gửi hồ sơ để anh/chị kiểm tra và phê duyệt.</p><p><strong>Nhóm công việc:</strong> {{workPackageCode}} · {{workPackageName}}</p><p><strong>Loại hồ sơ:</strong> {{recordType}}</p><p><strong>Hồ sơ:</strong> {{recordReference}}</p><p>Vui lòng đăng nhập hệ thống NICON và mở nhóm công việc trên để xem chi tiết. Email này chỉ thông báo; quyết định phê duyệt được thực hiện trong hệ thống.</p></div>";
    public const string DefaultBody = """
        <div style="margin:0;padding:24px;background:#f3f6fb;font-family:Arial,sans-serif;color:#1f2937">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb">
            <tr><td style="padding:18px 24px;background:#e5394a;color:#ffffff">
              <div style="font-size:20px;font-weight:700">NICON</div>
              <div style="font-size:12px;margin:6px 0 0">YÊU CẦU PHÊ DUYỆT · {{workPackageCode}}</div>
            </td></tr>
            <tr><td style="padding:24px">
              <p style="margin:0 0 16px;font-size:15px;line-height:24px">Kính gửi <strong>{{recipientName}}</strong>,</p>
              <p style="margin:0 0 18px;font-size:14px;line-height:22px"><strong>{{senderName}}</strong> đã gửi hồ sơ để anh/chị kiểm tra và phê duyệt.</p>
              <div style="padding:16px;background:#f8fafc;border:1px solid #e5e7eb">
                <div style="font-size:12px;font-weight:700;color:#64748b">NHÓM CÔNG VIỆC</div>
                <p style="margin:8px 0 0;font-size:16px;font-weight:700;line-height:24px">{{workPackageCode}}</p>
                <p style="margin:4px 0 0;font-size:14px;line-height:22px">{{workPackageName}}</p>
                <p style="margin:16px 0 0;font-size:13px;line-height:20px;color:#64748b">Loại hồ sơ</p>
                <p style="margin:4px 0 0;font-size:14px;font-weight:700;line-height:22px">{{recordType}}</p>
                <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#64748b">Hồ sơ</p>
                <p style="margin:4px 0 0;font-size:14px;font-weight:700;line-height:22px">{{recordReference}}</p>
              </div>
              <div style="margin:18px 0 0;padding:16px;background:#fff1f2;color:#9f1239;font-size:14px;line-height:22px">Vui lòng đăng nhập hệ thống NICON và mở nhóm công việc trên để xem chi tiết.</div>
              <p style="margin:16px 0 0;font-size:13px;line-height:20px;color:#64748b">Email này chỉ thông báo; quyết định phê duyệt được thực hiện trong hệ thống.</p>
              <p style="margin:22px 0 0;font-size:14px;line-height:22px">Trân trọng,<br /><strong>NICON</strong></p>
            </td></tr>
            <tr><td style="padding:12px 24px;background:#f8fafc;color:#64748b;font-size:12px;line-height:18px">NICON · Thông báo phê duyệt hồ sơ</td></tr>
          </table>
        </div>
        """;

    public static WorkPackageApprovalTemplate Get(SiteSettings? settings) => new(
        settings?.WorkPackageApprovalEmailSubjectTemplate ?? DefaultSubject,
        settings?.WorkPackageApprovalEmailBodyTemplate ?? DefaultBody);

    public static string Validate(string value, bool body)
    {
        var text = value.Trim();
        var matches = Regex.Matches(text, @"\{\{(\w+)\}\}");
        var remainder = Regex.Replace(text, @"\{\{(\w+)\}\}", "");
        if (text.Length < 1 || text.Length > (body ? 8000 : 200) ||
            (!body && (text.Contains('\r') || text.Contains('\n'))) ||
            matches.Any(match => !Tokens.Contains(match.Groups[1].Value, StringComparer.OrdinalIgnoreCase)) ||
            remainder.Contains("{{") || remainder.Contains("}}") ||
            body && (!EmailTemplateFormatter.IsSafeQuoteHtml(text) ||
                !text.Contains("{{workPackageCode}}", StringComparison.OrdinalIgnoreCase) ||
                !text.Contains("{{recordReference}}", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Use a subject of 1–200 characters and safe HTML of 1–8,000 characters. Use supported variables; the body must include {{workPackageCode}} and {{recordReference}}.");
        return text;
    }

    public static WorkPackageApprovalTemplate Render(WorkPackageApprovalTemplate template, Dictionary<string, string> tokens)
    {
        var subject = EmailTemplateFormatter.ReplaceTokens(template.Subject, tokens);
        var body = EmailTemplateFormatter.ReplaceTokens(template.Body,
            tokens.ToDictionary(pair => pair.Key, pair => WebUtility.HtmlEncode(pair.Value), StringComparer.OrdinalIgnoreCase));
        if (subject.Length > 200 || subject.Contains('\r') || subject.Contains('\n') || body.Length > 16000)
            throw new ArgumentException("Rendered email exceeds supported limits. Shorten the template or record name.");
        return new(subject, body);
    }
}
