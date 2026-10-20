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
    public const string RenewalBody = "<div><p>Kính gửi <strong>{{customerName}}</strong>,</p><p>Hợp đồng <strong>{{contractNumber}}</strong> có ngày kết thúc dự kiến là {{endDate}}. Quý khách vui lòng liên hệ NICON để trao đổi việc gia hạn nếu có nhu cầu.</p><p>Trân trọng,<br />NICON</p></div>";
    public const string EndBody = "<div><p>Kính gửi <strong>{{customerName}}</strong>,</p><p>NICON thông báo ngày kết thúc dự kiến của hợp đồng <strong>{{contractNumber}}</strong> là {{endDate}}. Quý khách vui lòng liên hệ NICON để phối hợp các công việc liên quan.</p><p>Trân trọng,<br />NICON</p></div>";

    public static ContractReminderTemplatesResponse Get(SiteSettings? settings) => new(
        settings?.ContractRenewalEmailSubjectTemplate ?? RenewalSubject,
        settings?.ContractRenewalEmailBodyTemplate ?? RenewalBody,
        settings?.ContractEndEmailSubjectTemplate ?? EndSubject,
        settings?.ContractEndEmailBodyTemplate ?? EndBody);

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
