using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public interface IQuoteApprovalEmailService
{
    Task QueueAsync(int quoteId, int approverId, string? rowVersion, string? previewToken,
        int callerId, bool canSeeAll, CancellationToken ct);
}

public sealed class QuoteApprovalEmailService(AppDbContext db, IDataProtectionProvider protection,
    IPermissionService permissions, IEmailService email, IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor, ILogger<QuoteApprovalEmailService> logger) : IQuoteApprovalEmailService
{
    private readonly IDataProtector protector = protection.CreateProtector("QuoteApproval.Preview.v1");
    private sealed record Preview(int CallerId, int ApproverId, string Hash, DateTime ExpiresAt);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<IReadOnlyList<QuoteApprovalCandidateResponse>> CandidatesAsync(int callerId, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != callerId)
            .Select(u => new { u.Id, u.FullName, u.Email, u.RoleEntityId }).ToListAsync(ct);
        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive && r.Code == "PROCUREMENT_MANAGER")
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                r.LabelKey,
                DepartmentId = r.RoleGroup != null && r.RoleGroup.IsActive ? r.RoleGroupId : null,
                DepartmentLabelKey = r.RoleGroup != null && r.RoleGroup.IsActive ? r.RoleGroup.LabelKey : null
            }).ToListAsync(ct);
        var roleById = roles.ToDictionary(r => r.Id);
        var candidates = new List<QuoteApprovalCandidateResponse>();
        foreach (var user in users.Where(user => ContactValidation.IsValidEmail(user.Email) &&
            user.RoleEntityId is int roleId && roleById.ContainsKey(roleId)))
        {
            if (!await HasQuoteApproverAccessAsync(user.Id, ct)) continue;
            var role = roleById[user.RoleEntityId!.Value];
            candidates.Add(new QuoteApprovalCandidateResponse(user.Id, user.FullName ?? user.Email,
                user.Email, role.Code, role.Name, role.LabelKey, role.DepartmentId, role.DepartmentLabelKey));
        }
        return candidates.OrderBy(user => user.Name).ToList();
    }

    private async Task<(Quote Quote, ApplicationUser Approver, string[] CcEmails, QuoteApprovalTemplate Message, string Hash)> RenderAsync(
        int quoteId, int approverId, string? rowVersion, int callerId, bool canSeeAll, CancellationToken ct)
    {
        var quote = await db.Quotes.Include(q => q.Opportunity).SingleOrDefaultAsync(q => q.Id == quoteId, ct);
        if (quote is null || (!canSeeAll && quote.OwnerUserId != callerId))
            throw new QuoteOperationException("Không tìm thấy báo giá được phép gửi duyệt.");
        if (quote.Status != QuoteStatus.Draft)
            throw new QuoteOperationException("Chỉ báo giá ở trạng thái Nháp mới được gửi duyệt.");
        if (string.IsNullOrWhiteSpace(rowVersion) || rowVersion != CrmConcurrency.Encode(quote.RowVersion))
            throw new QuoteOperationException("Báo giá đã thay đổi. Vui lòng tải lại và xem trước email lần nữa.");
        var approver = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == approverId, ct);
        if (approver is null || !approver.IsActive || approver.Id == callerId ||
            !ContactValidation.IsValidEmail(approver.Email) ||
            !await IsProcurementManagerAsync(approver.Id, ct) ||
            !await HasQuoteApproverAccessAsync(approverId, ct))
            throw new QuoteOperationException("Chọn Trưởng phòng Cung ứng khác người gửi, đang hoạt động, có quyền xem và duyệt báo giá cùng email hợp lệ (ví dụ: ten@nicon.vn).");
        var ccEmails = await GetCcEmailsAsync(quote, approver.Id, ct);
        if (JsonSerializer.Serialize(ccEmails).Length > 20000)
            throw new QuoteOperationException("Danh sách CC của đội ngũ quá dài. Hãy cập nhật đội ngũ dự án trước khi gửi duyệt.");
        var sender = await db.Users.AsNoTracking().SingleAsync(u => u.Id == callerId, ct);
        var origin = GetPublicOrigin();
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = approver.FullName ?? approver.Email,
            ["senderName"] = sender.FullName ?? sender.Email,
            ["quoteCode"] = quote.Code,
            ["quoteVersion"] = quote.Version.ToString(CultureInfo.InvariantCulture),
            ["grandTotal"] = quote.GrandTotal.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")),
            ["validUntil"] = quote.ValidUntil.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["quoteUrl"] = $"{origin}/admin/quotes/{quote.Id}",
        };
        QuoteApprovalTemplate message;
        try
        {
            message = QuoteApprovalEmailTemplates.Render(QuoteApprovalEmailTemplates.Get(
                await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)), tokens);
        }
        catch (ArgumentException error)
        {
            throw new QuoteOperationException(error.Message);
        }
        var hash = Hash(JsonSerializer.Serialize(new
        {
            QuoteId = quote.Id,
            quote.RowVersion,
            quote.Status,
            quote.Version,
            quote.GrandTotal,
            quote.ValidUntil,
            ApproverId = approver.Id,
            approver.Email,
            ccEmails,
            callerId,
            message.Subject,
            message.Body
        }));
        return (quote, approver, ccEmails, message, hash);
    }

    public async Task<QuoteApprovalPreviewResponse> PreviewAsync(int quoteId, int approverId,
        string? rowVersion, int callerId, bool canSeeAll, CancellationToken ct)
    {
        var rendered = await RenderAsync(quoteId, approverId, rowVersion, callerId, canSeeAll, ct);
        var token = protector.Protect(JsonSerializer.Serialize(
            new Preview(callerId, approverId, rendered.Hash, DateTime.UtcNow.AddMinutes(15))));
        return new(token, approverId, rendered.Approver.FullName ?? rendered.Approver.Email,
            rendered.Approver.Email, rendered.CcEmails, rendered.Message.Subject, rendered.Message.Body);
    }

    public async Task QueueAsync(int quoteId, int approverId, string? rowVersion, string? previewToken,
        int callerId, bool canSeeAll, CancellationToken ct)
    {
        Preview? preview;
        try { preview = JsonSerializer.Deserialize<Preview>(protector.Unprotect(previewToken ?? "")); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new QuoteOperationException("Vui lòng chọn người duyệt và xem trước email trước khi gửi duyệt."); }
        var rendered = await RenderAsync(quoteId, approverId, rowVersion, callerId, canSeeAll, ct);
        if (preview is null || preview.CallerId != callerId || preview.ApproverId != approverId ||
            preview.ExpiresAt < DateTime.UtcNow || preview.Hash != rendered.Hash)
            throw new QuoteOperationException("Báo giá, người duyệt hoặc mẫu email đã thay đổi. Vui lòng xem trước email lần nữa.");
        rendered.Quote.ApprovalApproverUserId = approverId;
        db.QuoteApprovalEmails.Add(new()
        {
            QuoteId = quoteId,
            ApproverUserId = approverId,
            ToEmail = rendered.Approver.Email.Trim(),
            CcEmailsJson = JsonSerializer.Serialize(rendered.CcEmails),
            Subject = rendered.Message.Subject,
            Body = rendered.Message.Body,
        });
    }

    public async Task SendPendingAsync(CancellationToken ct)
    {
        var ids = await db.QuoteApprovalEmails.AsNoTracking().Where(e => e.Status == "Pending")
            .OrderBy(e => e.Id).Select(e => e.Id).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            QuoteApprovalEmail message;
            await using (var tx = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null)
            {
                message = await db.QuoteApprovalEmails.Include(e => e.Quote).SingleAsync(e => e.Id == id, ct);
                await db.Entry(message).ReloadAsync(ct);
                if (message.Status != "Pending") continue;
                var approver = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == message.ApproverUserId, ct);
                var currentCc = await GetCcEmailsAsync(message.Quote, message.ApproverUserId, ct);
                string[] queuedCc;
                var validCc = true;
                try { queuedCc = JsonSerializer.Deserialize<string[]>(message.CcEmailsJson) ?? []; }
                catch (JsonException) { queuedCc = []; validCc = false; }
                message.Status = message.Quote.Status == QuoteStatus.PendingApproval &&
                    message.Quote.ApprovalApproverUserId == message.ApproverUserId &&
                    message.Quote.SubmittedByUserId != message.ApproverUserId &&
                    approver is { IsActive: true } && approver.Email.Trim() == message.ToEmail &&
                    ContactValidation.IsValidEmail(approver.Email) &&
                    await IsProcurementManagerAsync(message.ApproverUserId, ct) &&
                    await HasQuoteApproverAccessAsync(message.ApproverUserId, ct) &&
                    validCc && queuedCc.SequenceEqual(currentCc, StringComparer.OrdinalIgnoreCase)
                    ? "Sending" : "NeedsReview";
                message.AttemptedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
            }
            if (message.Status != "Sending") continue;
            try
            {
                var ccEmails = JsonSerializer.Deserialize<string[]>(message.CcEmailsJson) ?? [];
                await email.SendEmailWithCcAsync(message.ToEmail, ccEmails, message.Subject, message.Body, ct);
                message.Status = "Sent";
                message.SentAt = DateTime.UtcNow;
            }
            catch (Exception error)
            {
                message.Status = "NeedsReview";
                logger.LogError(error, "Quote approval email {EmailId} delivery outcome requires review", id);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private Task<bool> IsProcurementManagerAsync(int userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            user.RoleEntity != null && user.RoleEntity.IsActive &&
            user.RoleEntity.Code == "PROCUREMENT_MANAGER", ct);

    private async Task<bool> HasQuoteApproverAccessAsync(int userId, CancellationToken ct) =>
        await permissions.HasAsync(userId, "crm.quotes.view", ct) &&
        await permissions.HasAsync(userId, "crm.quotes.approve", ct);

    private async Task<string[]> GetCcEmailsAsync(Quote quote, int approverId, CancellationToken ct)
    {
        if (!quote.OperationalProjectId.HasValue) return [];
        var members = await db.OperationalProjectMembers.AsNoTracking()
            .Where(member => member.OperationalProjectId == quote.OperationalProjectId &&
                member.EndedAt == null && member.User.IsActive && member.UserId != approverId)
            .Select(member => new { member.UserId, member.User.Email })
            .Distinct().ToListAsync(ct);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            if (!ContactValidation.IsValidEmail(member.Email) ||
                !await permissions.HasAsync(member.UserId, "crm.quotes.view", ct) ||
                (member.UserId != quote.OwnerUserId &&
                 !await permissions.HasAsync(member.UserId, "crm.quotes.view.all", ct)))
                continue;
            result.Add(member.Email.Trim());
        }
        return result.OrderBy(address => address, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private string GetPublicOrigin()
    {
        var value = configuration["Frontend:PublicBaseUrl"];
        var request = httpContextAccessor.HttpContext?.Request;
        if (string.IsNullOrWhiteSpace(value) && request is not null)
        {
            var origin = request.Headers.Origin.ToString();
            var allowed = configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? [];
            value = !string.IsNullOrWhiteSpace(origin) && allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)
                ? origin : $"{request.Scheme}://{request.Host}";
        }
        if (!Uri.TryCreate(value?.TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new QuoteOperationException("Chưa cấu hình URL hệ thống hợp lệ để gửi liên kết báo giá. Hãy đặt Frontend:PublicBaseUrl.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
