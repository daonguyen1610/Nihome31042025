using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using MimeKit;

namespace NihomeBackend.Services;

public sealed class WorkPackageApprovalEmailService(AppDbContext db, IDataProtectionProvider protection,
    IPermissionService permissions, IEmailService email, ILogger<WorkPackageApprovalEmailService> logger,
    IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
{
    private readonly IDataProtector protector = protection.CreateProtector("WorkPackageApproval.Preview.v1");
    private sealed record Preview(int UserId, string Hash, DateTime ExpiresAt,
        string? ToEmail = null, string[]? CcEmails = null);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<(WorkPackageApprovalPreview Email, int RecipientId, string Hash, string LeadEmail)> RenderAsync(
        DepartmentWorkPackage package, string recordType, int recordId, string reference,
        int authorId, string rowVersion, int callerId, CancellationToken ct,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        var recipient = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == package.LeadUserId, ct);
        if (recipient is null || !recipient.IsActive || recipient.Id == authorId || string.IsNullOrWhiteSpace(recipient.Email) ||
            recipient.Email.Trim().Length > 254 || !ContactValidation.IsValidEmail(recipient.Email) ||
            !await permissions.HasAsync(recipient.Id, "work.packages.verify", ct))
            throw new DepartmentWorkPackageOperationException("Chọn người chủ trì độc lập với người lập hồ sơ, đang hoạt động, có quyền duyệt nhóm công việc và email hợp lệ (ví dụ: ten@nicon.vn) trước khi gửi duyệt.");
        var sender = await db.Users.AsNoTracking().SingleAsync(item => item.Id == callerId, ct);
        var toEmail = NormalizeEmail(recipients?.ToEmail ?? recipient.Email, "To");
        var ccEmails = NormalizeCc(recipients?.CcEmails ?? [], toEmail);
        var template = WorkPackageApprovalEmailTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var rendered = WorkPackageApprovalEmailTemplates.Render(template, new(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = recipient.FullName ?? recipient.Email,
            ["workPackageCode"] = package.Code,
            ["workPackageName"] = package.Name,
            ["recordType"] = recordType == "InputPriceOffer" ? "Báo giá đầu vào" : "Hồ sơ rà soát tài chính",
            ["recordReference"] = reference,
            ["senderName"] = sender.FullName ?? sender.Email,
            ["workPackageUrl"] = $"{GetPublicOrigin()}/admin/work-packages/{package.Id}",
        });
        var hash = Hash(JsonSerializer.Serialize(new
        {
            PackageId = package.Id,
            package.UpdatedAt,
            package.Purpose,
            recordType,
            recordId,
            authorId,
            rowVersion,
            RecipientId = recipient.Id,
            recipient.Email,
            toEmail,
            ccEmails,
            rendered.Subject,
            rendered.Body
        }));
        return (new("", toEmail, rendered.Subject, rendered.Body, ccEmails), recipient.Id, hash, recipient.Email.Trim());
    }

    private string GetPublicOrigin()
    {
        var configured = configuration["Frontend:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return ValidateOrigin(configured);

        var allowed = configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? [];
        var request = httpContextAccessor.HttpContext?.Request;
        var browserOrigin = request?.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(browserOrigin) &&
            allowed.Contains(browserOrigin, StringComparer.OrdinalIgnoreCase))
            return ValidateOrigin(browserOrigin);

        if (request is not null)
        {
            var requestOrigin = $"{request.Scheme}://{request.Host}";
            if (Uri.TryCreate(requestOrigin, UriKind.Absolute, out var uri) &&
                (uri.IsLoopback || allowed.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase)))
                return ValidateOrigin(requestOrigin);
        }

        throw new DepartmentWorkPackageOperationException(
            "Chưa cấu hình URL hệ thống để gửi link nhóm công việc. Hãy đặt Frontend:PublicBaseUrl.");
    }

    private static string ValidateOrigin(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new DepartmentWorkPackageOperationException(
                "Frontend:PublicBaseUrl phải là origin HTTPS công khai hoặc HTTP localhost hợp lệ.");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string NormalizeEmail(string? value, string field)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length == 0 || !ContactValidation.IsValidEmail(normalized) ||
            !MailboxAddress.TryParse(normalized, out var mailbox) || mailbox is null || mailbox.Address != normalized || !string.IsNullOrEmpty(mailbox.Name))
            throw new DepartmentWorkPackageOperationException($"Email {field} phải là một địa chỉ hợp lệ, tối đa 150 ký tự (ví dụ: ten@nicon.vn).");
        return normalized;
    }

    private static string[] NormalizeCc(IReadOnlyCollection<string> addresses, string toEmail)
    {
        if (addresses.Count > 20) throw new DepartmentWorkPackageOperationException("CC chỉ được có tối đa 20 địa chỉ email.");
        return addresses.Select(address => NormalizeEmail(address, "CC"))
            .Where(address => !address.Equals(toEmail, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<WorkPackageApprovalPreview> PreviewAsync(DepartmentWorkPackage package, string type,
        int id, string reference, int authorId, string rowVersion, int callerId, CancellationToken ct,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        var rendered = await RenderAsync(package, type, id, reference, authorId, rowVersion, callerId, ct, recipients);
        return rendered.Email with
        {
            PreviewToken = protector.Protect(JsonSerializer.Serialize(
            new Preview(callerId, rendered.Hash, DateTime.UtcNow.AddMinutes(15), rendered.Email.ToEmail, rendered.Email.CcEmails?.ToArray()))
            )
        };
    }

    public async Task<DateTime> QueueAsync(DepartmentWorkPackage package, string type, int id, string reference,
        int authorId, string rowVersion, int callerId, string? token, CancellationToken ct)
    {
        Preview? preview;
        try { preview = JsonSerializer.Deserialize<Preview>(protector.Unprotect(token ?? "")); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new DepartmentWorkPackageOperationException("Vui lòng xem trước email trước khi gửi duyệt."); }
        var rendered = await RenderAsync(package, type, id, reference, authorId, rowVersion, callerId, ct,
            new() { ToEmail = preview?.ToEmail, CcEmails = preview?.CcEmails?.ToList() ?? [] });
        if (preview is null || preview.UserId != callerId || preview.ExpiresAt < DateTime.UtcNow || preview.Hash != rendered.Hash)
            throw new DepartmentWorkPackageOperationException("Hồ sơ, người nhận hoặc mẫu email đã thay đổi. Vui lòng xem trước email lại trước khi gửi duyệt.");
        var createdAt = DateTime.UtcNow;
        db.WorkPackageApprovalEmails.Add(new()
        {
            WorkPackageId = package.Id,
            RecordType = type,
            RecordId = id,
            AuthorUserId = authorId,
            RecipientUserId = rendered.RecipientId,
            ToEmail = rendered.Email.ToEmail,
            LeadEmail = rendered.LeadEmail,
            CcEmailsJson = JsonSerializer.Serialize(rendered.Email.CcEmails ?? []),
            Subject = rendered.Email.Subject,
            Body = rendered.Email.Body,
            PreviewHash = Hash(token!),
            CreatedAt = createdAt,
        });
        return createdAt;
    }

    public async Task SendPendingAsync(CancellationToken ct)
    {
        var ids = await db.WorkPackageApprovalEmails.AsNoTracking().Where(item => item.Status == "Pending")
            .OrderBy(item => item.Id).Select(item => item.Id).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            WorkPackageApprovalEmail message;
            string[] ccEmails;
            await using (var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null)
            {
                message = await db.WorkPackageApprovalEmails.Include(item => item.WorkPackage).SingleAsync(item => item.Id == id, ct);
                await db.Entry(message).ReloadAsync(ct);
                if (message.Status != "Pending") continue;
                var recipient = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.RecipientUserId, ct);
                var validAddresses = true;
                try
                {
                    var toEmail = NormalizeEmail(message.ToEmail, "To");
                    ccEmails = NormalizeCc(JsonSerializer.Deserialize<string[]>(message.CcEmailsJson) ?? [], toEmail);
                }
                catch (Exception error) when (error is JsonException or DepartmentWorkPackageOperationException)
                { validAddresses = false; ccEmails = []; }
                var submitted = message.RecordType == "InputPriceOffer"
                    ? await db.ProcurementInputPriceOffers.AnyAsync(item => item.Id == message.RecordId && item.WorkPackageId == message.WorkPackageId && item.Status == InputPriceOfferStatus.Submitted && item.SubmittedAt == message.CreatedAt, ct)
                    : await db.DepartmentFinanceReviews.AnyAsync(item => item.Id == message.RecordId && item.WorkPackageId == message.WorkPackageId && item.Status == FinanceReviewStatus.Submitted && item.SubmittedAt == message.CreatedAt, ct);
                message.Status = validAddresses && submitted && recipient is { IsActive: true } &&
                    message.WorkPackage.LeadUserId == recipient.Id && recipient.Id != message.AuthorUserId &&
                    !string.IsNullOrWhiteSpace(recipient.Email) &&
                    recipient.Email.Trim() == message.LeadEmail && ContactValidation.IsValidEmail(recipient.Email) &&
                    message.WorkPackage.Status is not (WorkPackageStatus.Cancelled or WorkPackageStatus.Completed) &&
                    await permissions.HasAsync(recipient.Id, "work.packages.verify", ct) ? "Sending" : "NeedsReview";
                message.AttemptedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
            }
            if (message.Status != "Sending") continue;
            try
            {
                if (ccEmails.Length == 0) await email.SendEmailAsync(message.ToEmail, message.Subject, message.Body);
                else await email.SendEmailWithCcAsync(message.ToEmail, ccEmails, message.Subject, message.Body, ct);
                message.Status = "Sent";
                message.SentAt = DateTime.UtcNow;
            }
            catch (Exception error)
            {
                message.Status = "NeedsReview";
                logger.LogError(error, "Approval email {EmailId} delivery outcome requires review", id);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
