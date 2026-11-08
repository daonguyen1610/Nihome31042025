using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class CmScheduleMailService(AppDbContext db, IDataProtectionProvider protection)
{
    public const string DefaultSubject = "NICON · {{eventName}} · {{projectCode}} · CM v{{version}}";
    public const string DefaultBody = """
        <div style="font-family:Arial,sans-serif;max-width:640px;margin:auto;padding:24px;color:#17233b">
        <div style="font-size:20px;font-weight:700">NICON · KẾ HOẠCH CM · {{eventName}}</div>
        <p>Kính gửi {{recipientName}},</p>
        <p>Kế hoạch CM dự án <strong>{{projectCode}} · {{projectName}}</strong>, phiên bản {{version}} cần được duyệt.</p>
        <p><strong>Lý do:</strong> {{changeReason}}</p>
        <p style="white-space:pre-line">{{changeSummary}}</p>
        <p>Đăng nhập NICON, mở Tiến độ CRM – CM – BCH của dự án để kiểm tra và duyệt. Email chỉ thông báo, không thay thế phê duyệt trên hệ thống.</p>
        <p>Trân trọng,<br />NICON</p>
        </div>
        """;
    private readonly IDataProtector protector = protection.CreateProtector("CmSchedule.EmailPreview.v1");
    private sealed record Claim(int BaselineId, int Actor, string RowVersion, string Hash, DateTime ExpiresAt);

    public static ProjectScheduleEmailTemplate Template(SiteSettings? settings) =>
        settings?.CmScheduleEmailSubjectTemplate is { } subject && settings.CmScheduleEmailBodyTemplate is { } body
            ? ProjectScheduleEmailTemplates.Validate(subject, body) : throw new CmScheduleException("emailTemplateRequired");

    private async Task<ProjectScheduleEmailPreview> RenderAsync(CmScheduleBaseline baseline, CancellationToken ct)
    {
        var recipient = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == baseline.ApproverUserId && x.IsActive, ct);
        if (!ContactValidation.IsValidEmail(recipient?.Email)) throw new CmScheduleException("emailRequired");
        var project = await db.OperationalProjects.AsNoTracking().SingleAsync(x => x.Id == baseline.OperationalProjectId, ct);
        var lines = JsonSerializer.Deserialize<List<CmScheduleLine>>(baseline.ItemsJson)!;
        var summary = string.Join("\n", lines.Select(x => $"{x.Wbs} · {x.Name}: {x.PlannedStart:yyyy-MM-dd} – {x.PlannedEnd:yyyy-MM-dd}; {x.Weight}"));
        var template = Template(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var rendered = ProjectScheduleEmailTemplates.Render(template, new Dictionary<string, string>
        {
            ["eventName"] = "Yêu cầu duyệt kế hoạch CM",
            ["recipientName"] = recipient!.FullName ?? "",
            ["projectCode"] = project.Code,
            ["projectName"] = project.Name,
            ["version"] = baseline.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["changeReason"] = baseline.ChangeReason,
            ["changeSummary"] = summary,
        });
        return new(baseline.ApproverUserId, recipient.Email!.Trim(), rendered.Subject, rendered.Body);
    }
    public async Task<ProjectScheduleEmailPreviewResponse> PreviewAsync(CmScheduleBaseline baseline, int actor, CancellationToken ct)
    {
        var email = await RenderAsync(baseline, ct);
        var claim = new Claim(baseline.Id, actor, CrmConcurrency.Encode(baseline.RowVersion), Hash(email), DateTime.UtcNow.AddMinutes(15));
        return new(protector.Protect(JsonSerializer.Serialize(claim)), [email]);
    }
    public async Task QueueAsync(CmScheduleBaseline baseline, int actor, string previewToken, CancellationToken ct)
    {
        var email = await RenderAsync(baseline, ct);
        Claim? claim;
        try { claim = JsonSerializer.Deserialize<Claim>(protector.Unprotect(previewToken)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw new CmScheduleException("emailPreviewRequired"); }
        if (claim is null || claim.BaselineId != baseline.Id || claim.Actor != actor || claim.RowVersion != CrmConcurrency.Encode(baseline.RowVersion) || claim.Hash != Hash(email) || claim.ExpiresAt < DateTime.UtcNow)
            throw new CmScheduleException("emailPreviewRequired");
        db.ProjectScheduleEmails.Add(new ProjectScheduleEmail
        {
            OperationalProjectId = baseline.OperationalProjectId,
            RevisionId = baseline.CrmRevisionId,
            CmScheduleBaselineId = baseline.Id,
            RecipientUserId = email.RecipientUserId,
            ToEmail = email.ToEmail,
            Subject = email.Subject,
            Body = email.Body
        });
    }
    private static string Hash(ProjectScheduleEmailPreview email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(email))));
}
