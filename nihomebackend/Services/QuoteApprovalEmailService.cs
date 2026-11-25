using NihomeBackend.Infrastructure;
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
    Task QueueAsync(int quoteId, IReadOnlyList<int> chosenApprovers, string? rowVersion, string? previewToken,
        int callerId, bool canSeeAll, CancellationToken ct);

    /// <summary>
    /// Queues the approval request for the next step of a quote's chain, saved together
    /// with the step change.
    /// </summary>
    Task QueueNextStepAsync(int quoteId, int approverId, CancellationToken ct);

    /// <summary>
    /// Queues the outcome email for the person who submitted the quote, with the
    /// same project-team CC list as the request. It must be called before the
    /// workflow transition because rejecting clears the submitter fields.
    /// </summary>
    Task QueueDecisionAsync(int quoteId, int callerId, bool approved, string? note, CancellationToken ct);

    /// <summary>
    /// Of the given approvers (each with the role their step needs), those who can decide that
    /// step now: active, in that active role, with permission to view and approve quotes. The
    /// one rule for deciding, handing over, flagging and withdrawing, in a fixed number of queries.
    /// </summary>
    Task<IReadOnlySet<(int UserId, string RoleCode)>> CanDecideAsync(
        IReadOnlyCollection<(int UserId, string RoleCode)> approvers, CancellationToken ct);
}

public sealed class QuoteApprovalEmailService(AppDbContext db, IDataProtectionProvider protection,
    IPermissionService permissions, IEmailService email,
    IHttpContextAccessor httpContextAccessor, ILogger<QuoteApprovalEmailService> logger) : IQuoteApprovalEmailService
{
    private readonly IDataProtector protector = protection.CreateProtector("QuoteApproval.Preview.v1");
    private sealed record Preview(int CallerId, int ApproverId, string Hash, DateTime ExpiresAt);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// The quote workflow as it applies to the caller's next submission: each step with the
    /// people who may approve it (active, in the step's role, not the caller, with an email
    /// and permission to view and approve quotes).
    /// </summary>
    public async Task<IReadOnlyList<QuoteApprovalChainStepResponse>> ChainAsync(int callerId, CancellationToken ct)
    {
        var chain = await QuoteApprovalChain.WithCandidatesAsync(db, permissions,
            await QuoteApprovalChain.ConfiguredAsync(db, ct), [callerId], ApprovePermissions, ct);
        // The approval request goes by email, so each approver needs a usable address.
        return chain.Select(step => step with
        {
            Candidates = step.Candidates.Where(user => ContactValidation.IsValidEmail(user.Email)).ToList()
        }).ToList();
    }

    /// <summary>The people who may approve the first step (kept for single-step clients).</summary>
    public async Task<IReadOnlyList<QuoteApprovalCandidateResponse>> CandidatesAsync(int callerId, CancellationToken ct) =>
        (await ChainAsync(callerId, ct))[0].Candidates;

    /// <summary>
    /// Fixes the chain for a submission: the chosen person for each step that needs a choice,
    /// and every eligible person, one after another, for a step everyone in the role approves.
    /// </summary>
    private async Task<List<QuoteApprovalStep>> BuildChainAsync(int callerId, IReadOnlyList<int> chosenApprovers,
        CancellationToken ct)
    {
        var chain = await ChainAsync(callerId, ct);
        var picks = new Queue<int>(chosenApprovers);
        var result = new List<QuoteApprovalStep>();
        for (var index = 0; index < chain.Count; index++)
        {
            var step = chain[index];
            if (step.Candidates.Count == 0)
                throw await StepErrorAsync(step, index, "QUOTE_APPROVAL_STEP_NO_APPROVER", label =>
                    $"Bước {index + 1} ({label}) chưa có ai đủ điều kiện duyệt báo giá: cần tài khoản đang hoạt động, khác người gửi, có email hợp lệ và quyền xem, duyệt báo giá. Nhờ quản trị kiểm tra Phân quyền hoặc Cấu hình → Luồng duyệt.", ct);
            IReadOnlyList<QuoteApprovalCandidateResponse> people;
            if (step.EveryoneApproves)
            {
                people = step.Candidates;
            }
            else
            {
                if (!picks.TryDequeue(out var chosenId))
                    throw await StepErrorAsync(step, index, "QUOTE_APPROVAL_STEP_UNCHOSEN",
                        label => $"Chọn người duyệt cho bước {index + 1} ({label}).", ct);
                var person = step.Candidates.FirstOrDefault(candidate => candidate.Id == chosenId);
                if (person is null)
                    throw await StepErrorAsync(step, index, "QUOTE_APPROVAL_STEP_INVALID_APPROVER", label =>
                        $"Bước {index + 1}: chọn một {label} khác người gửi, đang hoạt động, có quyền xem và duyệt báo giá cùng email hợp lệ (ví dụ: ten@nicon.vn).", ct);
                people = [person];
            }
            result.AddRange(people.Select(person => new QuoteApprovalStep(step.Order, step.Name, step.RoleCode,
                step.RoleLabelKey, step.SlaHours, person.Id, person.Name, null, null)));
        }
        if (picks.Count > 0)
            throw new QuoteOperationException("Luồng duyệt vừa thay đổi. Tải lại trang rồi chọn lại người duyệt.",
                "QUOTE_APPROVAL_CHAIN_CHANGED", new Dictionary<string, object>());
        var repeated = result.GroupBy(step => step.ApproverUserId).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
            throw new QuoteOperationException(
                $"Mỗi người chỉ duyệt một bước: {repeated.First().ApproverName} đang ở nhiều bước. Chọn người khác cho các bước còn lại.",
                "QUOTE_APPROVAL_PERSON_TWICE", new Dictionary<string, object> { ["name"] = repeated.First().ApproverName });
        return result;
    }

    // The role name is read only for the error, never on the happy path.
    private async Task<QuoteOperationException> StepErrorAsync(QuoteApprovalChainStepResponse step, int index,
        string code, Func<string, string> message, CancellationToken ct) =>
        new(message(await SourceLabels.VietnameseAsync(db, step.RoleLabelKey, step.RoleName, ct)), code,
            new Dictionary<string, object>
            {
                ["step"] = index + 1,
                ["roleLabelKey"] = step.RoleLabelKey ?? "",
                ["roleName"] = step.RoleName,
            });

    public Task<IReadOnlySet<(int UserId, string RoleCode)>> CanDecideAsync(
        IReadOnlyCollection<(int UserId, string RoleCode)> approvers, CancellationToken ct) =>
        QuoteApprovalChain.CanDecideAsync(db, permissions, approvers, ApprovePermissions, ct);

    private static readonly string[] ApprovePermissions = ["crm.quotes.view", "crm.quotes.approve"];

    private async Task<bool> CanDecideAsync(int userId, string roleCode, CancellationToken ct) =>
        (await CanDecideAsync([(userId, roleCode)], ct)).Count == 1;

    private async Task<(Quote Quote, ApplicationUser Approver, List<QuoteApprovalStep> Chain, string[] CcEmails, QuoteApprovalTemplate Message, string Hash)> RenderAsync(
        int quoteId, IReadOnlyList<int> chosenApprovers, string? rowVersion, int callerId, bool canSeeAll, CancellationToken ct)
    {
        var quote = await db.Quotes.Include(q => q.Opportunity).SingleOrDefaultAsync(q => q.Id == quoteId, ct);
        if (quote is null || (!canSeeAll && quote.OwnerUserId != callerId))
            throw new QuoteOperationException("Không tìm thấy báo giá được phép gửi duyệt.");
        if (quote.Status != QuoteStatus.Draft)
            throw new QuoteOperationException("Chỉ báo giá ở trạng thái Nháp mới được gửi duyệt.");
        if (string.IsNullOrWhiteSpace(rowVersion) || rowVersion != CrmConcurrency.Encode(quote.RowVersion))
            throw new QuoteOperationException("Báo giá đã thay đổi. Vui lòng tải lại và xem trước email lần nữa.");
        var chain = await BuildChainAsync(callerId, chosenApprovers, ct);
        var approver = await db.Users.AsNoTracking().SingleAsync(u => u.Id == chain[0].ApproverUserId, ct);
        var ccEmails = await GetCcEmailsAsync(quote, approver.Id, ct);
        if (JsonSerializer.Serialize(ccEmails).Length > 20000)
            throw new QuoteOperationException("Danh sách CC của đội ngũ quá dài. Hãy cập nhật đội ngũ dự án trước khi gửi duyệt.");
        var sender = await db.Users.AsNoTracking().SingleAsync(u => u.Id == callerId, ct);
        var message = await RenderRequestAsync(quote, approver, sender, ct);
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
            Chain = chain.Select(step => new { step.Order, step.RoleCode, step.ApproverUserId }),
            ccEmails,
            callerId,
            message.Subject,
            message.Body
        }));
        return (quote, approver, chain, ccEmails, message, hash);
    }

    private async Task<QuoteApprovalTemplate> RenderRequestAsync(Quote quote, ApplicationUser approver,
        ApplicationUser sender, CancellationToken ct)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = approver.FullName ?? approver.Email,
            ["senderName"] = sender.FullName ?? sender.Email,
            ["quoteCode"] = quote.Code,
            ["quoteVersion"] = quote.Version.ToString(CultureInfo.InvariantCulture),
            ["grandTotal"] = quote.GrandTotal.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")),
            ["currency"] = quote.Currency,
            ["validUntil"] = quote.ValidUntil.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["quoteUrl"] = $"{GetPublicOrigin()}/admin/quotes/{quote.Id}",
        };
        try
        {
            return QuoteApprovalEmailTemplates.Render(QuoteApprovalEmailTemplates.Get(
                await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)), tokens);
        }
        catch (ArgumentException error)
        {
            throw new QuoteOperationException(error.Message);
        }
    }

    public async Task<QuoteApprovalPreviewResponse> PreviewAsync(int quoteId, IReadOnlyList<int> chosenApprovers,
        string? rowVersion, int callerId, bool canSeeAll, CancellationToken ct)
    {
        var rendered = await RenderAsync(quoteId, chosenApprovers, rowVersion, callerId, canSeeAll, ct);
        var token = protector.Protect(JsonSerializer.Serialize(
            new Preview(callerId, rendered.Approver.Id, rendered.Hash, DateTime.UtcNow.AddMinutes(15))));
        return new(token, rendered.Approver.Id, rendered.Approver.FullName ?? rendered.Approver.Email,
            rendered.Approver.Email, rendered.CcEmails, rendered.Message.Subject, rendered.Message.Body);
    }

    public async Task QueueAsync(int quoteId, IReadOnlyList<int> chosenApprovers, string? rowVersion, string? previewToken,
        int callerId, bool canSeeAll, CancellationToken ct)
    {
        Preview? preview;
        try { preview = JsonSerializer.Deserialize<Preview>(protector.Unprotect(previewToken ?? "")); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new QuoteOperationException("Vui lòng chọn người duyệt và xem trước email trước khi gửi duyệt."); }
        var rendered = await RenderAsync(quoteId, chosenApprovers, rowVersion, callerId, canSeeAll, ct);
        if (preview is null || preview.CallerId != callerId || preview.ApproverId != rendered.Approver.Id ||
            preview.ExpiresAt < DateTime.UtcNow || preview.Hash != rendered.Hash)
            throw new QuoteOperationException("Báo giá, người duyệt hoặc mẫu email đã thay đổi. Vui lòng xem trước email lần nữa.");
        var now = DateTime.UtcNow;
        rendered.Chain[0] = rendered.Chain[0] with { StartedAt = now };
        rendered.Quote.ApprovalApproverUserId = rendered.Approver.Id;
        rendered.Quote.ApprovalStepsJson = QuoteApprovalChain.Write(rendered.Chain);
        rendered.Quote.ApprovalStepIndex = 0;
        await QuoteApprovalChain.SetApproversAsync(db, rendered.Quote, rendered.Chain, ct);
        db.QuoteApprovalEmails.Add(new()
        {
            QuoteId = quoteId,
            ApproverUserId = rendered.Approver.Id,
            ToEmail = rendered.Approver.Email.Trim(),
            CcEmailsJson = JsonSerializer.Serialize(rendered.CcEmails),
            Subject = rendered.Message.Subject,
            Body = rendered.Message.Body,
        });
    }

    public async Task QueueNextStepAsync(int quoteId, int approverId, CancellationToken ct)
    {
        var quote = await db.Quotes.SingleAsync(q => q.Id == quoteId, ct);
        var approver = await db.Users.AsNoTracking().SingleAsync(u => u.Id == approverId, ct);
        var sender = await db.Users.AsNoTracking().SingleAsync(u => u.Id == quote.SubmittedByUserId, ct);
        var ccEmails = await GetCcEmailsAsync(quote, approverId, ct);
        var message = await RenderRequestAsync(quote, approver, sender, ct);
        db.QuoteApprovalEmails.Add(new()
        {
            QuoteId = quoteId,
            ApproverUserId = approverId,
            ToEmail = approver.Email.Trim(),
            CcEmailsJson = JsonSerializer.Serialize(ccEmails),
            Subject = message.Subject,
            Body = message.Body,
        });
    }

    public async Task QueueDecisionAsync(int quoteId, int callerId, bool approved, string? note,
        CancellationToken ct)
    {
        var quote = await db.Quotes.SingleOrDefaultAsync(q => q.Id == quoteId, ct)
            ?? throw new QuoteOperationException("Không tìm thấy báo giá.");
        if (quote.Status != QuoteStatus.PendingApproval ||
            quote.ApprovalApproverUserId != callerId ||
            quote.SubmittedByUserId is not int authorId || authorId == callerId)
        {
            throw new QuoteOperationException(
                "Chỉ người được chọn duyệt, khác người gửi, mới ghi nhận kết quả báo giá.");
        }
        var author = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == authorId, ct);
        if (author is null || !author.IsActive || !ContactValidation.IsValidEmail(author.Email))
        {
            throw new QuoteOperationException(
                "Người lập báo giá không còn email hợp lệ để nhận kết quả duyệt. Hãy cập nhật tài khoản trước khi duyệt.");
        }
        var approver = await db.Users.AsNoTracking().SingleAsync(u => u.Id == callerId, ct);
        var ccEmails = await GetCcEmailsAsync(quote, callerId, ct);
        var toEmail = author.Email.Trim();
        var origin = GetPublicOrigin();
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipientName"] = author.FullName ?? author.Email,
            ["approverName"] = approver.FullName ?? approver.Email,
            ["quoteCode"] = quote.Code,
            ["quoteVersion"] = quote.Version.ToString(CultureInfo.InvariantCulture),
            ["grandTotal"] = quote.GrandTotal.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")),
            ["currency"] = quote.Currency,
            ["validUntil"] = quote.ValidUntil.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["decisionLabel"] = approved ? "Đã phê duyệt" : "Từ chối",
            ["decisionNote"] = string.IsNullOrWhiteSpace(note) ? "—" : note.Trim(),
            ["quoteUrl"] = $"{origin}/admin/quotes/{quote.Id}",
        };
        QuoteApprovalTemplate message;
        try
        {
            message = QuoteApprovalDecisionEmailTemplates.Render(
                QuoteApprovalDecisionEmailTemplates.Get(
                    await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)),
                tokens);
        }
        catch (ArgumentException error)
        {
            throw new QuoteOperationException(error.Message);
        }
        db.QuoteApprovalEmails.Add(new()
        {
            QuoteId = quoteId,
            ApproverUserId = callerId,
            Kind = "Decision",
            RecipientUserId = authorId,
            Decision = approved ? "Approved" : "Rejected",
            ToEmail = toEmail,
            CcEmailsJson = JsonSerializer.Serialize(ccEmails.Where(address =>
                !string.Equals(address, toEmail, StringComparison.OrdinalIgnoreCase))),
            Subject = message.Subject,
            Body = message.Body,
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
                message.Status = await IsDeliverableAsync(message, ct) ? "Sending" : "NeedsReview";
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

    /// <summary>
    /// Re-checks a queued message against the quote as it stands now, so a stale
    /// delivery is flagged for review instead of being sent. Request emails need
    /// the approval still pending for the same approver; decision emails need the
    /// quote to have reached the decision's target state.
    /// </summary>
    private async Task<bool> IsDeliverableAsync(QuoteApprovalEmail message, CancellationToken ct)
    {
        string[] queuedCc;
        try { queuedCc = JsonSerializer.Deserialize<string[]>(message.CcEmailsJson) ?? []; }
        catch (JsonException) { return false; }
        // Decision emails exclude the author from the stored CC list because the
        // author is the recipient; reapply that filter before comparing.
        var currentCc = (await GetCcEmailsAsync(message.Quote, message.ApproverUserId, ct))
            .Where(address => !string.Equals(address, message.ToEmail, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!queuedCc.SequenceEqual(currentCc, StringComparer.OrdinalIgnoreCase)) return false;
        if (!ContactValidation.IsValidEmail(message.ToEmail)) return false;

        if (message.Kind == "Decision")
        {
            var expected = message.Decision switch
            {
                "Approved" => QuoteStatus.Approved,
                "Rejected" => QuoteStatus.Draft,
                _ => (QuoteStatus?)null,
            };
            var submitter = await db.Users.AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == message.RecipientUserId, ct);
            return expected.HasValue && message.Quote.Status == expected.Value &&
                submitter is { IsActive: true } &&
                string.Equals(submitter.Email.Trim(), message.ToEmail, StringComparison.OrdinalIgnoreCase) &&
                await permissions.HasAsync(submitter.Id, "crm.quotes.view", ct) &&
                (submitter.Id == message.Quote.OwnerUserId ||
                 await permissions.HasAsync(submitter.Id, "crm.quotes.view.all", ct));
        }

        var approver = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == message.ApproverUserId, ct);
        return message.Quote.Status == QuoteStatus.PendingApproval &&
            message.Quote.ApprovalApproverUserId == message.ApproverUserId &&
            message.Quote.SubmittedByUserId != message.ApproverUserId &&
            approver is { IsActive: true } && approver.Email.Trim() == message.ToEmail &&
            ContactValidation.IsValidEmail(approver.Email) &&
            await CanDecideAsync(message.ApproverUserId, QuoteApprovalChain.CurrentRoleCode(
                message.Quote.ApprovalStepsJson, message.Quote.ApprovalStepIndex), ct);
    }

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

    // Links use the address the user is working on (no configuration on production).
    private string GetPublicOrigin() =>
        PublicOrigin.FromRequest(httpContextAccessor.HttpContext?.Request)
        ?? throw new QuoteOperationException(
            "Không xác định được địa chỉ trang NICON để gửi liên kết báo giá. Hãy thao tác lại từ trình duyệt.");
}
