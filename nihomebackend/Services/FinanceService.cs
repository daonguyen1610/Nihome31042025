using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class FinanceService(AppDbContext db) : IFinanceService
{
    private const string AccountantRole = "ACCOUNTANT";
    private static readonly HashSet<string> CorrectionSourceTypes =
        [nameof(PaymentRequest), nameof(ContractPaymentMilestone), nameof(Contract)];

    public async Task<IReadOnlyList<PaymentRequestResponse>> ListPaymentRequestsAsync(CancellationToken ct = default) =>
        (await PaymentQuery().OrderByDescending(item => item.CreatedAt).ToListAsync(ct)).Select(MapPayment).ToList();

    public async Task<IReadOnlyList<AccountingPeriodResponse>> ListPeriodsAsync(CancellationToken ct = default) =>
        (await db.AccountingPeriods.AsNoTracking().OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Month).ToListAsync(ct)).Select(MapPeriod).ToList();

    public async Task<IReadOnlyList<AccountingCorrectionResponse>> ListCorrectionsAsync(CancellationToken ct = default) =>
        (await CorrectionQuery().OrderByDescending(item => item.CreatedAt).ToListAsync(ct)).Select(MapCorrection).ToList();

    public async Task<PaymentRequestResponse> CreatePaymentRequestAsync(
        PaymentRequestUpsertRequest request, int userId, CancellationToken ct = default)
    {
        await ValidatePaymentAsync(request, null, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = new PaymentRequest
        {
            Code = await AllocatePaymentCodeAsync(ct),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyPayment(entity, request);
        entity.Events.Add(NewPaymentEvent(null, PaymentRequestStatus.Draft, userId, null));
        db.PaymentRequests.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetPaymentAsync(entity.Id, ct))!;
    }

    public async Task<PaymentRequestResponse?> UpdatePaymentRequestAsync(
        int id, PaymentRequestUpsertRequest request, CancellationToken ct = default)
    {
        var entity = await db.PaymentRequests.Include(item => item.Attachments).SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != PaymentRequestStatus.Draft)
            throw new FinanceOperationException("Chỉ được sửa đề nghị thanh toán ở trạng thái Nháp.");
        await ValidatePaymentAsync(request, id, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        db.PaymentRequestAttachments.RemoveRange(entity.Attachments);
        entity.Attachments.Clear();
        ApplyPayment(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetPaymentAsync(id, ct);
    }

    public Task<PaymentRequestResponse?> SubmitPaymentRequestAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default) =>
        TransitionPaymentAsync(id, request, userId, PaymentRequestStatus.UnderValidation, ct);

    public Task<PaymentRequestResponse?> ValidatePaymentRequestAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default) =>
        TransitionPaymentAsync(id, request, userId, PaymentRequestStatus.ReadyForApproval, ct);

    public async Task<PaymentRequestResponse?> DecidePaymentRequestAsync(
        int id, FinanceDecisionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.PaymentRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        var target = request.Approved ? PaymentRequestStatus.Approved : PaymentRequestStatus.Rejected;
        if (entity.Status == target) return await GetPaymentAsync(id, ct);
        if (request.Approved)
        {
            if (entity.Status != PaymentRequestStatus.ReadyForApproval)
                throw new FinanceOperationException("Chỉ đề nghị đã sẵn sàng mới được phê duyệt.");
            if (entity.AssignedAccountantUserId == userId)
                throw new FinanceOperationException("Kế toán được phân công không được tự phê duyệt đề nghị thanh toán.");
        }
        else if (entity.Status is not (PaymentRequestStatus.UnderValidation or PaymentRequestStatus.ReadyForApproval))
        {
            throw new FinanceOperationException("Chỉ đề nghị đang kiểm tra hoặc sẵn sàng phê duyệt mới được từ chối.");
        }
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw new FinanceOperationException("Lý do từ chối là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        var from = entity.Status;
        entity.Status = target;
        entity.ApprovedAt = request.Approved ? now : null;
        entity.ApprovedByUserId = request.Approved ? userId : null;
        entity.RejectedAt = request.Approved ? null : now;
        entity.RejectedByUserId = request.Approved ? null : userId;
        entity.DecisionReason = TrimOrNull(request.Reason);
        entity.UpdatedAt = now;
        entity.Events.Add(NewPaymentEvent(from, target, userId, request.Reason, now));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetPaymentAsync(id, ct);
    }

    public async Task<PaymentRequestResponse?> PayPaymentRequestAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.PaymentRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == PaymentRequestStatus.Paid) return await GetPaymentAsync(id, ct);
        if (entity.Status != PaymentRequestStatus.Approved)
            throw new FinanceOperationException("Chỉ đề nghị đã phê duyệt mới được xác nhận thanh toán.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var from = entity.Status;
        entity.Status = PaymentRequestStatus.Paid;
        entity.PaidAt = DateTime.UtcNow;
        entity.PaidByUserId = userId;
        entity.UpdatedAt = entity.PaidAt.Value;
        entity.Events.Add(NewPaymentEvent(from, PaymentRequestStatus.Paid, userId, request.Reason, entity.PaidAt));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetPaymentAsync(id, ct);
    }

    public async Task<PaymentRequestResponse?> CancelPaymentRequestAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.PaymentRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == PaymentRequestStatus.Cancelled) return await GetPaymentAsync(id, ct);
        if (entity.Status != PaymentRequestStatus.Approved)
            throw new FinanceOperationException("Chỉ đề nghị đã phê duyệt và chưa thanh toán mới được hủy.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new FinanceOperationException("Lý do hủy là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var from = entity.Status;
        entity.Status = PaymentRequestStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.CancelledByUserId = userId;
        entity.DecisionReason = request.Reason.Trim();
        entity.UpdatedAt = entity.CancelledAt.Value;
        entity.Events.Add(NewPaymentEvent(from, PaymentRequestStatus.Cancelled, userId, request.Reason, entity.CancelledAt));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetPaymentAsync(id, ct);
    }

    public async Task<AccountingPeriodResponse> CreatePeriodAsync(AccountingPeriodCreateRequest request, CancellationToken ct = default)
    {
        if (await db.AccountingPeriods.AnyAsync(item => item.Year == request.Year && item.Month == request.Month, ct))
            throw new FinanceOperationException("Kỳ kế toán tháng này đã tồn tại.");
        var (start, end) = VietnamMonthBoundaries(request.Year, request.Month);
        var entity = new AccountingPeriod
        {
            Year = request.Year,
            Month = request.Month,
            PeriodStartUtc = start,
            PeriodEndUtc = end,
        };
        db.AccountingPeriods.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapPeriod(entity);
    }

    public async Task<AccountingPeriodResponse?> StartClosingPeriodAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.AccountingPeriods.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == AccountingPeriodStatus.Closing) return MapPeriod(entity);
        if (entity.Status != AccountingPeriodStatus.Open)
            throw new FinanceOperationException("Chỉ kỳ kế toán đang mở mới có thể bắt đầu đóng.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = AccountingPeriodStatus.Closing;
        entity.ClosingAt = DateTime.UtcNow;
        entity.ClosingByUserId = userId;
        entity.UpdatedAt = entity.ClosingAt.Value;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapPeriod(entity);
    }

    public async Task<AccountingPeriodResponse?> ClosePeriodAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.AccountingPeriods.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == AccountingPeriodStatus.Closed) return MapPeriod(entity);
        if (entity.Status != AccountingPeriodStatus.Closing)
            throw new FinanceOperationException("Kỳ kế toán phải ở trạng thái Đang đóng trước khi đóng sổ.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new FinanceOperationException("Lý do đóng kỳ kế toán là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = AccountingPeriodStatus.Closed;
        entity.ClosedAt = DateTime.UtcNow;
        entity.ClosedByUserId = userId;
        entity.CloseReason = request.Reason.Trim();
        entity.UpdatedAt = entity.ClosedAt.Value;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapPeriod(entity);
    }

    public async Task<AccountingCorrectionResponse> CreateCorrectionAsync(
        AccountingCorrectionUpsertRequest request, int userId, CancellationToken ct = default)
    {
        await ValidateCorrectionAsync(request, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = new AccountingCorrection
        {
            Code = await AllocateCorrectionCodeAsync(ct),
            RecordedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyCorrection(entity, request);
        db.AccountingCorrections.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetCorrectionAsync(entity.Id, ct))!;
    }

    public async Task<AccountingCorrectionResponse?> UpdateCorrectionAsync(
        int id, AccountingCorrectionUpsertRequest request, CancellationToken ct = default)
    {
        var entity = await db.AccountingCorrections.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != AccountingCorrectionStatus.Draft || entity.ReversalOfCorrectionId.HasValue)
            throw new FinanceOperationException("Chỉ được sửa bút toán điều chỉnh ở trạng thái Nháp.");
        await ValidateCorrectionAsync(request, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        ApplyCorrection(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetCorrectionAsync(id, ct);
    }

    public async Task<AccountingCorrectionResponse?> SubmitCorrectionAsync(
        int id, FinanceTransitionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.AccountingCorrections.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == AccountingCorrectionStatus.Submitted) return await GetCorrectionAsync(id, ct);
        if (entity.Status != AccountingCorrectionStatus.Draft || entity.ReversalOfCorrectionId.HasValue)
            throw new FinanceOperationException("Chỉ bút toán Nháp mới được gửi phê duyệt.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = AccountingCorrectionStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.SubmittedByUserId = userId;
        entity.UpdatedAt = entity.SubmittedAt.Value;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetCorrectionAsync(id, ct);
    }

    public async Task<AccountingCorrectionResponse?> DecideCorrectionAsync(
        int id, FinanceDecisionRequest request, int userId, CancellationToken ct = default)
    {
        var entity = await db.AccountingCorrections.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        var target = request.Approved ? AccountingCorrectionStatus.Approved : AccountingCorrectionStatus.Rejected;
        if (entity.Status == target) return await GetCorrectionAsync(id, ct);
        if (entity.Status != AccountingCorrectionStatus.Submitted)
            throw new FinanceOperationException("Chỉ bút toán đã gửi mới được phê duyệt hoặc từ chối.");
        if (entity.ResponsibleAccountantUserId == userId)
            throw new FinanceOperationException("Kế toán chịu trách nhiệm không được tự phê duyệt điều chỉnh.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw new FinanceOperationException("Lý do từ chối là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        entity.Status = target;
        entity.ApprovedAt = request.Approved ? now : null;
        entity.ApprovedByUserId = request.Approved ? userId : null;
        entity.RejectedAt = request.Approved ? null : now;
        entity.RejectedByUserId = request.Approved ? null : userId;
        entity.DecisionReason = TrimOrNull(request.Reason);
        entity.UpdatedAt = now;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetCorrectionAsync(id, ct);
    }

    public async Task<AccountingCorrectionResponse?> ReverseCorrectionAsync(
        int id, AccountingCorrectionReversalRequest request, int userId, CancellationToken ct = default)
    {
        await using var transaction = await BeginSerializableAsync(ct);
        var original = await db.AccountingCorrections.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (original is null) return null;
        if (original.Status == AccountingCorrectionStatus.Reversed)
        {
            var existingReversal = await CorrectionQuery().SingleAsync(item => item.ReversalOfCorrectionId == id, ct);
            return MapCorrection(existingReversal);
        }
        if (original.Status != AccountingCorrectionStatus.Approved || original.ReversalOfCorrectionId.HasValue)
            throw new FinanceOperationException("Chỉ bút toán gốc đã phê duyệt mới được đảo.");
        CrmConcurrency.Apply(db, original, request.RowVersion);
        var now = DateTime.UtcNow;
        var reversal = new AccountingCorrection
        {
            Code = await AllocateCorrectionCodeAsync(ct),
            OperationalProjectId = original.OperationalProjectId,
            AccountingPeriodId = original.AccountingPeriodId,
            SourceEntityType = original.SourceEntityType,
            SourceEntityId = original.SourceEntityId,
            ReasonCode = "REVERSAL",
            OriginalValue = original.CorrectedValue,
            CorrectedValue = original.OriginalValue,
            Currency = original.Currency,
            Note = request.Reason.Trim(),
            ResponsibleAccountantUserId = original.ResponsibleAccountantUserId,
            Status = AccountingCorrectionStatus.Approved,
            RecordedByUserId = userId,
            SubmittedAt = now,
            SubmittedByUserId = userId,
            ApprovedAt = now,
            ApprovedByUserId = userId,
            ReversalOfCorrectionId = original.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        original.Status = AccountingCorrectionStatus.Reversed;
        original.ReversedAt = now;
        original.ReversedByUserId = userId;
        original.UpdatedAt = now;
        db.AccountingCorrections.Add(reversal);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetCorrectionAsync(reversal.Id, ct);
    }

    private async Task<PaymentRequestResponse?> TransitionPaymentAsync(
        int id, FinanceTransitionRequest request, int userId, PaymentRequestStatus target, CancellationToken ct)
    {
        var entity = await db.PaymentRequests.Include(item => item.Attachments).SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == target) return await GetPaymentAsync(id, ct);
        if (target == PaymentRequestStatus.UnderValidation && entity.Status != PaymentRequestStatus.Draft)
            throw new FinanceOperationException("Chỉ đề nghị Nháp mới được gửi kiểm tra.");
        if (target == PaymentRequestStatus.ReadyForApproval)
        {
            if (entity.Status != PaymentRequestStatus.UnderValidation)
                throw new FinanceOperationException("Chỉ đề nghị đang kiểm tra mới được xác nhận hợp lệ.");
            if (entity.AssignedAccountantUserId != userId)
                throw new FinanceOperationException("Chỉ kế toán được phân công mới được xác nhận hồ sơ.");
            if (entity.Attachments.Count == 0)
                throw new FinanceOperationException("Đề nghị phải có ít nhất một chứng từ trước khi xác nhận hợp lệ.");
        }
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        var from = entity.Status;
        entity.Status = target;
        if (target == PaymentRequestStatus.UnderValidation)
        {
            entity.SubmittedAt = now;
            entity.SubmittedByUserId = userId;
        }
        else
        {
            entity.ValidatedAt = now;
            entity.ValidatedByUserId = userId;
        }
        entity.UpdatedAt = now;
        entity.Events.Add(NewPaymentEvent(from, target, userId, request.Reason, now));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetPaymentAsync(id, ct);
    }

    private async Task ValidatePaymentAsync(PaymentRequestUpsertRequest request, int? excludeId, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ContractId, ct);
        if (contract is null || contract.Direction != ContractDirection.Downstream || contract.VendorId != request.VendorId ||
            contract.Status is ContractStatus.Draft or ContractStatus.Cancelled)
            throw new FinanceOperationException("Đề nghị thanh toán phải liên kết với hợp đồng đầu ra đang hiệu lực và đúng nhà cung cấp.");
        if (request.ContractPaymentMilestoneId.HasValue && !await db.ContractPaymentMilestones.AsNoTracking().AnyAsync(item =>
                item.Id == request.ContractPaymentMilestoneId && item.ContractId == request.ContractId, ct))
            throw new FinanceOperationException("Mốc thanh toán không thuộc hợp đồng đã chọn.");
        await ValidateAccountantAsync(request.AssignedAccountantUserId, ct);
        var invoice = request.SupplierInvoiceNumber.Trim();
        if (await db.PaymentRequests.AsNoTracking().AnyAsync(item => item.VendorId == request.VendorId &&
                item.SupplierInvoiceNumber == invoice && item.Id != excludeId, ct))
            throw new FinanceOperationException("Số hóa đơn nhà cung cấp đã tồn tại.");
        if (request.ReceivedAt!.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            throw new FinanceOperationException("Thời điểm nhận hóa đơn không được ở tương lai.");
        if (request.InvoiceDate!.Value > DateOnly.FromDateTime(request.ReceivedAt.Value))
            throw new FinanceOperationException("Ngày hóa đơn không được sau thời điểm nhận.");
        foreach (var attachment in request.Attachments)
            ValidateAttachment(attachment);
        if (request.Attachments.Select(item => item.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Attachments.Count)
            throw new FinanceOperationException("Đường dẫn chứng từ không được trùng lặp.");
    }

    private async Task ValidateCorrectionAsync(AccountingCorrectionUpsertRequest request, CancellationToken ct)
    {
        if (request.OriginalValue == request.CorrectedValue)
            throw new FinanceOperationException("Giá trị điều chỉnh phải khác giá trị ban đầu.");
        if (!CorrectionSourceTypes.Contains(request.SourceEntityType))
            throw new FinanceOperationException("Loại chứng từ nguồn không được hỗ trợ.");
        var period = await db.AccountingPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.AccountingPeriodId, ct);
        if (period?.Status != AccountingPeriodStatus.Closed)
            throw new FinanceOperationException("Chỉ được lập điều chỉnh cho kỳ kế toán đã đóng.");
        if (!await db.OperationalProjects.AsNoTracking().AnyAsync(item => item.Id == request.OperationalProjectId, ct))
            throw new FinanceOperationException("Dự án vận hành không tồn tại.");
        await ValidateAccountantAsync(request.ResponsibleAccountantUserId, ct);
        var sourceExists = request.SourceEntityType switch
        {
            nameof(PaymentRequest) => await db.PaymentRequests.AsNoTracking().AnyAsync(item => item.Id == request.SourceEntityId &&
                item.Status == PaymentRequestStatus.Paid && item.Contract.OperationalProjectId == request.OperationalProjectId, ct),
            nameof(ContractPaymentMilestone) => await db.ContractPaymentMilestones.AsNoTracking().AnyAsync(item => item.Id == request.SourceEntityId &&
                item.Contract.OperationalProjectId == request.OperationalProjectId, ct),
            nameof(Contract) => await db.Contracts.AsNoTracking().AnyAsync(item => item.Id == request.SourceEntityId &&
                item.OperationalProjectId == request.OperationalProjectId, ct),
            _ => false,
        };
        if (!sourceExists) throw new FinanceOperationException("Chứng từ nguồn không tồn tại trong dự án đã chọn.");
    }

    private async Task ValidateAccountantAsync(int userId, CancellationToken ct)
    {
        var role = await db.Users.AsNoTracking().Where(item => item.Id == userId && item.IsActive)
            .Select(item => item.RoleEntity != null ? item.RoleEntity.Code : item.Role.ToString()).SingleOrDefaultAsync(ct);
        if (!string.Equals(role, AccountantRole, StringComparison.OrdinalIgnoreCase))
            throw new FinanceOperationException("Người được phân công phải là kế toán đang hoạt động.");
    }

    private static void ValidateAttachment(PaymentAttachmentRequest attachment)
    {
        var path = attachment.FilePath.Trim();
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') ||
            path.Contains("..", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
            throw new FinanceOperationException("Đường dẫn chứng từ phải là đường dẫn tương đối theo máy chủ.");
    }

    private static void ApplyPayment(PaymentRequest entity, PaymentRequestUpsertRequest request)
    {
        entity.ContractId = request.ContractId;
        entity.VendorId = request.VendorId;
        entity.ContractPaymentMilestoneId = request.ContractPaymentMilestoneId;
        entity.SupplierInvoiceNumber = request.SupplierInvoiceNumber.Trim();
        entity.InvoiceDate = request.InvoiceDate!.Value;
        entity.InvoiceAmount = request.InvoiceAmount;
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.ReceivedAt = request.ReceivedAt!.Value.ToUniversalTime();
        entity.AssignedAccountantUserId = request.AssignedAccountantUserId;
        entity.Attachments = request.Attachments.Select(item => new PaymentRequestAttachment
        {
            FileName = item.FileName.Trim(),
            FilePath = item.FilePath.Trim(),
        }).ToList();
    }

    private static void ApplyCorrection(AccountingCorrection entity, AccountingCorrectionUpsertRequest request)
    {
        entity.OperationalProjectId = request.OperationalProjectId;
        entity.AccountingPeriodId = request.AccountingPeriodId;
        entity.SourceEntityType = request.SourceEntityType.Trim();
        entity.SourceEntityId = request.SourceEntityId;
        entity.ReasonCode = request.ReasonCode.Trim().ToUpperInvariant();
        entity.OriginalValue = request.OriginalValue;
        entity.CorrectedValue = request.CorrectedValue;
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.Note = TrimOrNull(request.Note);
        entity.ResponsibleAccountantUserId = request.ResponsibleAccountantUserId;
    }

    private static (DateTime Start, DateTime End) VietnamMonthBoundaries(int year, int month)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(localStart, zone), TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), zone));
    }

    private async Task<string> AllocatePaymentCodeAsync(CancellationToken ct) =>
        $"PAY-{(await db.PaymentRequests.MaxAsync(item => (int?)item.Id, ct) ?? 0) + 1:D6}";

    private async Task<string> AllocateCorrectionCodeAsync(CancellationToken ct) =>
        $"AC-{(await db.AccountingCorrections.MaxAsync(item => (int?)item.Id, ct) ?? 0) + 1:D6}";

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginSerializableAsync(CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    private IQueryable<PaymentRequest> PaymentQuery() => db.PaymentRequests.AsNoTracking()
        .Include(item => item.Contract).Include(item => item.Vendor).Include(item => item.AssignedAccountant)
        .Include(item => item.Attachments)
        .Include(item => item.Events.OrderBy(entry => entry.ChangedAt)).ThenInclude(entry => entry.ChangedByUser);

    private IQueryable<AccountingCorrection> CorrectionQuery() => db.AccountingCorrections.AsNoTracking()
        .Include(item => item.OperationalProject).Include(item => item.AccountingPeriod).Include(item => item.ResponsibleAccountant);

    private async Task<PaymentRequestResponse?> GetPaymentAsync(int id, CancellationToken ct)
    {
        var entity = await PaymentQuery().SingleOrDefaultAsync(item => item.Id == id, ct);
        return entity is null ? null : MapPayment(entity);
    }

    private async Task<AccountingCorrectionResponse?> GetCorrectionAsync(int id, CancellationToken ct)
    {
        var entity = await CorrectionQuery().SingleOrDefaultAsync(item => item.Id == id, ct);
        return entity is null ? null : MapCorrection(entity);
    }

    private static PaymentRequestResponse MapPayment(PaymentRequest item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        ContractId = item.ContractId,
        ContractNumber = item.Contract.ContractNumber,
        VendorId = item.VendorId,
        VendorName = item.Vendor.CompanyName,
        ContractPaymentMilestoneId = item.ContractPaymentMilestoneId,
        SupplierInvoiceNumber = item.SupplierInvoiceNumber,
        InvoiceDate = item.InvoiceDate,
        InvoiceAmount = item.InvoiceAmount,
        Currency = item.Currency,
        Status = item.Status.ToString(),
        ReceivedAt = item.ReceivedAt,
        ValidatedAt = item.ValidatedAt,
        AssignedAccountantUserId = item.AssignedAccountantUserId,
        AssignedAccountantName = item.AssignedAccountant.FullName ?? item.AssignedAccountant.Email,
        SubmittedAt = item.SubmittedAt,
        SubmittedByUserId = item.SubmittedByUserId,
        ApprovedAt = item.ApprovedAt,
        ApprovedByUserId = item.ApprovedByUserId,
        PaidAt = item.PaidAt,
        PaidByUserId = item.PaidByUserId,
        RejectedAt = item.RejectedAt,
        CancelledAt = item.CancelledAt,
        DecisionReason = item.DecisionReason,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Attachments = item.Attachments.Select(attachment => new PaymentAttachmentResponse
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            FilePath = attachment.FilePath,
        }).ToList(),
        Events = item.Events.Select(entry => new PaymentRequestEventResponse
        {
            Id = entry.Id,
            FromStatus = entry.FromStatus?.ToString(),
            ToStatus = entry.ToStatus.ToString(),
            Reason = entry.Reason,
            ChangedByUserId = entry.ChangedByUserId,
            ChangedByName = entry.ChangedByUser.FullName ?? entry.ChangedByUser.Email,
            ChangedAt = entry.ChangedAt,
        }).ToList(),
    };

    private static AccountingPeriodResponse MapPeriod(AccountingPeriod item) => new()
    {
        Id = item.Id,
        Year = item.Year,
        Month = item.Month,
        PeriodStartUtc = item.PeriodStartUtc,
        PeriodEndUtc = item.PeriodEndUtc,
        Status = item.Status.ToString(),
        ClosingAt = item.ClosingAt,
        ClosedAt = item.ClosedAt,
        CloseReason = item.CloseReason,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
    };

    private static AccountingCorrectionResponse MapCorrection(AccountingCorrection item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        OperationalProjectId = item.OperationalProjectId,
        ProjectName = item.OperationalProject.Name,
        AccountingPeriodId = item.AccountingPeriodId,
        PeriodLabel = $"{item.AccountingPeriod.Year:D4}-{item.AccountingPeriod.Month:D2}",
        SourceEntityType = item.SourceEntityType,
        SourceEntityId = item.SourceEntityId,
        ReasonCode = item.ReasonCode,
        OriginalValue = item.OriginalValue,
        CorrectedValue = item.CorrectedValue,
        Currency = item.Currency,
        Note = item.Note,
        ResponsibleAccountantUserId = item.ResponsibleAccountantUserId,
        ResponsibleAccountantName = item.ResponsibleAccountant.FullName ?? item.ResponsibleAccountant.Email,
        Status = item.Status.ToString(),
        RecordedByUserId = item.RecordedByUserId,
        SubmittedAt = item.SubmittedAt,
        ApprovedAt = item.ApprovedAt,
        RejectedAt = item.RejectedAt,
        DecisionReason = item.DecisionReason,
        ReversalOfCorrectionId = item.ReversalOfCorrectionId,
        ReversedAt = item.ReversedAt,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
    };

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PaymentRequestEvent NewPaymentEvent(
        PaymentRequestStatus? from,
        PaymentRequestStatus to,
        int userId,
        string? reason,
        DateTime? changedAt = null) => new()
        {
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            Reason = TrimOrNull(reason),
            ChangedAt = changedAt ?? DateTime.UtcNow,
        };
}