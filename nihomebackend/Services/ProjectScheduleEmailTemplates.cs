using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed record ProjectScheduleEmailTemplate(string Subject, string Body);

public static partial class ProjectScheduleEmailTemplates
{
    public const string DefaultSubject = "NICON · {{eventName}} · {{projectCode}} · v{{version}}";
    public const string DefaultBody = "<div style=\"font-family:Arial,sans-serif;max-width:640px;margin:auto;padding:24px;color:#17233b\"><p><strong>NICON · {{eventName}}</strong></p><p>Kính gửi {{recipientName}},</p><p>Tiến độ tổng thể dự án <strong>{{projectCode}} · {{projectName}}</strong>, phiên bản v{{version}}.</p><p><strong>Lý do cập nhật:</strong> {{changeReason}}</p><p><strong>Thay đổi so với bản đã phát hành trước:</strong><br />{{changeSummary}}</p><p>Vui lòng đăng nhập NICON để xem tiến độ và thực hiện bước được giao.</p></div>";

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
            EmailTemplateFormatter.ReplaceTokens(template.Subject, tokens),
            EmailTemplateFormatter.ReplaceTokens(template.Body, tokens));
        if (rendered.Subject.Length > 200 || rendered.Body.Length > 16000 ||
            rendered.Subject.Contains("{{", StringComparison.Ordinal) || rendered.Body.Contains("{{", StringComparison.Ordinal))
            throw new ArgumentException("Nội dung email tiến độ sau khi điền biến vượt quá giới hạn hoặc còn biến chưa được điền.");
        return rendered;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\{\{|\}\}")]
    private static partial Regex InvalidBracesRegex();
}
