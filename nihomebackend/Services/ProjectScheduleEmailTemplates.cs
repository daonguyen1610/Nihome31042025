using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record ProjectScheduleEmailTemplate(string Subject, string Body);

public static partial class ProjectScheduleEmailTemplates
{
    public const string DefaultSubject = "NICON · {{eventName}} · {{projectCode}} · v{{version}}";
    public const string DefaultBody = """
        <div style="margin:0;padding:24px;background:#f3f6fb;font-family:Arial,sans-serif;color:#1f2937">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb">
            <tr><td style="padding:18px 24px;background:#e5394a;color:#ffffff">
              <div style="font-size:20px;font-weight:700">NICON</div>
              <div style="font-size:12px;margin:6px 0 0">TIẾN ĐỘ TỔNG THỂ · {{eventName}}</div>
            </td></tr>
            <tr><td style="padding:24px">
              <p style="margin:0 0 16px;font-size:15px;line-height:24px">Kính gửi <strong>{{recipientName}}</strong>,</p>
              <p style="margin:0 0 18px;font-size:14px;line-height:22px">Tiến độ tổng thể của dự án dưới đây đang cần anh/chị theo dõi trong NICON.</p>
              <div style="padding:16px;background:#f8fafc;border:1px solid #e5e7eb">
                <div style="font-size:12px;font-weight:700;color:#64748b">DỰ ÁN · PHIÊN BẢN v{{version}}</div>
                <p style="margin:8px 0 0;font-size:16px;font-weight:700;line-height:24px">{{projectCode}}</p>
                <p style="margin:4px 0 0;font-size:14px;line-height:22px">{{projectName}}</p>
                <p style="margin:16px 0 0;font-size:13px;line-height:20px;color:#64748b">Lý do cập nhật</p>
                <p style="margin:4px 0 0;font-size:14px;line-height:22px;white-space:pre-line">{{changeReason}}</p>
              </div>
              <div style="margin:18px 0 0;padding:16px;background:#fff1f2;border:1px solid #fecdd3">
                <div style="font-size:12px;font-weight:700;color:#9f1239">THAY ĐỔI SO VỚI BẢN ĐÃ PHÁT HÀNH TRƯỚC</div>
                <p style="margin:8px 0 0;font-size:14px;line-height:22px;white-space:pre-line">{{changeSummary}}</p>
              </div>
              <p style="margin:18px 0 0;font-size:14px;line-height:22px">Vui lòng đăng nhập NICON, mở tiến độ tổng thể của dự án và thực hiện bước được giao.</p>
              <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#64748b">Email này chỉ thông báo; việc phê duyệt và cập nhật tiến độ được thực hiện trong hệ thống.</p>
              <p style="margin:22px 0 0;font-size:14px;line-height:22px">Trân trọng,<br /><strong>NICON</strong></p>
            </td></tr>
            <tr><td style="padding:12px 24px;background:#f8fafc;color:#64748b;font-size:12px;line-height:18px">NICON · Thông báo tiến độ dự án</td></tr>
          </table>
        </div>
        """;

    private static readonly string[] SupportedTokens =
        ["eventName", "recipientName", "projectCode", "projectName", "version", "changeReason", "changeSummary"];

    // Keep Vietnamese and other Unicode text readable while escaping HTML-significant characters.
    private static readonly HtmlEncoder EmailHtmlEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static ProjectScheduleEmailTemplate Get(SiteSettings? settings) => new(
        settings?.ProjectScheduleEmailSubjectTemplate ?? DefaultSubject,
        settings?.ProjectScheduleEmailBodyTemplate ?? DefaultBody);

    public static ProjectScheduleEmailTemplate Validate(string subject, string body)
    {
        var normalizedSubject = subject.Trim();
        var normalizedBody = body.Trim();
        if (normalizedSubject.Length is < 1 or > 200 || normalizedSubject.Contains('\r') || normalizedSubject.Contains('\n'))
            throw new ArgumentException("Tiêu đề email tiến độ phải có từ 1 đến 200 ký tự trên một dòng.");
        if (normalizedBody.Length is < 1 or > 8000 || !EmailTemplateFormatter.IsSafeQuoteHtml(normalizedBody))
            throw new ArgumentException("Nội dung email tiến độ phải là HTML an toàn, dài từ 1 đến 8.000 ký tự.");
        foreach (var template in new[] { normalizedSubject, normalizedBody })
        {
            var tokens = TokenRegex().Matches(template).Select(match => match.Groups[1].Value);
            if (tokens.Any(token => !SupportedTokens.Contains(token, StringComparer.OrdinalIgnoreCase)) ||
                InvalidBracesRegex().IsMatch(TokenRegex().Replace(template, string.Empty)))
                throw new ArgumentException("Mẫu email tiến độ có biến không hỗ trợ hoặc cú pháp biến sai.");
        }
        if (SupportedTokens.Any(token => !normalizedBody.Contains($"{{{{{token}}}}}", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Nội dung email tiến độ phải có đủ biến người nhận, dự án, phiên bản, lý do và nội dung thay đổi.");
        return new(normalizedSubject, normalizedBody);
    }

    public static ProjectScheduleEmailTemplate Render(ProjectScheduleEmailTemplate template,
        IReadOnlyDictionary<string, string> values)
    {
        var tokens = values.ToDictionary(item => item.Key, item => EmailHtmlEncoder.Encode(item.Value), StringComparer.OrdinalIgnoreCase);
        var rendered = new ProjectScheduleEmailTemplate(
            EmailTemplateFormatter.ReplaceTokens(template.Subject,
                values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)),
            EmailTemplateFormatter.ReplaceTokens(template.Body, tokens));
        if (rendered.Subject.Length > 200 || rendered.Body.Length > 16000 ||
            rendered.Subject.Contains('\r') || rendered.Subject.Contains('\n') ||
            rendered.Subject.Contains("{{", StringComparison.Ordinal) || rendered.Body.Contains("{{", StringComparison.Ordinal))
            throw new ArgumentException("Nội dung email tiến độ sau khi điền biến vượt quá giới hạn hoặc còn biến chưa được điền.");
        return rendered;
    }

    public static ProjectScheduleEmailTemplate Preview(ProjectScheduleEmailTemplate template) => Render(template,
        new Dictionary<string, string>
        {
            ["eventName"] = "Yêu cầu duyệt tiến độ tổng thể",
            ["recipientName"] = "Nguyễn Văn An",
            ["projectCode"] = "PJ-2026-001",
            ["projectName"] = "Trung tâm dịch vụ NICON",
            ["version"] = "2",
            ["changeReason"] = "Khách hàng điều chỉnh thời gian bàn giao.",
            ["changeSummary"] = "1.1 2026-03-01–2026-04-01 → 1.1 2026-03-15–2026-04-15",
        });

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\{\{|\}\}")]
    private static partial Regex InvalidBracesRegex();
}
