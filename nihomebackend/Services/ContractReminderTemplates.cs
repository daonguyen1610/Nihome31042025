using System.Net;
using System.Text.RegularExpressions;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public static class ContractReminderTemplates
{
    public static readonly string[] Tokens = ["customerName", "contractNumber", "endDate"];
    public const string RenewalSubject = "NICON – Nhắc gia hạn hợp đồng {{contractNumber}}";
    public const string EndSubject = "NICON – Thông báo thời hạn hợp đồng {{contractNumber}}";
    public const string LegacyRenewalBody = "<div><p>Kính gửi <strong>{{customerName}}</strong>,</p><p>Hợp đồng <strong>{{contractNumber}}</strong> có ngày kết thúc dự kiến là {{endDate}}. Quý khách vui lòng liên hệ NICON để trao đổi việc gia hạn nếu có nhu cầu.</p><p>Trân trọng,<br />NICON</p></div>";
    public const string LegacyEndBody = "<div><p>Kính gửi <strong>{{customerName}}</strong>,</p><p>NICON thông báo ngày kết thúc dự kiến của hợp đồng <strong>{{contractNumber}}</strong> là {{endDate}}. Quý khách vui lòng liên hệ NICON để phối hợp các công việc liên quan.</p><p>Trân trọng,<br />NICON</p></div>";
    public static string RenewalBody => EmailTemplateBaseline.Wrap(
        "NHẮC GIA HẠN HỢP ĐỒNG",
        """
        <p style="margin:0 0 16px;font-size:15px;line-height:24px">Kính gửi <strong>{{customerName}}</strong>,</p>
        <p style="margin:0 0 18px;font-size:14px;line-height:22px">NICON trân trọng gửi Quý khách thông tin về thời hạn hợp đồng để thuận tiện trao đổi và chuẩn bị việc gia hạn nếu có nhu cầu.</p>
        <div style="padding:16px;background:#f8fafc;border:1px solid #e5e7eb">
          <div style="font-size:12px;font-weight:700;color:#64748b">THÔNG TIN HỢP ĐỒNG</div>
          <p style="margin:12px 0 0;font-size:14px;line-height:22px">Số hợp đồng<br /><strong>{{contractNumber}}</strong></p>
        </div>
        <div style="margin:12px 0 0;padding:16px;background:#fff1f2;color:#9f1239">
          <div style="font-size:13px;line-height:20px">Ngày kết thúc dự kiến</div>
          <div style="margin:6px 0 0;font-size:20px;font-weight:700;line-height:28px">{{endDate}}</div>
        </div>
        <p style="margin:18px 0 0;font-size:14px;line-height:22px">Quý khách vui lòng liên hệ NICON để trao đổi việc gia hạn nếu có nhu cầu.</p>
        <p style="margin:22px 0 0;font-size:14px;line-height:22px">Trân trọng,<br /><strong>NICON</strong></p>
        """,
        "NICON · Đối tác phát triển của Quý khách");
    public static string EndBody => EmailTemplateBaseline.Wrap(
        "THÔNG BÁO THỜI HẠN HỢP ĐỒNG",
        """
        <p style="margin:0 0 16px;font-size:15px;line-height:24px">Kính gửi <strong>{{customerName}}</strong>,</p>
        <p style="margin:0 0 18px;font-size:14px;line-height:22px">NICON trân trọng thông báo ngày kết thúc dự kiến của hợp đồng theo thông tin dưới đây.</p>
        <div style="padding:16px;background:#f8fafc;border:1px solid #e5e7eb">
          <div style="font-size:12px;font-weight:700;color:#64748b">THÔNG TIN HỢP ĐỒNG</div>
          <p style="margin:12px 0 0;font-size:14px;line-height:22px">Số hợp đồng<br /><strong>{{contractNumber}}</strong></p>
        </div>
        <div style="margin:12px 0 0;padding:16px;background:#fff1f2;color:#9f1239">
          <div style="font-size:13px;line-height:20px">Ngày kết thúc dự kiến</div>
          <div style="margin:6px 0 0;font-size:20px;font-weight:700;line-height:28px">{{endDate}}</div>
        </div>
        <p style="margin:18px 0 0;font-size:14px;line-height:22px">Quý khách vui lòng liên hệ NICON để phối hợp các công việc liên quan.</p>
        <p style="margin:22px 0 0;font-size:14px;line-height:22px">Trân trọng,<br /><strong>NICON</strong></p>
        """,
        "NICON · Đối tác phát triển của Quý khách");

    public static ContractReminderTemplatesResponse Get(SiteSettings? settings) => new(
        settings?.ContractRenewalEmailSubjectTemplate ?? RenewalSubject,
        EmailTemplateBaseline.Apply(
            settings?.ContractRenewalEmailBodyTemplate ?? RenewalBody,
            "NHẮC GIA HẠN HỢP ĐỒNG", "NICON · Đối tác phát triển của Quý khách"),
        settings?.ContractEndEmailSubjectTemplate ?? EndSubject,
        EmailTemplateBaseline.Apply(
            settings?.ContractEndEmailBodyTemplate ?? EndBody,
            "THÔNG BÁO THỜI HẠN HỢP ĐỒNG", "NICON · Đối tác phát triển của Quý khách"));

    public static string Validate(string value, bool body)
    {
        var text = value.Trim();
        if (text.Length < 1 || text.Length > (body ? 8000 : 200) ||
            (!body && (text.Contains('\r') || text.Contains('\n'))))
            throw new ArgumentException("Invalid email template length or subject line.");
        var matches = Regex.Matches(text, @"\{\{(\w+)\}\}");
        var remainder = Regex.Replace(text, @"\{\{(\w+)\}\}", "");
        if (matches.Any(match => !Tokens.Contains(match.Groups[1].Value, StringComparer.OrdinalIgnoreCase)) ||
            remainder.Contains("{{") || remainder.Contains("}}"))
            throw new ArgumentException("Unsupported email template variable. Use customerName, contractNumber, endDate.");
        if (body && (!EmailTemplateFormatter.IsSafeQuoteHtml(text) ||
            !text.Contains("{{contractNumber}}", StringComparison.OrdinalIgnoreCase) ||
            !text.Contains("{{endDate}}", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Use safe basic HTML including {{contractNumber}} and {{endDate}}.");
        return text;
    }

    public static (string Subject, string Body) Render(ContractReminderTemplatesResponse templates,
        string kind, Contract contract)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customerName"] = contract.Customer.Name,
            ["contractNumber"] = contract.ContractNumber,
            ["endDate"] = contract.EndDate!.Value.ToString("dd/MM/yyyy"),
        };
        var subject = EmailTemplateFormatter.ReplaceTokens(kind == "Renewal" ? templates.RenewalSubject : templates.EndSubject, tokens);
        var body = EmailTemplateFormatter.ReplaceTokens(kind == "Renewal" ? templates.RenewalBody : templates.EndBody,
            tokens.ToDictionary(pair => pair.Key, pair => WebUtility.HtmlEncode(pair.Value), StringComparer.OrdinalIgnoreCase));
        if (subject.Length > 200 || subject.Contains('\r') || subject.Contains('\n') || body.Length > 16000)
            throw new ArgumentException("Rendered email exceeds supported limits.");
        return (subject, body);
    }
}
