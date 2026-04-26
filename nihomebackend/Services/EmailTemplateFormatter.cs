using System.Net;
using System.Text.RegularExpressions;

namespace NihomeBackend.Services;

public static partial class EmailTemplateFormatter
{
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    public static string ReplaceTokens(string template, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return tokens.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    public static string DefaultNewApplicationSubject =>
        "[{{siteName}}] Ứng viên mới: {{candidateName}} – {{positionTitle}}";

    public static string DefaultNewApplicationBody => """
        <div style='margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;'>
          <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='width:100%;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;'>
            <tr>
              <td style='padding:14px 18px;background:linear-gradient(135deg,#0f172a,#7c3aed);color:#fff;'>
                <div style='font-size:18px;font-weight:700;'>{{siteName}}</div>
                <div style='font-size:12px;opacity:.9;margin-top:4px;'>ĐƠN ỨNG TUYỂN MỚI</div>
              </td>
            </tr>
            <tr>
              <td style='padding:16px 18px;'>
                <p style='margin:0 0 12px;font-size:14px;'>Có một đơn ứng tuyển mới vừa được gửi:</p>
                <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                  <tr><td style='padding:6px 0;font-weight:600;width:140px;'>Vị trí:</td><td>{{positionTitle}}</td></tr>
                  <tr><td style='padding:6px 0;font-weight:600;'>Phòng ban:</td><td>{{department}}</td></tr>
                  <tr><td style='padding:6px 0;font-weight:600;'>Họ tên:</td><td>{{candidateName}}</td></tr>
                  <tr><td style='padding:6px 0;font-weight:600;'>Email:</td><td>{{email}}</td></tr>
                  <tr><td style='padding:6px 0;font-weight:600;'>Điện thoại:</td><td>{{phone}}</td></tr>
                  <tr><td style='padding:6px 0;font-weight:600;'>Kinh nghiệm:</td><td>{{experienceYears}} năm</td></tr>
                </table>
                <div style='margin:14px 0 0;padding:12px;background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;'>
                  <p style='margin:0 0 6px;font-weight:600;font-size:13px;'>Giới thiệu:</p>
                  <p style='margin:0;font-size:13px;white-space:pre-wrap;'>{{coverLetter}}</p>
                </div>
                <p style='margin:14px 0 0;font-size:13px;color:#6b7280;'>Ngày ứng tuyển: {{appliedAt}}</p>
              </td>
            </tr>
            <tr>
              <td style='padding:10px 18px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:11px;color:#6b7280;'>
                © {{siteName}}. Email tự động – vui lòng không trả lời.
              </td>
            </tr>
          </table>
        </div>
        """;

    public static (string subject, string body) BuildNewApplicationEmail(
        string? subjectTemplate,
        string? bodyTemplate,
        string siteName,
        string positionTitle,
        string department,
        string candidateName,
        string email,
        string? phone,
        int? experienceYears,
        string? coverLetter,
        DateTime appliedAt)
    {
        var phoneValue = phone ?? "—";
        var expValue = experienceYears?.ToString() ?? "—";
        var coverValue = coverLetter ?? "—";
        var appliedValue = appliedAt.ToString("dd/MM/yyyy HH:mm");

        // Plain-text tokens for subject line
        var plainTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["siteName"] = siteName,
            ["positionTitle"] = positionTitle,
            ["department"] = department,
            ["candidateName"] = candidateName,
            ["email"] = email,
            ["phone"] = phoneValue,
            ["experienceYears"] = expValue,
            ["coverLetter"] = coverValue,
            ["appliedAt"] = appliedValue,
        };

        // HTML-encoded tokens for body (prevent XSS)
        var htmlTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["siteName"] = WebUtility.HtmlEncode(siteName),
            ["positionTitle"] = WebUtility.HtmlEncode(positionTitle),
            ["department"] = WebUtility.HtmlEncode(department),
            ["candidateName"] = WebUtility.HtmlEncode(candidateName),
            ["email"] = WebUtility.HtmlEncode(email),
            ["phone"] = WebUtility.HtmlEncode(phoneValue),
            ["experienceYears"] = expValue,
            ["coverLetter"] = WebUtility.HtmlEncode(coverValue),
            ["appliedAt"] = appliedValue,
        };

        var subject = ReplaceTokens(
            string.IsNullOrWhiteSpace(subjectTemplate) ? DefaultNewApplicationSubject : subjectTemplate,
            plainTokens);

        var body = ReplaceTokens(
            string.IsNullOrWhiteSpace(bodyTemplate) ? DefaultNewApplicationBody : bodyTemplate,
            htmlTokens);

        return (subject, body);
    }

    // ─── OTP Email ──────────────────────────────────────────────

    public static string DefaultOtpSubject =>
        "[{{siteName}}] Mã xác thực của bạn";

    public static string DefaultOtpBody => """
        <div style='margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;'>
          <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='width:100%;max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;'>
            <tr>
              <td style='padding:14px 18px;background:linear-gradient(135deg,#0f172a,#7c3aed);color:#fff;'>
                <div style='font-size:18px;font-weight:700;'>{{siteName}}</div>
                <div style='font-size:12px;opacity:.9;margin-top:4px;'>XÁC THỰC TÀI KHOẢN</div>
              </td>
            </tr>
            <tr>
              <td style='padding:24px 18px;text-align:center;'>
                <p style='margin:0 0 16px;font-size:14px;'>Mã xác thực (OTP) của bạn là:</p>
                <div style='display:inline-block;padding:12px 32px;background:#f1f5f9;border:2px solid #7c3aed;border-radius:8px;font-size:32px;font-weight:700;letter-spacing:6px;color:#7c3aed;'>
                  {{otpCode}}
                </div>
                <p style='margin:16px 0 0;font-size:13px;color:#6b7280;'>Mã có hiệu lực trong <strong>{{otpExpireMinutes}} phút</strong>.</p>
                <p style='margin:8px 0 0;font-size:13px;color:#6b7280;'>Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email.</p>
              </td>
            </tr>
            <tr>
              <td style='padding:10px 18px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:11px;color:#6b7280;text-align:center;'>
                © {{siteName}}. Email tự động – vui lòng không trả lời.
              </td>
            </tr>
          </table>
        </div>
        """;

    public static (string subject, string body) BuildOtpEmail(
        string? subjectTemplate,
        string? bodyTemplate,
        string siteName,
        string otpCode,
        string otpExpireMinutes = "5")
    {
        // OTP tokens are all system-generated, but encode for body safety
        var plainTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["siteName"] = siteName,
            ["otpCode"] = otpCode,
            ["otpExpireMinutes"] = otpExpireMinutes,
        };

        var htmlTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["siteName"] = WebUtility.HtmlEncode(siteName),
            ["otpCode"] = WebUtility.HtmlEncode(otpCode),
            ["otpExpireMinutes"] = otpExpireMinutes,
        };

        var subject = ReplaceTokens(
            string.IsNullOrWhiteSpace(subjectTemplate) ? DefaultOtpSubject : subjectTemplate,
            plainTokens);

        var body = ReplaceTokens(
            string.IsNullOrWhiteSpace(bodyTemplate) ? DefaultOtpBody : bodyTemplate,
            htmlTokens);

        return (subject, body);
    }
}
