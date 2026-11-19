using NihomeBackend.Infrastructure;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using MimeKit;

namespace NihomeBackend.Services;

public sealed class WorkPackageApprovalEmailService(AppDbContext db, IDataProtectionProvider protection,
    IPermissionService permissions, IEmailService email, ILogger<WorkPackageApprovalEmailService> logger,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IDataProtector protector = protection.CreateProtector("WorkPackageApproval.Preview.v1");
    private sealed record Preview(int UserId, string Hash, DateTime ExpiresAt,
        string? ToEmail = null, string[]? CcEmails = null, int? ApproverId = null);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<(WorkPackageApprovalPreview Email, int RecipientId, string Hash, string LeadEmail)> RenderAsync(
        DepartmentWorkPackage package, string recordType, int recordId, string reference,
        int authorId, string rowVersion, int callerId, CancellationToken ct,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        // The sender names the approver (NICON); the department lead when none is chosen.
        var approverId = recipients?.ApproverUserId ?? package.LeadUserId;
        var recipient = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == approverId, ct);
        if (recipient is not null && recipient.Id == authorId)
            throw new DepartmentWorkPackageOperationException("Người duyệt phải khác người lập hồ sơ. Hãy chọn một người duyệt khác.");
        if (recipient is null || !recipient.IsActive || string.IsNullOrWhiteSpace(recipient.Email) ||
            recipient.Email.Trim().Length > 254 || !ContactValidation.IsValidEmail(recipient.Email) ||
            !await permissions.HasAsync(recipient.Id, "work.packages.verify", ct))
            throw new DepartmentWorkPackageOperationException("Người duyệt phải đang hoạt động, có quyền duyệt nhóm công việc và email hợp lệ (ví dụ: ten@nicon.vn). Hãy chọn người duyệt khác.");
        var sender = await db.Users.AsNoTracking().SingleAsync(item => item.Id == callerId, ct);
        var toEmail = NormalizeEmail(recipients?.ToEmail ?? recipient.Email, "To");
        var ccEmails = NormalizeCc(recipients?.CcEmails ?? [], toEmail);
        var template = WorkPackageApprovalEmailTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var rendered = WorkPackageApprovalEmailTemplates.Render(template, new(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = recipient.FullName ?? recipient.Email,
            ["workPackageCode"] = package.Code,
            ["workPackageName"] = package.Name,
            ["recordType"] = recordType switch
            {
                "InputPriceOffer" => "Báo giá đầu vào",
                "ProjectBoqRevision" => "BOQ báo giá đầu vào",
                _ => "Hồ sơ rà soát tài chính",
            },
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

    /// <summary>
    /// An absolute link for a notification email, or the relative path when no
    /// public origin is configured: an assignment must not fail over a link.
    /// </summary>
    public string PublicLinkOrPath(string path)
    {
        try { return GetPublicOrigin() + path; }
        catch (DepartmentWorkPackageOperationException) { return path; }
    }

    // Links use the address the user is working on (no configuration on production).
    private string GetPublicOrigin() =>
        PublicOrigin.FromRequest(httpContextAccessor.HttpContext?.Request)
        ?? throw new DepartmentWorkPackageOperationException(
            "Không xác định được địa chỉ trang NICON để gửi link nhóm công việc. Hãy thao tác lại từ trình duyệt.");

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
            new Preview(callerId, rendered.Hash, DateTime.UtcNow.AddMinutes(15), rendered.Email.ToEmail,
                rendered.Email.CcEmails?.ToArray(), rendered.RecipientId))
            )
        };
    }

    /// <summary>
    /// Active NICON users with an email who may approve work package records,
    /// any department: NICON chooses the approver per submission.
    /// </summary>
    public async Task<IReadOnlyList<WorkPackageApproverOption>> ListApproverCandidatesAsync(
        DepartmentWorkPackage package, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Where(user => user.IsActive && user.Email != null && user.Email != "")
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                RoleLabelKey = user.RoleEntity == null ? null : user.RoleEntity.LabelKey,
                RoleName = user.RoleEntity == null ? null : user.RoleEntity.Name,
                DepartmentLabelKey = user.RoleEntity == null || user.RoleEntity.RoleGroup == null
                    ? null : user.RoleEntity.RoleGroup.LabelKey,
            }).ToListAsync(ct);
        var approvers = await permissions.FilterByRolePermissionAsync(
            users.Select(user => user.Id).ToList(), "work.packages.verify", ct);
        return users
            .Where(user => approvers.Contains(user.Id) && ContactValidation.IsValidEmail(user.Email!))
            .Select(user => new WorkPackageApproverOption(user.Id, user.FullName ?? user.Email!, user.Email!.Trim(),
                user.RoleLabelKey, user.RoleName, user.DepartmentLabelKey, user.Id == package.LeadUserId))
            .OrderByDescending(user => user.IsDepartmentLead).ThenBy(user => user.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <returns>When it was queued, and the approver it was sent to.</returns>
    public async Task<(DateTime SubmittedAt, int ApproverUserId)> QueueAsync(DepartmentWorkPackage package, string type, int id, string reference,
        int authorId, string rowVersion, int callerId, string? token, CancellationToken ct)
    {
        Preview? preview;
        try { preview = JsonSerializer.Deserialize<Preview>(protector.Unprotect(token ?? "")); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new DepartmentWorkPackageOperationException("Vui lòng xem trước email trước khi gửi duyệt."); }
        var rendered = await RenderAsync(package, type, id, reference, authorId, rowVersion, callerId, ct,
            new() { ToEmail = preview?.ToEmail, CcEmails = preview?.CcEmails?.ToList() ?? [], ApproverUserId = preview?.ApproverId });
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
        return (createdAt, rendered.RecipientId);
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
                var submitted = message.RecordType switch
                {
                    "InputPriceOffer" => await db.ProcurementInputPriceOffers.AnyAsync(item => item.Id == message.RecordId && item.WorkPackageId == message.WorkPackageId && item.Status == InputPriceOfferStatus.Submitted && item.SubmittedAt == message.CreatedAt, ct),
                    "ProjectBoqRevision" => await db.ProjectBoqRevisions.AnyAsync(item => item.Id == message.RecordId && item.SourceWorkPackageId == message.WorkPackageId && item.Status == ProjectBoqRevisionStatus.Submitted && item.SubmittedAt == message.CreatedAt, ct),
                    _ => await db.DepartmentFinanceReviews.AnyAsync(item => item.Id == message.RecordId && item.WorkPackageId == message.WorkPackageId && item.Status == FinanceReviewStatus.Submitted && item.SubmittedAt == message.CreatedAt, ct),
                };
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
