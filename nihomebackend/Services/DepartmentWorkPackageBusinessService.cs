using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed partial class DepartmentWorkPackageService
{
    public async Task<WorkPackageApprovalPreview> PreviewApprovalEmailAsync(
        int packageId, int? offerId, int callerUserId, CancellationToken ct = default,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        var package = offerId.HasValue
            ? await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct)
            : await RequireFinancePackageAsync(packageId, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        if (offerId.HasValue)
        {
            var offer = await db.ProcurementInputPriceOffers.Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == offerId && item.WorkPackageId == packageId, ct)
                ?? throw Error("Không tìm thấy báo giá đầu vào.");
            if (offer.Status is not (InputPriceOfferStatus.Draft or InputPriceOfferStatus.Rejected) ||
                offer.ValidUntil <= DateTime.UtcNow || offer.Lines.Count == 0)
                throw Error("Báo giá phải ở trạng thái Nháp/Bị từ chối, còn hiệu lực và có chi tiết trước khi gửi duyệt.");
            return await approvalEmails.PreviewAsync(package, "InputPriceOffer", offer.Id, offer.SupplierReference,
                offer.CreatedByUserId, CrmConcurrency.Encode(offer.RowVersion), callerUserId, ct, recipients);
        }
        var review = await db.DepartmentFinanceReviews.SingleOrDefaultAsync(item => item.WorkPackageId == packageId, ct)
            ?? throw Error("Không tìm thấy hồ sơ rà soát tài chính.");
        if (review.Status is not (FinanceReviewStatus.Draft or FinanceReviewStatus.Rejected))
            throw Error("Hồ sơ phải ở trạng thái Nháp hoặc Bị từ chối trước khi gửi duyệt.");
        ValidateReviewChecklist(package.Purpose, review);
        return await approvalEmails.PreviewAsync(package, "FinanceReview", review.Id, package.Code,
            review.CreatedByUserId, CrmConcurrency.Encode(review.RowVersion), callerUserId, ct, recipients);
    }

    public async Task<InputPriceOfferResponse> AddInputPriceOfferAsync(
        int packageId, UpsertInputPriceOfferRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        await ValidateInputPriceOfferAsync(packageId, request, null, ct);
        var now = DateTime.UtcNow;
        var offer = new ProcurementInputPriceOffer
        {
            WorkPackageId = packageId,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        ApplyOffer(offer, request);
        db.ProcurementInputPriceOffers.Add(offer);
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadOfferAsync(packageId, offer.Id, ct);
        await AddHistoryAsync(packageId, "InputPriceOffer", offer.Id, "Created", response, callerUserId, ct);
        return response;
    }

    public async Task<InputPriceOfferResponse?> UpdateInputPriceOfferAsync(
        int packageId, int offerId, UpsertInputPriceOfferRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var offer = await db.ProcurementInputPriceOffers.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == offerId && item.WorkPackageId == packageId, ct);
        if (offer is null) return null;
        if (offer.Status is not (InputPriceOfferStatus.Draft or InputPriceOfferStatus.Rejected))
            throw Error("Chỉ được sửa giá đầu vào ở trạng thái Nháp hoặc Bị từ chối.");
        await ValidateInputPriceOfferAsync(packageId, request, offerId, ct);
        CrmConcurrency.Apply(db, offer, request.RowVersion);
        db.ProcurementInputPriceLines.RemoveRange(offer.Lines);
        offer.Lines.Clear();
        ApplyOffer(offer, request);
        offer.Status = InputPriceOfferStatus.Draft;
        offer.DecisionReason = null;
        offer.SubmittedAt = null;
        offer.RejectedAt = null;
        offer.RejectedByUserId = null;
        offer.UpdatedAt = DateTime.UtcNow;
        offer.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, offer.UpdatedAt);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadOfferAsync(packageId, offerId, ct);
        await AddHistoryAsync(packageId, "InputPriceOffer", offerId, "Updated", response, callerUserId, ct);
        return response;
    }

    public async Task<InputPriceOfferResponse?> SubmitInputPriceOfferAsync(
        int packageId, int offerId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var offer = await db.ProcurementInputPriceOffers.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == offerId && item.WorkPackageId == packageId, ct);
        if (offer is null) return null;
        if (offer.Status == InputPriceOfferStatus.Submitted) return await LoadOfferAsync(packageId, offerId, ct);
        if (offer.Status is not (InputPriceOfferStatus.Draft or InputPriceOfferStatus.Rejected))
            throw Error("Giá đầu vào không thể gửi duyệt từ trạng thái hiện tại.");
        if (offer.ValidUntil <= DateTime.UtcNow || offer.Lines.Count == 0)
            throw Error("Giá đầu vào phải còn hiệu lực và có ít nhất một dòng trước khi gửi duyệt.");
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.EnsureMatches(offer.RowVersion, request.RowVersion);
        var submittedAt = await approvalEmails.QueueAsync(package, "InputPriceOffer", offer.Id, offer.SupplierReference,
            offer.CreatedByUserId, CrmConcurrency.Encode(offer.RowVersion), callerUserId, request.PreviewToken, ct);
        CrmConcurrency.Apply(db, offer, request.RowVersion);
        offer.Status = InputPriceOfferStatus.Submitted;
        offer.SubmittedAt = submittedAt;
        offer.DecisionReason = null;
        offer.UpdatedAt = DateTime.UtcNow;
        offer.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, offer.UpdatedAt);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadOfferAsync(packageId, offerId, ct);
        await AddHistoryAsync(packageId, "InputPriceOffer", offerId, "Submitted", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        return response;
    }

    public async Task<InputPriceOfferResponse?> DecideInputPriceOfferAsync(
        int packageId, int offerId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        if (!await IsAdministratorAsync(callerUserId, ct) && package.LeadUserId != callerUserId)
            throw ScopeError();
        var offer = await db.ProcurementInputPriceOffers.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == offerId && item.WorkPackageId == packageId, ct);
        if (offer is null) return null;
        if (offer.Status != InputPriceOfferStatus.Submitted)
            throw Error("Chỉ giá đầu vào đã gửi duyệt mới được quyết định.");
        if (offer.CreatedByUserId == callerUserId)
            throw Error("Người lập giá đầu vào không được tự phê duyệt hoặc từ chối cùng hồ sơ.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw Error("Vui lòng nhập lý do từ chối giá đầu vào.");
        if (request.Approved && offer.ValidUntil <= DateTime.UtcNow)
            throw Error("Không thể duyệt giá đầu vào đã hết hiệu lực.");
        CrmConcurrency.Apply(db, offer, request.RowVersion);
        var now = DateTime.UtcNow;
        offer.Status = request.Approved ? InputPriceOfferStatus.Approved : InputPriceOfferStatus.Rejected;
        offer.ApprovedAt = request.Approved ? now : null;
        offer.ApprovedByUserId = request.Approved ? callerUserId : null;
        offer.RejectedAt = request.Approved ? null : now;
        offer.RejectedByUserId = request.Approved ? null : callerUserId;
        offer.DecisionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        offer.UpdatedAt = now;
        offer.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadOfferAsync(packageId, offerId, ct);
        await AddHistoryAsync(packageId, "InputPriceOffer", offerId, request.Approved ? "Approved" : "Rejected", response, callerUserId, ct);
        return response;
    }

    public async Task<FinanceReviewResponse> SaveFinanceReviewAsync(
        int packageId, UpsertFinanceReviewRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireFinancePackageAsync(packageId, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var review = await db.DepartmentFinanceReviews.SingleOrDefaultAsync(item => item.WorkPackageId == packageId, ct);
        if (review is not null && review.Status is FinanceReviewStatus.Submitted or FinanceReviewStatus.Approved)
            throw Error("Hồ sơ rà soát đã gửi hoặc đã duyệt và không thể chỉnh sửa.");
        var now = DateTime.UtcNow;
        review ??= new DepartmentFinanceReview
        {
            WorkPackageId = packageId,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
        };
        if (review.Id == 0) db.DepartmentFinanceReviews.Add(review);
        else CrmConcurrency.Apply(db, review, request.RowVersion);
        review.ContractConditionsReviewed = request.ContractConditionsReviewed;
        review.GuaranteesReviewed = request.GuaranteesReviewed;
        review.PaymentTermsReviewed = request.PaymentTermsReviewed;
        review.FinancialDossierReviewed = request.FinancialDossierReviewed;
        review.Findings = string.IsNullOrWhiteSpace(request.Findings) ? null : request.Findings.Trim();
        // §5: pin the review to the quotation version it covers. A quotation that
        // is materially edited after approval is re-versioned, so the approved
        // review no longer authorises sending the newer content.
        if (package.SourceQuoteId.HasValue)
        {
            review.ReviewedQuoteVersion = await db.Quotes.AsNoTracking()
                .Where(item => item.Id == package.SourceQuoteId.Value)
                .Select(item => (int?)item.Version).SingleOrDefaultAsync(ct);
        }
        review.Status = FinanceReviewStatus.Draft;
        review.DecisionReason = null;
        review.UpdatedAt = now;
        review.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadReviewAsync(packageId, ct);
        await AddHistoryAsync(packageId, "FinanceReview", review.Id, "Saved", response, callerUserId, ct);
        return response;
    }

    public async Task<FinanceReviewResponse?> SubmitFinanceReviewAsync(
        int packageId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireFinancePackageAsync(packageId, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var review = await db.DepartmentFinanceReviews.SingleOrDefaultAsync(item => item.WorkPackageId == packageId, ct);
        if (review is null) return null;
        if (review.Status == FinanceReviewStatus.Submitted) return await LoadReviewAsync(packageId, ct);
        if (review.Status is not (FinanceReviewStatus.Draft or FinanceReviewStatus.Rejected))
            throw Error("Hồ sơ rà soát không thể gửi từ trạng thái hiện tại.");
        ValidateReviewChecklist(package.Purpose, review);
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.EnsureMatches(review.RowVersion, request.RowVersion);
        var submittedAt = await approvalEmails.QueueAsync(package, "FinanceReview", review.Id, package.Code,
            review.CreatedByUserId, CrmConcurrency.Encode(review.RowVersion), callerUserId, request.PreviewToken, ct);
        CrmConcurrency.Apply(db, review, request.RowVersion);
        review.Status = FinanceReviewStatus.Submitted;
        review.SubmittedAt = submittedAt;
        review.DecisionReason = null;
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, review.UpdatedAt);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadReviewAsync(packageId, ct);
        await AddHistoryAsync(packageId, "FinanceReview", review.Id, "Submitted", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        return response;
    }

    public async Task<FinanceReviewResponse?> DecideFinanceReviewAsync(
        int packageId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireFinancePackageAsync(packageId, ct);
        if (!await IsAdministratorAsync(callerUserId, ct) && package.LeadUserId != callerUserId)
            throw ScopeError();
        var review = await db.DepartmentFinanceReviews.SingleOrDefaultAsync(item => item.WorkPackageId == packageId, ct);
        if (review is null) return null;
        if (review.Status != FinanceReviewStatus.Submitted)
            throw Error("Chỉ hồ sơ đã gửi rà soát mới được quyết định.");
        if (review.CreatedByUserId == callerUserId)
            throw Error("Người lập hồ sơ không được tự phê duyệt hoặc từ chối cùng hồ sơ.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw Error("Vui lòng nhập lý do từ chối hồ sơ rà soát.");
        CrmConcurrency.Apply(db, review, request.RowVersion);
        var now = DateTime.UtcNow;
        review.Status = request.Approved ? FinanceReviewStatus.Approved : FinanceReviewStatus.Rejected;
        review.ReviewedAt = now;
        review.ReviewedByUserId = callerUserId;
        review.DecisionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        review.UpdatedAt = now;
        review.UpdatedByUserId = callerUserId;
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadReviewAsync(packageId, ct);
        await AddHistoryAsync(packageId, "FinanceReview", review.Id, request.Approved ? "Approved" : "Rejected", response, callerUserId, ct);
        return response;
    }

    private async Task<DepartmentWorkPackage> RequireBusinessPackageAsync(
        int packageId, WorkPackagePurpose purpose, CancellationToken ct)
    {
        var package = await db.DepartmentWorkPackages.SingleOrDefaultAsync(item => item.Id == packageId, ct)
            ?? throw Error("Không tìm thấy nhóm công việc.");
        if (package.Kind != WorkPackageKind.Project || package.Purpose != purpose)
            throw Error("Nhóm công việc không đúng mục đích nghiệp vụ.");
        if (package.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Nhóm công việc đã kết thúc.");
        return package;
    }

    private async Task<DepartmentWorkPackage> RequireFinancePackageAsync(int packageId, CancellationToken ct)
    {
        var package = await db.DepartmentWorkPackages.SingleOrDefaultAsync(item => item.Id == packageId, ct)
            ?? throw Error("Không tìm thấy nhóm công việc.");
        if (package.Purpose is not (WorkPackagePurpose.FinanceCommercialReview or WorkPackagePurpose.FinanceTenderReview))
            throw Error("Nhóm công việc không phải hồ sơ rà soát Tài chính – Kế toán.");
        if (package.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Nhóm công việc đã kết thúc.");
        return package;
    }

    private async Task EnsureCanWorkInDepartmentAsync(DepartmentWorkPackage package, int userId, CancellationToken ct)
    {
        if (await IsAdministratorAsync(userId, ct)) return;
        var groupId = await UserRoleGroupIdAsync(userId, ct);
        if (groupId != package.DepartmentRoleGroupId)
            throw ScopeError();
    }

    private async Task ValidateInputPriceOfferAsync(
        int packageId, UpsertInputPriceOfferRequest request, int? offerId, CancellationToken ct)
    {
        if (request.ValidUntil <= DateTime.UtcNow)
            throw Error("Hạn hiệu lực giá đầu vào phải ở tương lai.");
        if (request.Lines is null || request.Lines.Count is < 1 or > 1000)
            throw Error("Giá đầu vào phải có từ 1 đến 1.000 dòng.");
        if (!await db.Vendors.AsNoTracking().AnyAsync(item => item.Id == request.VendorId && item.IsActive, ct))
            throw Error("Nhà cung cấp/thầu phụ không tồn tại hoặc đã ngừng hoạt động.");
        var reference = request.SupplierReference?.Trim() ?? string.Empty;
        var duplicateOffer = await db.ProcurementInputPriceOffers.AsNoTracking().AnyAsync(item =>
            item.WorkPackageId == packageId &&
            item.Id != (offerId ?? 0) &&
            item.VendorId == request.VendorId &&
            item.SupplierReference == reference, ct);
        if (duplicateOffer)
            throw Error($"Nhà cung cấp đã có chào giá số '{reference}' trong nhóm công việc này.");
        var duplicate = request.Lines.GroupBy(item => item.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null) throw Error($"Mã hạng mục '{duplicate}' bị trùng trong cùng chào giá.");
        if (offerId.HasValue && await db.Quotes.AsNoTracking().AnyAsync(item => item.InputPriceOfferId == offerId, ct))
            throw Error("Giá đầu vào đã được báo giá tham chiếu và không thể sửa; hãy tạo chào giá mới.");
    }

    private static void ApplyOffer(ProcurementInputPriceOffer offer, UpsertInputPriceOfferRequest request)
    {
        offer.VendorId = request.VendorId;
        offer.SupplierReference = request.SupplierReference.Trim();
        offer.Currency = request.Currency.Trim().ToUpperInvariant();
        offer.ValidUntil = request.ValidUntil.ToUniversalTime();
        offer.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        offer.Lines = request.Lines.Select((line, index) =>
        {
            var amount = decimal.Round(line.Quantity * line.UnitPrice, 4, MidpointRounding.AwayFromZero);
            return new ProcurementInputPriceLine
            {
                ItemCode = line.ItemCode.Trim(),
                Description = line.Description.Trim(),
                Unit = line.Unit.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatPercent = line.VatPercent,
                Amount = amount,
                // An explicit display order wins; otherwise follow the submitted
                // order. Deriving the fallback from the position keeps the two
                // rules from producing colliding numbers on the same line set.
                SortOrder = line.SortOrder > 0 ? line.SortOrder : index + 1,
            };
        }).ToList();
    }

    private static void ValidateReviewChecklist(WorkPackagePurpose purpose, DepartmentFinanceReview review)
    {
        if (purpose == WorkPackagePurpose.FinanceCommercialReview &&
            (!review.ContractConditionsReviewed || !review.GuaranteesReviewed || !review.PaymentTermsReviewed))
            throw Error("Rà soát thương mại phải hoàn tất điều kiện hợp đồng, bảo lãnh và điều kiện thanh toán.");
        if (purpose == WorkPackagePurpose.FinanceTenderReview &&
            (!review.FinancialDossierReviewed || !review.GuaranteesReviewed))
            throw Error("Rà soát hồ sơ thầu phải hoàn tất hồ sơ tài chính và bảo lãnh trước khi gửi duyệt.");
    }

    private async Task<InputPriceOfferResponse> LoadOfferAsync(int packageId, int offerId, CancellationToken ct)
        => MapInputPriceOffer(await db.ProcurementInputPriceOffers.AsNoTracking()
            .Include(item => item.Vendor).Include(item => item.Lines)
            .SingleAsync(item => item.Id == offerId && item.WorkPackageId == packageId, ct));

    private async Task<FinanceReviewResponse> LoadReviewAsync(int packageId, CancellationToken ct)
        => MapFinanceReview(await db.DepartmentFinanceReviews.AsNoTracking().Include(item => item.ReviewedByUser)
            .SingleAsync(item => item.WorkPackageId == packageId, ct));

    private static InputPriceOfferResponse MapInputPriceOffer(ProcurementInputPriceOffer offer)
    {
        var subtotal = offer.Lines.Sum(line => line.Amount);
        var vat = offer.Lines.Sum(line => decimal.Round(line.Amount * line.VatPercent / 100m, 4, MidpointRounding.AwayFromZero));
        return new InputPriceOfferResponse
        {
            Id = offer.Id,
            WorkPackageId = offer.WorkPackageId,
            VendorId = offer.VendorId,
            VendorCode = offer.Vendor.VendorCode,
            VendorName = offer.Vendor.CompanyName,
            VendorType = offer.Vendor.VendorType.ToString(),
            SupplierReference = offer.SupplierReference,
            Currency = offer.Currency,
            ValidUntil = offer.ValidUntil,
            Status = offer.Status.ToString(),
            Note = offer.Note,
            DecisionReason = offer.DecisionReason,
            Subtotal = subtotal,
            VatAmount = vat,
            GrandTotal = subtotal + vat,
            CreatedByUserId = offer.CreatedByUserId,
            RowVersion = CrmConcurrency.Encode(offer.RowVersion),
            Lines = offer.Lines.OrderBy(line => line.SortOrder).Select(line => new InputPriceLineResponse
            {
                Id = line.Id,
                ItemCode = line.ItemCode,
                Description = line.Description,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatPercent = line.VatPercent,
                Amount = line.Amount,
                SortOrder = line.SortOrder,
            }).ToList(),
        };
    }

    private static FinanceReviewResponse MapFinanceReview(DepartmentFinanceReview review) => new()
    {
        Id = review.Id,
        WorkPackageId = review.WorkPackageId,
        Status = review.Status.ToString(),
        ContractConditionsReviewed = review.ContractConditionsReviewed,
        GuaranteesReviewed = review.GuaranteesReviewed,
        PaymentTermsReviewed = review.PaymentTermsReviewed,
        FinancialDossierReviewed = review.FinancialDossierReviewed,
        Findings = review.Findings,
        DecisionReason = review.DecisionReason,
        CreatedByUserId = review.CreatedByUserId,
        ReviewedByUserId = review.ReviewedByUserId,
        ReviewedByName = review.ReviewedByUser?.FullName ?? review.ReviewedByUser?.Email,
        RowVersion = CrmConcurrency.Encode(review.RowVersion),
    };
}
