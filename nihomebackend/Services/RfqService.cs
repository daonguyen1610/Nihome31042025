using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class RfqService(AppDbContext db, INotificationService notifications,
    IEmailService email, IConfiguration configuration, ILogger<RfqService> logger,
    IProjectDocumentService documents)
{
    private static readonly HashSet<string> SupportedCurrencyCodes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(culture => new RegionInfo(culture.Name).ISOCurrencySymbol)
        .Append("VND").ToHashSet(StringComparer.Ordinal);
    // Take an update lock before reading mutable state to avoid shared-lock
    // conversion deadlocks between concurrent awards or bid transitions.
    private IQueryable<Rfq> MutableRoots() => db.Database.IsSqlServer()
        ? db.Rfqs.FromSqlRaw("SELECT * FROM [rfqs] WITH (UPDLOCK)") : db.Rfqs;

    private IQueryable<Rfq> Query(bool forUpdate = false) => (forUpdate ? MutableRoots() : db.Rfqs)
        .Include(x => x.OperationalProject).ThenInclude(x => x.Customer)
        .Include(x => x.SourceBoqRevision).Include(x => x.Owner)
        .Include(x => x.Lines).Include(x => x.Invitations).ThenInclude(x => x.Vendor)
        .Include(x => x.Bids).ThenInclude(x => x.Lines)
        .Include(x => x.Bids).ThenInclude(x => x.SubmittedBy)
        .Include(x => x.Bids).ThenInclude(x => x.EvaluatedBy)
        .Include(x => x.Awards).ThenInclude(x => x.Contract)
        .Include(x => x.Awards).ThenInclude(x => x.Vendor)
        .Include(x => x.Awards).ThenInclude(x => x.RfqBid)
        .Include(x => x.Awards).ThenInclude(x => x.Lines).ThenInclude(x => x.RfqBidLine)
        .Include(x => x.Awards).ThenInclude(x => x.Lines).ThenInclude(x => x.MaterialRequestAllocations).ThenInclude(x => x.MaterialRequestLine).ThenInclude(x => x.MaterialRequest)
        .Include(x => x.Events).ThenInclude(x => x.Actor)
        .Include(x => x.Documents).ThenInclude(x => x.ProjectDocument)
        .Include(x => x.Contract).Include(x => x.AwardedBy).AsSplitQuery();

    private IQueryable<Rfq> Filter(int projectId, RfqListQuery query)
    {
        var rows = db.Rfqs.AsNoTracking().Where(x => x.OperationalProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(x => x.Code.Contains(term) || x.Title.Contains(term) ||
                x.Invitations.Any(v => v.VendorName.Contains(term)));
        }
        if (query.Status.HasValue) rows = rows.Where(x => x.Status == query.Status);
        if (query.OwnerUserId.HasValue) rows = rows.Where(x => x.OwnerUserId == query.OwnerUserId);
        if (query.DueFrom.HasValue) rows = rows.Where(x => x.DueAt >= query.DueFrom);
        if (query.DueTo.HasValue) rows = rows.Where(x => x.DueAt <= query.DueTo);
        if (query.Overdue)
        {
            var now = DateTime.UtcNow;
            rows = rows.Where(x => x.DueAt < now && (x.Status == RfqStatus.Issued || x.Status == RfqStatus.UnderEvaluation));
        }
        var descending = query.SortDirection == "desc";
        return (query.SortBy, descending) switch
        {
            ("dueAt", true) => rows.OrderByDescending(x => x.DueAt).ThenByDescending(x => x.Id),
            ("dueAt", false) => rows.OrderBy(x => x.DueAt).ThenBy(x => x.Id),
            ("code", true) => rows.OrderByDescending(x => x.Code).ThenByDescending(x => x.Id),
            ("code", false) => rows.OrderBy(x => x.Code).ThenBy(x => x.Id),
            (_, false) => rows.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            _ => rows.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id),
        };
    }

    public async Task<RfqListResponse> ListAsync(int projectId, RfqListQuery query, bool export, CancellationToken ct)
    {
        var rows = Filter(projectId, query);
        var total = await rows.CountAsync(ct);
        if (!export) rows = rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
        // Only list fields are loaded here; bid prices and files stay on the detail endpoint.
        var now = DateTime.UtcNow;
        var items = await rows.Select(x => new RfqListItemResponse(x.Id, x.OperationalProjectId, x.Code, x.Title,
            x.OperationalProject.Code, x.OperationalProject.Name, x.OperationalProject.Customer.Name,
            x.SourceBoqRevisionId, x.SourceBoqRevision.RevisionNumber, x.Status, x.OwnerUserId, x.Owner.FullName ?? x.Owner.PhoneNumber,
            x.Invitations.Count, x.Bids.Where(b => b.WithdrawnAt == null &&
                !x.Bids.Any(newer => newer.VendorId == b.VendorId && newer.Revision > b.Revision)).Count(),
            Utc(x.IssuedAt), Utc(x.DueAt), Utc(x.UpdatedAt),
            x.DueAt < now && (x.Status == RfqStatus.Issued || x.Status == RfqStatus.UnderEvaluation),
            CrmConcurrency.Encode(x.RowVersion))).ToListAsync(ct);
        return new(total, query.Page, query.PageSize, items);
    }

    public async Task<RfqDetailResponse?> GetAsync(int projectId, int id, CancellationToken ct)
    {
        var entity = await Query().AsNoTracking().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (entity is null) return null;
        var vendorIds = entity.Invitations.Select(x => x.VendorId).ToList();
        var ratings = await db.VendorRatings.AsNoTracking()
            .Where(x => vendorIds.Contains(x.VendorId) && x.Status == VendorRatingStatus.Approved)
            .GroupBy(x => x.VendorId)
            .Select(group => new { VendorId = group.Key, Score = group.Average(x => x.OverallScore) })
            .ToDictionaryAsync(x => x.VendorId, x => x.Score, ct);
        var materialRequests = await MaterialRequestOptionsAsync(entity, ct);
        return Map(entity, ratings, materialRequests);
    }

    private IQueryable<ApplicationUser> Owners(int projectId) => db.Users.AsNoTracking().Where(u => u.IsActive &&
        u.RoleEntity != null && u.RoleEntity.IsActive && u.RoleEntity.Code == "PROCUREMENT" &&
        db.OperationalProjectMembers.Any(m => m.OperationalProjectId == projectId && m.UserId == u.Id && m.EndedAt == null));

    private IQueryable<ProjectBoqRevision> ApprovedBoqs(int projectId) => db.ProjectBoqRevisions.AsNoTracking()
        .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved && x.Currency == "VND");

    public async Task<RfqReferenceResponse> ReferencesAsync(int projectId, CancellationToken ct)
    {
        var revisions = await ApprovedBoqs(projectId).Include(x => x.Lines).OrderByDescending(x => x.RevisionNumber).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().Where(x => x.IsActive &&
            (x.VendorType == VendorType.Supplier || x.VendorType == VendorType.SubContractor || x.VendorType == VendorType.Both))
            .OrderBy(x => x.CompanyName).Select(x => new RfqVendorResponse(x.Id, x.CompanyName,
                x.VendorType, x.IsActive, false, null, null)).ToListAsync(ct);
        var owners = await Owners(projectId).OrderBy(x => x.FullName).Select(x => new RfqUserOption(x.Id, x.FullName ?? x.PhoneNumber)).ToListAsync(ct);
        var documents = await db.ProjectDocuments.AsNoTracking().Where(x => x.OperationalProjectId == projectId &&
            x.Category == ProjectDocumentCategory.Procurement && x.DeletedAt == null &&
            x.DesiredOperation != ProjectDocumentDesiredOperation.Delete && x.SyncStatus != ProjectDocumentSyncStatus.Deleted &&
            x.SourceEntityType == null && !db.RfqDocuments.Any(link => link.ProjectDocumentId == x.Id))
            .OrderByDescending(x => x.CreatedAt).Select(x => new RfqFileResponse(x.Id, null, x.OriginalFileName)).ToListAsync(ct);
        return new(revisions.Select(x => new RfqBoqOption(x.Id, x.RevisionNumber, x.Currency,
            x.Lines.OrderBy(l => l.SortOrder).Select(l => new RfqBoqLineOption(l.Id, l.ItemCode, l.Description, l.Unit, l.ApprovedQuantity)).ToList())).ToList(),
            vendors, owners, documents);
    }

    public async Task<RfqDetailResponse?> SaveAsync(int projectId, int? id, RfqUpsertRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(ct, creating: !id.HasValue);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = id.HasValue ? await LoadMutableAsync(projectId, id.Value, request.RowVersion, ct) : new Rfq
        {
            OperationalProjectId = projectId,
            Code = $"RFQ-{projectId}-{Guid.NewGuid().ToString("N")[..20]}",
        };
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.Draft, "Only draft RFQs can be edited. Issued RFQs retain their original scope.");
        Require(!string.IsNullOrWhiteSpace(request.Title) && request.Title.Trim().Length <= 200, "Title is required and must be at most 200 characters.");
        Require(request.DueAt.Kind != DateTimeKind.Unspecified && request.DueAt.ToUniversalTime() > DateTime.UtcNow, "Due date must include a timezone and be in the future, e.g. 2026-12-01T09:00:00Z.");
        Require(await Owners(projectId).AnyAsync(x => x.Id == request.OwnerUserId, ct), "Owner must be an active Procurement member of this project.");
        var revision = await ApprovedBoqs(projectId).Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == request.SourceBoqRevisionId, ct);
        Require(revision is not null, "Source BOQ must be approved, use VND, and belong to this project.");
        Require(request.VendorIds.Count is > 0 and <= 100 && request.VendorIds.Distinct().Count() == request.VendorIds.Count,
            "Invite 1 to 100 different active vendors.");
        var vendors = await db.Vendors.Where(x => request.VendorIds.Contains(x.Id) && x.IsActive &&
            (x.VendorType == VendorType.Supplier || x.VendorType == VendorType.SubContractor || x.VendorType == VendorType.Both)).ToListAsync(ct);
        Require(vendors.Count == request.VendorIds.Count, "All invited vendors must be active suppliers or subcontractors.");
        Require(request.Lines.Count is > 0 and <= 500 && request.Lines.Select(x => x.ProjectBoqLineId).Distinct().Count() == request.Lines.Count,
            "Select 1 to 500 different BOQ lines.");
        Require(request.PriceWeight + request.LeadTimeWeight + request.VendorRatingWeight + request.CommercialWeight == 100m,
            "RFQ scoring weights must total exactly 100 percent.");
        var source = revision!.Lines.ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
            Require(source.TryGetValue(line.ProjectBoqLineId, out var boq) && line.Quantity > 0 &&
                line.Quantity <= boq.ApprovedQuantity && decimal.Round(line.Quantity, 6) == line.Quantity,
                "Quantity must be positive, use at most 6 decimal places, and not exceed its source BOQ line.");
        entity.Title = request.Title.Trim();
        entity.SourceBoqRevisionId = revision.Id;
        entity.OwnerUserId = request.OwnerUserId;
        entity.DueAt = request.DueAt.ToUniversalTime();
        entity.Currency = revision.Currency;
        entity.Note = Trim(request.Note);
        entity.PriceWeight = request.PriceWeight;
        entity.LeadTimeWeight = request.LeadTimeWeight;
        entity.VendorRatingWeight = request.VendorRatingWeight;
        entity.CommercialWeight = request.CommercialWeight;
        // Keep retained child IDs stable and remove only draft-owned rows.
        foreach (var line in entity.Lines.Where(x => !request.Lines.Any(l => l.ProjectBoqLineId == x.ProjectBoqLineId)).ToList())
        {
            db.Remove(line);
            entity.Lines.Remove(line);
        }
        foreach (var input in request.Lines)
        {
            var line = entity.Lines.SingleOrDefault(x => x.ProjectBoqLineId == input.ProjectBoqLineId);
            if (line is null) { line = new RfqLine { ProjectBoqLineId = input.ProjectBoqLineId }; entity.Lines.Add(line); }
            var boq = source[input.ProjectBoqLineId];
            line.ItemCode = boq.ItemCode;
            line.Description = boq.Description;
            line.Unit = boq.Unit;
            line.BudgetUnitPrice = boq.BudgetUnitPrice;
            line.Quantity = input.Quantity;
        }
        foreach (var invitation in entity.Invitations.Where(x => !request.VendorIds.Contains(x.VendorId)).ToList())
        {
            db.Remove(invitation);
            entity.Invitations.Remove(invitation);
        }
        foreach (var vendor in vendors)
        {
            var invitation = entity.Invitations.SingleOrDefault(x => x.VendorId == vendor.Id);
            if (invitation is null) { invitation = new RfqInvitation { VendorId = vendor.Id }; entity.Invitations.Add(invitation); }
            invitation.VendorName = vendor.CompanyName;
        }
        if (!id.HasValue) db.Rfqs.Add(entity);
        Event(entity, id.HasValue ? "updated" : "created", actor);
        await SaveAsync(transaction, ct);
        return await GetAsync(projectId, entity.Id, ct);
    }

    public async Task<RfqDetailResponse?> TransitionAsync(int projectId, int id, string action,
        ProcurementTransitionRequest request, int actor, CancellationToken ct)
    {
        var portalDeliveries = new List<(RfqInvitation Invitation, string Token, string Email)>();
        await using var transaction = await BeginAsync(ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        switch (action)
        {
            case "issue":
                Require(entity.Status == RfqStatus.Draft && entity.DueAt > DateTime.UtcNow, "Only a draft RFQ with a future due date can be issued.");
                Require(await ApprovedBoqs(projectId).AnyAsync(x => x.Id == entity.SourceBoqRevisionId, ct), "The source BOQ is no longer approved.");
                Require(await Owners(projectId).AnyAsync(x => x.Id == entity.OwnerUserId, ct), "The RFQ owner is no longer an active Procurement member.");
                Require(entity.Invitations.All(x => x.Vendor.IsActive), "All invited vendors must still be active.");
                entity.Status = RfqStatus.Issued;
                entity.IssuedAt = DateTime.UtcNow;
                foreach (var invitation in entity.Invitations)
                {
                    if (!ContactValidation.IsValidEmail(invitation.Vendor.Email))
                    {
                        invitation.InvitationDeliveryError = "Vendor email is missing or invalid; Procurement must record this quotation manually.";
                        continue;
                    }
                    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    invitation.PortalTokenHash = HashPortalToken(token);
                    invitation.PortalTokenExpiresAt = entity.DueAt;
                    invitation.InvitationDeliveryError = null;
                    portalDeliveries.Add((invitation, token, invitation.Vendor.Email!));
                }
                break;
            case "evaluate":
                Require(entity.Status == RfqStatus.Issued && CurrentBids(entity).Any(x => x.WithdrawnAt == null), "Evaluation requires an issued RFQ with at least one submitted bid.");
                entity.Status = RfqStatus.UnderEvaluation;
                break;
            case "cancel":
                Require(entity.Status is RfqStatus.Draft or RfqStatus.Issued or RfqStatus.UnderEvaluation, "Only an unawarded RFQ can be cancelled.");
                Require(Trim(request.Reason)?.Length >= 3, "Cancellation reason must contain at least 3 characters.");
                entity.Status = RfqStatus.Cancelled;
                break;
            case "close":
                Require(entity.Status == RfqStatus.Awarded, "Only an awarded RFQ can be closed.");
                entity.Status = RfqStatus.Closed;
                break;
            default: throw new ProcurementOperationException("Unknown RFQ transition.");
        }
        Event(entity, action, actor, Trim(request.Reason));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        if (action == "issue")
        {
            await NotifyBestEffortAsync(entity, "issued", ct);
            await DeliverInvitationsAsync(entity, portalDeliveries, ct);
        }
        return await GetAsync(projectId, id, ct);
    }

    public async Task<VendorPortalRfqResponse?> GetPortalAsync(string token, CancellationToken ct)
    {
        var hash = HashPortalToken(token);
        var invitation = await db.RfqInvitations.Include(x => x.Vendor)
            .Include(x => x.Rfq).ThenInclude(x => x.Lines)
            .SingleOrDefaultAsync(x => x.PortalTokenHash == hash, ct);
        if (invitation is null || invitation.PortalTokenExpiresAt < DateTime.UtcNow ||
            invitation.Rfq.Status != RfqStatus.Issued || !invitation.Vendor.IsActive) return null;
        invitation.LastPortalAccessAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var packageDocuments = await db.RfqDocuments.AsNoTracking()
            .Where(x => x.RfqId == invitation.RfqId && x.RfqBidId == null)
            .OrderBy(x => x.Id)
            .Select(x => new VendorPortalRfqDocumentResponse(x.ProjectDocumentId, x.ProjectDocument.OriginalFileName))
            .ToListAsync(ct);
        return new VendorPortalRfqResponse(invitation.Rfq.Code, invitation.Rfq.Title,
            Utc(invitation.Rfq.DueAt), invitation.VendorName,
            invitation.Rfq.Lines.OrderBy(x => x.Id).Select(x =>
                new VendorPortalRfqLineResponse(x.Id, x.ItemCode, x.Description, x.Unit, x.Quantity)).ToList(),
            packageDocuments, invitation.Rfq.DueAt >= DateTime.UtcNow);
    }

    public async Task<ProjectDocumentDownload?> DownloadPortalDocumentAsync(string token, long documentId,
        CancellationToken ct)
    {
        var hash = HashPortalToken(token);
        var invitation = await db.RfqInvitations.AsNoTracking().Include(x => x.Rfq)
            .SingleOrDefaultAsync(x => x.PortalTokenHash == hash, ct);
        if (invitation is null || invitation.PortalTokenExpiresAt < DateTime.UtcNow ||
            invitation.Rfq.Status != RfqStatus.Issued ||
            !await db.RfqDocuments.AsNoTracking().AnyAsync(x => x.RfqId == invitation.RfqId &&
                x.RfqBidId == null && x.ProjectDocumentId == documentId, ct)) return null;
        return await documents.DownloadAsync(invitation.Rfq.OperationalProjectId, documentId,
            invitation.Rfq.OwnerUserId, canSeeAll: true, ct);
    }

    public async Task<VendorPortalRfqResponse?> SubmitPortalBidAsync(string token,
        VendorPortalBidRequest request, CancellationToken ct)
    {
        var hash = HashPortalToken(token);
        var invitation = await db.RfqInvitations.AsNoTracking().Include(x => x.Rfq)
            .SingleOrDefaultAsync(x => x.PortalTokenHash == hash, ct);
        if (invitation is null || invitation.PortalTokenExpiresAt < DateTime.UtcNow ||
            invitation.Rfq.Status != RfqStatus.Issued) return null;
        var bidRequest = new RfqBidRequest
        {
            VendorId = invitation.VendorId,
            LeadTimeDays = request.LeadTimeDays,
            PaymentTerms = request.PaymentTerms,
            ValidUntil = request.ValidUntil,
            Note = request.Note,
            Lines = request.Lines,
            DocumentIds = [],
            RowVersion = CrmConcurrency.Encode(invitation.Rfq.RowVersion),
            Currency = request.Currency,
            ExchangeRateToVnd = request.ExchangeRateToVnd,
            FreightAmount = request.FreightAmount,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = request.DiscountAmount,
            VatPercent = request.VatPercent,
        };
        var result = await SubmitBidAsync(invitation.Rfq.OperationalProjectId, invitation.RfqId,
            bidRequest, invitation.Rfq.OwnerUserId, ct, submittedViaPortal: true,
            portalActorLabel: $"{invitation.VendorName} (vendor portal)");
        if (result is null) return null;
        return await GetPortalAsync(token, ct);
    }

    private async Task DeliverInvitationsAsync(Rfq entity,
        IReadOnlyList<(RfqInvitation Invitation, string Token, string Email)> deliveries, CancellationToken ct)
    {
        var origin = configuration.GetValue<string>("Frontend:PublicBaseUrl") ??
            configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>()?.FirstOrDefault();
        foreach (var delivery in deliveries)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(origin)) throw new InvalidOperationException("Frontend public URL is not configured.");
                var link = $"{origin.TrimEnd('/')}/vendor/rfqs#token={delivery.Token}";
                await email.SendEmailAsync(delivery.Email, $"RFQ {entity.Code}: {entity.Title}",
                    $"<p>{WebUtility.HtmlEncode(delivery.Invitation.VendorName)},</p>" +
                    $"<p>You are invited to quote for <strong>{WebUtility.HtmlEncode(entity.Title)}</strong>.</p>" +
                    $"<p>Deadline: {entity.DueAt:O}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Open secure quotation form</a></p>");
                delivery.Invitation.InvitationSentAt = DateTime.UtcNow;
                delivery.Invitation.InvitationDeliveryError = null;
            }
            catch (Exception exception)
            {
                delivery.Invitation.InvitationDeliveryError = exception.Message[..Math.Min(exception.Message.Length, 1000)];
                logger.LogWarning(exception, "RFQ invitation delivery failed for invitation {InvitationId}.", delivery.Invitation.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string HashPortalToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<RfqDetailResponse?> ResendInvitationsAsync(int projectId, int id,
        ProcurementTransitionRequest request, int actor, CancellationToken ct)
    {
        var deliveries = new List<(RfqInvitation Invitation, string Token, string Email)>();
        await using var transaction = await BeginAsync(ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.Issued && entity.DueAt > DateTime.UtcNow,
            "Invitations can be resent only for an issued RFQ before its deadline.");
        foreach (var invitation in entity.Invitations)
        {
            if (!ContactValidation.IsValidEmail(invitation.Vendor.Email))
            {
                invitation.InvitationDeliveryError = "Vendor email is missing or invalid; Procurement must record this quotation manually.";
                continue;
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            invitation.PortalTokenHash = HashPortalToken(token);
            invitation.PortalTokenExpiresAt = entity.DueAt;
            invitation.InvitationDeliveryError = null;
            deliveries.Add((invitation, token, invitation.Vendor.Email!));
        }
        Event(entity, "invitations-resent", actor);
        await SaveAsync(transaction, ct);
        await DeliverInvitationsAsync(entity, deliveries, ct);
        return await GetAsync(projectId, id, ct);
    }

    public async Task<RfqDetailResponse?> SubmitBidAsync(int projectId, int id, RfqBidRequest request,
        int actor, CancellationToken ct, bool submittedViaPortal = false, string? portalActorLabel = null)
    {
        await using var transaction = await BeginAsync(ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.Issued && entity.DueAt >= DateTime.UtcNow, "Bids can only be submitted before the deadline of an issued RFQ.");
        Require(entity.Invitations.Any(x => x.VendorId == request.VendorId && x.Vendor.IsActive), "Vendor must be active and invited to this RFQ.");
        Require(request.ValidUntil.Kind != DateTimeKind.Unspecified && request.ValidUntil.ToUniversalTime() >= entity.DueAt && request.ValidUntil.ToUniversalTime() > DateTime.UtcNow, "Quote validity must include a timezone and cover the RFQ deadline.");
        Require(!string.IsNullOrWhiteSpace(request.PaymentTerms), "Payment terms are required.");
        Require(request.Lines.Count > 0 && request.Lines.Select(x => x.RfqLineId).Distinct().Count() == request.Lines.Count,
            "Provide at least one unique quoted RFQ line; unquoted lines remain missing.");
        var sources = entity.Lines.ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
            Require(sources.ContainsKey(line.RfqLineId) && line.UnitPrice >= 0 && decimal.Round(line.UnitPrice, 4) == line.UnitPrice,
                "Unit price must be non-negative, use at most 4 decimal places, and refer to a line in this RFQ.");
        var currency = request.Currency.Trim().ToUpperInvariant();
        Require(SupportedCurrencyCodes.Contains(currency), "Bid currency must be a supported ISO 4217 code, for example VND or USD.");
        Require(currency != "VND" || request.ExchangeRateToVnd == 1m,
            "The VND exchange rate must equal 1.");
        Require(request.DiscountPercent == 0m || request.DiscountAmount == 0m,
            "Use either a discount percent or a discount amount, not both.");
        var bid = new RfqBid
        {
            VendorId = request.VendorId,
            Revision = entity.Bids.Where(x => x.VendorId == request.VendorId).Select(x => x.Revision).DefaultIfEmpty(0).Max() + 1,
            LeadTimeDays = request.LeadTimeDays,
            PaymentTerms = request.PaymentTerms.Trim(),
            ValidUntil = request.ValidUntil.ToUniversalTime(),
            Note = Trim(request.Note),
            SubmittedByUserId = actor,
            Currency = currency,
            ExchangeRateToVnd = request.ExchangeRateToVnd,
            FreightAmount = request.FreightAmount,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = request.DiscountAmount,
            VatPercent = request.VatPercent,
            SubmittedViaPortal = submittedViaPortal,
            Lines = request.Lines.Select(x => new RfqBidLine
            {
                RfqLineId = x.RfqLineId,
                UnitPrice = x.UnitPrice,
                Amount = CalculateLineAmount(sources[x.RfqLineId].Quantity, x.UnitPrice),
            }).ToList(),
        };
        bid.Subtotal = bid.Lines.Sum(x => x.Amount);
        var totals = CalculateCommercialTotals(bid.Subtotal, bid.FreightAmount,
            bid.DiscountPercent, bid.DiscountAmount, bid.VatPercent, bid.ExchangeRateToVnd);
        bid.DiscountAmount = totals.DiscountAmount;
        bid.TotalOriginal = totals.TotalOriginal;
        bid.Total = totals.TotalVnd;
        Require(bid.Total <= 99999999999999.9999m, "Quoted total exceeds the supported amount (99,999,999,999,999.9999 VND).");
        var documents = await ValidateDocumentsAsync(projectId, request.DocumentIds, ct);
        entity.Bids.Add(bid);
        foreach (var document in documents) Attach(entity, document, bid);
        Event(entity, submittedViaPortal ? "portal-bid-submitted" : "bid-submitted", actor,
            submittedViaPortal
                ? $"Vendor #{bid.VendorId} submitted revision {bid.Revision} through the secure portal."
            : $"Vendor #{bid.VendorId}, revision {bid.Revision}.", portalActorLabel);
        await SaveAsync(transaction, ct);
        return await GetAsync(projectId, id, ct);
    }

    public async Task<RfqDetailResponse?> WithdrawBidAsync(int projectId, int id, int bidId,
        ProcurementTransitionRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        var bid = CurrentBids(entity).SingleOrDefault(x => x.Id == bidId);
        if (bid is null) return null;
        Require(entity.Status == RfqStatus.Issued && bid.WithdrawnAt is null, "Only a current bid in an issued RFQ may be withdrawn.");
        Require(Trim(request.Reason)?.Length >= 3, "Withdrawal reason must contain at least 3 characters.");
        bid.WithdrawnAt = DateTime.UtcNow;
        Event(entity, "bid-withdrawn", actor, Trim(request.Reason));
        await SaveAsync(transaction, ct);
        return await GetAsync(projectId, id, ct);
    }

    public async Task<RfqDetailResponse?> EvaluateBidAsync(int projectId, int id,
        RfqBidEvaluationRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.UnderEvaluation,
            "Bid scoring is available only while the RFQ is under evaluation.");
        var bid = CurrentBids(entity).SingleOrDefault(x => x.Id == request.BidId);
        Require(bid is not null && bid.WithdrawnAt is null && bid.ValidUntil >= DateTime.UtcNow,
            "Only a current, active quotation can be evaluated.");
        bid!.CommercialScore = request.CommercialScore;
        bid.EvaluationNote = request.Note.Trim();
        bid.EvaluatedByUserId = actor;
        bid.EvaluatedAt = DateTime.UtcNow;
        Event(entity, "bid-evaluated", actor, $"Bid #{bid.Id}: {bid.CommercialScore:0.##}/100. {bid.EvaluationNote}");
        await SaveAsync(transaction, ct);
        return await GetAsync(projectId, id, ct);
    }

    public async Task<RfqDetailResponse?> BatchAwardAsync(int projectId, int id,
        RfqBatchAwardRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(ct);
        await AcquireMaterialRequestAllocationLockAsync(projectId, ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.UnderEvaluation, "The RFQ must be under evaluation before award.");
        Require(request.Lines.Count > 0, "At least one award allocation is required.");
        Require(request.Lines.All(x => decimal.Round(x.Quantity, 6) == x.Quantity),
            "Award quantities support at most 6 decimal places.");
        Require(await Owners(projectId).AnyAsync(x => x.Id == entity.OwnerUserId, ct),
            "The RFQ owner is no longer an active Procurement member of this project.");
        var currentBoqId = await db.ProjectBoqRevisions.AsNoTracking()
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(x => x.ApprovedAt).ThenByDescending(x => x.RevisionNumber)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        Require(currentBoqId == entity.SourceBoqRevisionId,
            "The approved BOQ has changed. Cancel this RFQ and prepare a new request against the current BOQ.");

        var currentBids = CurrentBids(entity).ToDictionary(x => x.Id);
        var rfqLines = entity.Lines.ToDictionary(x => x.Id);
        foreach (var allocation in request.Lines)
        {
            Require(rfqLines.ContainsKey(allocation.RfqLineId), "Every award allocation must reference a line in this RFQ.");
            Require(currentBids.TryGetValue(allocation.BidId, out var bid) &&
                bid.WithdrawnAt is null && bid.ValidUntil >= DateTime.UtcNow &&
                entity.Invitations.Any(x => x.VendorId == bid.VendorId && x.Vendor.IsActive) &&
                bid.Lines.Any(x => x.RfqLineId == allocation.RfqLineId),
                "Every award allocation must use a current, unexpired quote for that RFQ line.");
            var vendor = entity.Invitations.Single(x => x.VendorId == bid!.VendorId).Vendor;
            Require(allocation.ContractType is ContractType.Supply or ContractType.Subcontract &&
                (vendor.VendorType == VendorType.Both || allocation.ContractType == ContractType.Supply && vendor.VendorType == VendorType.Supplier ||
                 allocation.ContractType == ContractType.Subcontract && vendor.VendorType == VendorType.SubContractor),
                "Contract type must match the selected supplier or subcontractor.");
            if (allocation.ContractType == ContractType.Supply)
                Require(allocation.MaterialRequestAllocations.Count > 0 &&
                    allocation.MaterialRequestAllocations.Sum(x => x.Quantity) == allocation.Quantity,
                    "Each supplied quantity must be fully allocated to approved Material Request demand.");
            else
                Require(allocation.MaterialRequestAllocations.Count == 0,
                    "Subcontract allocations do not use Material Request demand.");
        }
        foreach (var line in entity.Lines)
            Require(request.Lines.Where(x => x.RfqLineId == line.Id).Sum(x => x.Quantity) == line.Quantity,
                $"Award allocations for {line.ItemCode} must equal the RFQ quantity {line.Quantity}.");

        var requestedMrIds = request.Lines.SelectMany(x => x.MaterialRequestAllocations)
            .Select(x => x.MaterialRequestLineId).Distinct().ToList();
        var mrLines = await db.MaterialRequestLines.Include(x => x.MaterialRequest)
            .Where(x => requestedMrIds.Contains(x.Id) && x.MaterialRequest.OperationalProjectId == projectId &&
                (x.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                 x.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled))
            .ToDictionaryAsync(x => x.Id, ct);
        Require(mrLines.Count == requestedMrIds.Count,
            "Material Request allocations must reference approved, unfulfilled demand in this project.");
        var alreadyAllocated = await db.RfqAwardMaterialRequestAllocations.AsNoTracking()
            .Where(x => requestedMrIds.Contains(x.MaterialRequestLineId))
            .GroupBy(x => x.MaterialRequestLineId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        foreach (var group in request.Lines.SelectMany(x => x.MaterialRequestAllocations)
            .GroupBy(x => x.MaterialRequestLineId))
        {
            var mrLine = mrLines[group.Key];
            Require(alreadyAllocated.GetValueOrDefault(group.Key) + group.Sum(x => x.Quantity) <= mrLine.RequestedQuantity,
                $"Award allocation exceeds remaining demand for Material Request {mrLine.MaterialRequest.Code}.");
        }
        foreach (var allocation in request.Lines)
            foreach (var mr in allocation.MaterialRequestAllocations)
                Require(mrLines[mr.MaterialRequestLineId].ProjectBoqLineId == rfqLines[allocation.RfqLineId].ProjectBoqLineId,
                    "Material Request allocation must reference the same BOQ line as the RFQ allocation.");

        var ratings = await VendorRatingsAsync(entity, ct);
        var lineScores = CalculateLineScores(entity, ratings);
        Require(request.Lines.All(x => lineScores.GetValueOrDefault((x.BidId, x.RfqLineId))?.Weighted.HasValue == true),
            "Every selected quotation line must have a commercial evaluation before award.");
        var selectsLowerScore = request.Lines.Any(allocation =>
        {
            var selected = lineScores[(allocation.BidId, allocation.RfqLineId)].Weighted!.Value;
            var best = lineScores.Where(x => x.Key.RfqLineId == allocation.RfqLineId && x.Value.Weighted.HasValue)
                .Select(x => x.Value.Weighted!.Value).DefaultIfEmpty().Max();
            return selected < best;
        });
        if (selectsLowerScore)
            Require(Trim(request.OverrideReason)?.Length >= 3,
                "Selecting a quotation below the highest weighted score requires an override reason.");

        entity.AwardSnapshotJson = JsonSerializer.Serialize(new { comparison = Map(entity, ratings), request.Lines, request.OverrideReason });
        var now = DateTime.UtcNow;
        foreach (var group in request.Lines.GroupBy(x => x.BidId))
        {
            var bid = currentBids[group.Key];
            var vendor = entity.Invitations.Single(x => x.VendorId == bid.VendorId).Vendor;
            var contractTypes = group.Select(x => x.ContractType).Distinct().ToList();
            Require(contractTypes.Count == 1, "All allocations for one vendor must use the same contract type.");
            var contractType = contractTypes.Single();
            var originalBase = group.Sum(x => CalculateLineAmount(x.Quantity,
                bid.Lines.Single(line => line.RfqLineId == x.RfqLineId).UnitPrice));
            var originalValue = bid.Subtotal == 0m ? bid.TotalOriginal :
                decimal.Round(bid.TotalOriginal * originalBase / bid.Subtotal, 4, MidpointRounding.AwayFromZero);
            var valueVnd = decimal.Round(originalValue * bid.ExchangeRateToVnd, 4, MidpointRounding.AwayFromZero);
            var contract = new Contract
            {
                ContractNumber = $"PO-{entity.Code}-{vendor.Id}",
                CustomerId = entity.OperationalProject.CustomerId,
                OperationalProjectId = projectId,
                Direction = ContractDirection.Downstream,
                Type = contractType,
                VendorId = vendor.Id,
                OwnerUserId = entity.OwnerUserId,
                Status = ContractStatus.Draft,
                Value = decimal.Round(valueVnd, 2, MidpointRounding.AwayFromZero),
                ScopeOfWork = entity.Title,
                Note = $"{bid.PaymentTerms}\n{bid.Currency} @ {bid.ExchangeRateToVnd:0.########} VND",
                CreatedByUserId = actor,
                UpdatedByUserId = actor,
            };
            var award = new RfqAward
            {
                Rfq = entity,
                RfqBid = bid,
                Vendor = vendor,
                Contract = contract,
                Currency = bid.Currency,
                ExchangeRateToVnd = bid.ExchangeRateToVnd,
                OriginalValue = originalValue,
                ValueVnd = valueVnd,
                AwardedAt = now,
                AwardedByUserId = actor,
            };
            foreach (var allocation in group)
            {
                var bidLine = bid.Lines.Single(x => x.RfqLineId == allocation.RfqLineId);
                var source = rfqLines[allocation.RfqLineId];
                var amountOriginal = CalculateLineAmount(allocation.Quantity, bidLine.UnitPrice);
                var amountVnd = decimal.Round(amountOriginal * bid.ExchangeRateToVnd, 4, MidpointRounding.AwayFromZero);
                var contractLine = new ContractLine
                {
                    Contract = contract,
                    ProjectBoqLineId = source.ProjectBoqLineId,
                    ProcurementOwnerUserId = entity.OwnerUserId,
                    Quantity = allocation.Quantity,
                    NegotiatedUnitPrice = decimal.Round(bidLine.UnitPrice * bid.ExchangeRateToVnd, 4, MidpointRounding.AwayFromZero),
                };
                db.ContractLines.Add(contractLine);
                award.Lines.Add(new RfqAwardLine
                {
                    RfqLine = source,
                    RfqBidLine = bidLine,
                    Quantity = allocation.Quantity,
                    UnitPrice = bidLine.UnitPrice,
                    AmountOriginal = amountOriginal,
                    AmountVnd = amountVnd,
                    MaterialRequestAllocations = allocation.MaterialRequestAllocations.Select(x =>
                        new RfqAwardMaterialRequestAllocation
                        { MaterialRequestLineId = x.MaterialRequestLineId, Quantity = x.Quantity }).ToList(),
                });
            }
            db.RfqAwards.Add(award);
            if (entity.Contract is null) { entity.Contract = contract; entity.SelectedBid = bid; }
        }
        entity.AwardedAt = now;
        entity.AwardedByUserId = actor;
        entity.AwardReason = request.Reason.Trim();
        entity.Status = RfqStatus.Awarded;
        Event(entity, "batch-awarded", actor, string.Join(" ", new[] { entity.AwardReason, Trim(request.OverrideReason) }.Where(x => x is not null)));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        await NotifyBestEffortAsync(entity, "awarded", ct);
        return await GetAsync(projectId, id, ct);
    }

    private async Task AcquireMaterialRequestAllocationLockAsync(int projectId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        var resource = $"rfq-mr-allocation:project:{projectId}";
        var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            EXEC {result} = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            """, ct);
        if ((int)result.Value < 0)
            throw new CrmConcurrencyException("Material Request allocation is busy. Reload and retry the award.");
    }

    public async Task<RfqDetailResponse?> AttachDocumentAsync(int projectId, int id, RfqAttachDocumentRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(ct);
        await EnsureMutableProjectAsync(projectId, ct);
        var entity = await LoadMutableAsync(projectId, id, request.RowVersion, ct);
        if (entity is null) return null;
        Require(entity.Status == RfqStatus.Draft, "RFQ package documents can only be attached to a draft.");
        var documents = await ValidateDocumentsAsync(projectId, [request.DocumentId], ct);
        Attach(entity, documents.Single(), null);
        Event(entity, "document-attached", actor);
        await SaveAsync(transaction, ct);
        return await GetAsync(projectId, id, ct);
    }

    private async Task<List<ProjectDocument>> ValidateDocumentsAsync(int projectId, List<long> ids, CancellationToken ct)
    {
        Require(ids.Count <= 20 && ids.Distinct().Count() == ids.Count, "Select at most 20 different quotation files.");
        var documents = await db.ProjectDocuments.Where(x => ids.Contains(x.Id) && x.OperationalProjectId == projectId &&
            x.Category == ProjectDocumentCategory.Procurement && x.DeletedAt == null && x.SourceEntityType == null &&
            x.DesiredOperation != ProjectDocumentDesiredOperation.Delete && x.SyncStatus != ProjectDocumentSyncStatus.Deleted &&
            !db.RfqDocuments.Any(link => link.ProjectDocumentId == x.Id)).ToListAsync(ct);
        Require(documents.Count == ids.Count, "Files must be unassigned Procurement documents of this project and available for use.");
        return documents;
    }

    private static void Attach(Rfq entity, ProjectDocument document, RfqBid? bid)
    {
        entity.Documents.Add(new RfqDocument { ProjectDocument = document, RfqBid = bid });
        document.SourceEntityType = "Rfq";
        document.SourceRecordId = entity.Id;
        document.SourceSlot = bid is null ? "package" : $"vendor-{bid.VendorId}-revision-{bid.Revision}";
    }

    public async Task NotifyOverdueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ids = await db.Rfqs.AsNoTracking().Where(x => x.OverdueNotifiedAt == null && x.DueAt < now &&
            (x.Status == RfqStatus.Issued || x.Status == RfqStatus.UnderEvaluation)).Select(x => x.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
        {
            await using var transaction = await BeginAsync(ct);
            var entity = await MutableRoots().Include(x => x.OperationalProject).SingleAsync(x => x.Id == id, ct);
            if (entity.OverdueNotifiedAt is not null || entity.Status is not (RfqStatus.Issued or RfqStatus.UnderEvaluation)) continue;
            entity.OverdueNotifiedAt = now;
            await CrmConcurrency.SaveChangesAsync(db, ct);
            await NotifyAsync(entity, "overdue", ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
    }

    private async Task NotifyAsync(Rfq entity, string action, CancellationToken ct)
    {
        var recipients = new[] { entity.OwnerUserId, entity.OperationalProject.ProjectManagerUserId ?? 0 }.Where(x => x > 0).Distinct().ToList();
        var active = await db.Users.Where(x => recipients.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        await notifications.NotifyManyFromTemplateAsync(active, $"procurement.rfq.{action}",
            new Dictionary<string, string> { ["code"] = entity.Code, ["title"] = entity.Title },
            "Rfq", entity.Id, $"/admin/procurement-control/rfqs?projectId={entity.OperationalProjectId}&rfqId={entity.Id}");
    }

    private async Task NotifyBestEffortAsync(Rfq entity, string action, CancellationToken ct)
    {
        try
        {
            await NotifyAsync(entity, action, ct);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "RFQ {RfqId} {Action} committed but notification delivery failed.", entity.Id, action);
        }
    }

    private async Task EnsureMutableProjectAsync(int projectId, CancellationToken ct)
    {
        var status = await db.OperationalProjects.Where(x => x.Id == projectId).Select(x => x.Status).SingleAsync(ct);
        Require(status is not (OperationalProjectStatus.Completed or OperationalProjectStatus.Cancelled), "Completed or cancelled projects cannot be changed.");
    }

    private async Task<Rfq?> LoadMutableAsync(int projectId, int id, string? token, CancellationToken ct)
    {
        // A root update lock serializes changes to one RFQ, but Serializable
        // collection scans can still share an empty index range with another
        // RFQ. Both writers then deadlock while converting that shared range
        // for a child insert. Lock owned children for update in one fixed order,
        // without weakening the isolation of external eligibility dependencies.
        IQueryable<Rfq> query = db.Database.IsSqlServer()
            ? MutableRoots()
                .Include(x => x.OperationalProject).ThenInclude(x => x.Customer)
                .Include(x => x.SourceBoqRevision).Include(x => x.Owner)
                .Include(x => x.Contract).Include(x => x.AwardedBy)
            : Query(forUpdate: true);
        var entity = await query.SingleOrDefaultAsync(x => x.Id == id && x.OperationalProjectId == projectId, ct);
        if (entity is not null)
        {
            CrmConcurrency.EnsureMatches(entity.RowVersion, token);
            CrmConcurrency.Apply(db, entity, token);
            if (db.Database.IsSqlServer()) await LoadMutableChildrenAsync(id, ct);
        }
        return entity;
    }

    private async Task LoadMutableChildrenAsync(int id, CancellationToken ct)
    {
        // Tracking fixup fills the root collections and bid-line relationships.
        // Explicit predicates avoid rejoining/locking unrelated RFQ roots in
        // EF's split Includes. Keep this order consistent for every mutation.
        await db.Set<RfqLine>().FromSqlInterpolated(
            $"SELECT * FROM [rfq_lines] WITH (UPDLOCK) WHERE [RfqId] = {id}")
            .LoadAsync(ct);
        await db.RfqInvitations.FromSqlInterpolated(
            $"SELECT * FROM [rfq_invitations] WITH (UPDLOCK) WHERE [RfqId] = {id}")
            .Include(x => x.Vendor).LoadAsync(ct);
        await db.RfqBids.FromSqlInterpolated(
            $"SELECT * FROM [rfq_bids] WITH (UPDLOCK) WHERE [RfqId] = {id}")
            .Include(x => x.SubmittedBy).LoadAsync(ct);
        await db.Set<RfqBidLine>().FromSqlInterpolated($"""
            SELECT line.* FROM [rfq_bid_lines] AS line WITH (UPDLOCK)
            INNER JOIN [rfq_bids] AS bid WITH (UPDLOCK) ON bid.[Id] = line.[RfqBidId]
            WHERE bid.[RfqId] = {id}
            """).LoadAsync(ct);
        await db.Set<RfqEvent>().FromSqlInterpolated(
            $"SELECT * FROM [rfq_events] WITH (UPDLOCK) WHERE [RfqId] = {id}")
            .Include(x => x.Actor).LoadAsync(ct);
        await db.RfqDocuments.FromSqlInterpolated(
            $"SELECT * FROM [rfq_documents] WITH (UPDLOCK) WHERE [RfqId] = {id}")
            .Include(x => x.ProjectDocument).LoadAsync(ct);
    }

    private async Task<IDbContextTransaction?> BeginAsync(CancellationToken ct, bool creating = false)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!db.Database.IsSqlServer()) return transaction;
        try
        {
            // EF inserts a new aggregate's children in dependency order, which
            // differs from the existing-aggregate read-lock order. Creation must
            // not interleave those inserts with another RFQ's empty-range reads.
            // Only creation takes Exclusive; mutations (including overdue jobs)
            // share this gate and retain their per-root/child update locks.
            // Acquire before any database reads, and release with the transaction.
            var resource = "rfqs:creation-and-mutation";
            var mode = creating ? "Exclusive" : "Shared";
            var result = new SqlParameter("@lockResult", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output,
            };
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC {result} = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = {mode},
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000;
                """, ct);
            if ((int)result.Value < 0)
                throw new CrmConcurrencyException("RFQ creation is busy. Reload and retry the operation.");
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
    private async Task SaveAsync(IDbContextTransaction? transaction, CancellationToken ct)
    {
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }
    internal static decimal CalculateLineAmount(decimal quantity, decimal unitPrice)
    {
        var amount = decimal.Round(quantity * unitPrice, 4, MidpointRounding.AwayFromZero);
        Require(amount <= 99999999999999.9999m, "Quoted line amount exceeds the supported amount (99,999,999,999,999.9999 VND).");
        return amount;
    }

    internal static (decimal DiscountAmount, decimal TotalOriginal, decimal TotalVnd) CalculateCommercialTotals(
        decimal subtotal, decimal freight, decimal discountPercent, decimal discountAmount,
        decimal vatPercent, decimal exchangeRateToVnd)
    {
        Require(subtotal >= 0 && freight >= 0 && discountPercent is >= 0 and <= 100 &&
            discountAmount >= 0 && vatPercent is >= 0 and <= 100 && exchangeRateToVnd > 0,
            "Commercial values and exchange rate are outside the supported range.");
        Require(discountPercent == 0 || discountAmount == 0,
            "Use either a discount percent or a discount amount, not both.");
        var appliedDiscount = discountPercent > 0
            ? decimal.Round(subtotal * discountPercent / 100m, 4, MidpointRounding.AwayFromZero)
            : discountAmount;
        var taxable = subtotal + freight - appliedDiscount;
        Require(taxable >= 0, "Discount cannot exceed subtotal plus freight.");
        var vat = decimal.Round(taxable * vatPercent / 100m, 4, MidpointRounding.AwayFromZero);
        var original = decimal.Round(taxable + vat, 4, MidpointRounding.AwayFromZero);
        var vnd = decimal.Round(original * exchangeRateToVnd, 4, MidpointRounding.AwayFromZero);
        Require(original <= 99999999999999.9999m && vnd <= 99999999999999.9999m,
            "Commercial total exceeds the supported amount.");
        return (appliedDiscount, original, vnd);
    }

    private async Task<Dictionary<int, decimal>> VendorRatingsAsync(Rfq entity, CancellationToken ct)
    {
        var vendorIds = entity.Invitations.Select(x => x.VendorId).ToList();
        return await db.VendorRatings.AsNoTracking()
            .Where(x => vendorIds.Contains(x.VendorId) && x.Status == VendorRatingStatus.Approved)
            .GroupBy(x => x.VendorId)
            .Select(group => new { VendorId = group.Key, Score = group.Average(x => x.OverallScore) })
            .ToDictionaryAsync(x => x.VendorId, x => x.Score, ct);
    }

    private async Task<IReadOnlyList<RfqMaterialRequestOptionResponse>> MaterialRequestOptionsAsync(
        Rfq entity, CancellationToken ct)
    {
        var projectBoqLineIds = entity.Lines.Select(x => x.ProjectBoqLineId).ToList();
        var lines = await db.MaterialRequestLines.AsNoTracking().Include(x => x.MaterialRequest)
            .Where(x => projectBoqLineIds.Contains(x.ProjectBoqLineId) &&
                x.MaterialRequest.OperationalProjectId == entity.OperationalProjectId &&
                (x.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                 x.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled))
            .OrderBy(x => x.MaterialRequest.RequiredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var ids = lines.Select(x => x.Id).ToList();
        var allocated = await db.RfqAwardMaterialRequestAllocations.AsNoTracking()
            .Where(x => ids.Contains(x.MaterialRequestLineId))
            .GroupBy(x => x.MaterialRequestLineId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        return lines.Select(x => new RfqMaterialRequestOptionResponse(x.Id, x.MaterialRequestId,
            x.MaterialRequest.Code, x.ProjectBoqLineId, x.RequestedQuantity,
            allocated.GetValueOrDefault(x.Id), x.RequestedQuantity - allocated.GetValueOrDefault(x.Id))).ToList();
    }

    private sealed record ScoreComponents(decimal? Price, decimal? LeadTime, decimal? VendorRating,
        decimal? Commercial, decimal? Weighted);

    private static Dictionary<(int BidId, int RfqLineId), ScoreComponents> CalculateLineScores(Rfq entity,
        IReadOnlyDictionary<int, decimal>? vendorRatings)
    {
        var current = CurrentBids(entity).ToList();
        var active = current.Where(x => x.WithdrawnAt is null && x.ValidUntil >= DateTime.UtcNow &&
            entity.Invitations.Any(invitation => invitation.VendorId == x.VendorId && invitation.Vendor.IsActive)).ToList();
        var result = new Dictionary<(int BidId, int RfqLineId), ScoreComponents>();
        foreach (var line in entity.Lines)
        {
            var quoted = active.Where(bid => bid.Lines.Any(value => value.RfqLineId == line.Id)).ToList();
            var lowest = quoted.Select(bid => bid.Lines.Single(value => value.RfqLineId == line.Id).UnitPrice * bid.ExchangeRateToVnd)
                .Select(value => (decimal?)value).Min();
            var minimumLeadTime = quoted.Select(bid => (int?)bid.LeadTimeDays).Min();
            foreach (var bid in current)
            {
                var bidLine = bid.Lines.SingleOrDefault(value => value.RfqLineId == line.Id);
                if (bidLine is null || !quoted.Contains(bid) || entity.CommercialWeight > 0 && !bid.CommercialScore.HasValue)
                {
                    result[(bid.Id, line.Id)] = new(null, null, null, bid.CommercialScore, null);
                    continue;
                }
                var convertedUnitPrice = bidLine.UnitPrice * bid.ExchangeRateToVnd;
                var price = lowest == 0 ? convertedUnitPrice == 0 ? 100m : 0m :
                    decimal.Round(lowest!.Value / convertedUnitPrice * 100m, 2);
                var lead = minimumLeadTime == 0 ? bid.LeadTimeDays == 0 ? 100m : 0m :
                    decimal.Round((decimal)minimumLeadTime!.Value / bid.LeadTimeDays * 100m, 2);
                var rating = vendorRatings?.GetValueOrDefault(bid.VendorId) ?? 50m;
                var weighted = decimal.Round((price * entity.PriceWeight + lead * entity.LeadTimeWeight +
                    rating * entity.VendorRatingWeight + (bid.CommercialScore ?? 0m) * entity.CommercialWeight) / 100m,
                    2, MidpointRounding.AwayFromZero);
                result[(bid.Id, line.Id)] = new(price, lead, rating, bid.CommercialScore, weighted);
            }
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ProcurementOperationException(message);
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Event(Rfq entity, string action, int actor, string? reason = null, string? actorLabel = null)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Events.Add(new RfqEvent { Action = action, ActorUserId = actor, ActorLabel = actorLabel, Reason = reason });
    }
    private static IEnumerable<RfqBid> CurrentBids(Rfq entity) => entity.Bids.GroupBy(x => x.VendorId).Select(x => x.MaxBy(b => b.Revision)!);
    private static bool Eligible(Rfq entity, RfqBid bid) => bid.WithdrawnAt is null && bid.ValidUntil >= DateTime.UtcNow &&
        bid.Lines.Count == entity.Lines.Count && entity.Invitations.Any(x => x.VendorId == bid.VendorId && x.Vendor.IsActive);

    private static RfqDetailResponse Map(Rfq entity, IReadOnlyDictionary<int, decimal>? vendorRatings = null,
        IReadOnlyList<RfqMaterialRequestOptionResponse>? materialRequests = null)
    {
        var current = CurrentBids(entity).ToList();
        var eligible = current.Where(x => Eligible(entity, x)).ToList();
        var lowest = eligible.Select(x => (decimal?)x.Total).Min();
        var lineScores = CalculateLineScores(entity, vendorRatings);
        var scores = current.ToDictionary(bid => bid.Id, bid =>
        {
            var values = bid.Lines.Select(line => lineScores.GetValueOrDefault((bid.Id, line.RfqLineId)))
                .Where(value => value?.Weighted.HasValue == true).Select(value => value!).ToList();
            return values.Count == bid.Lines.Count && values.Count > 0 ? new ScoreComponents(
                decimal.Round(values.Average(value => value.Price!.Value), 2, MidpointRounding.AwayFromZero),
                decimal.Round(values.Average(value => value.LeadTime!.Value), 2, MidpointRounding.AwayFromZero),
                decimal.Round(values.Average(value => value.VendorRating!.Value), 2, MidpointRounding.AwayFromZero),
                bid.CommercialScore,
                decimal.Round(values.Average(value => value.Weighted!.Value), 2, MidpointRounding.AwayFromZero))
                : new ScoreComponents(null, null, null, bid.CommercialScore, null);
        });
        var header = new RfqListItemResponse(entity.Id, entity.OperationalProjectId, entity.Code, entity.Title,
            entity.OperationalProject.Code, entity.OperationalProject.Name, entity.OperationalProject.Customer.Name,
            entity.SourceBoqRevisionId, entity.SourceBoqRevision.RevisionNumber, entity.Status, entity.OwnerUserId, entity.Owner.FullName ?? entity.Owner.PhoneNumber,
            entity.Invitations.Count, current.Count(x => x.WithdrawnAt is null), Utc(entity.IssuedAt), Utc(entity.DueAt), Utc(entity.UpdatedAt),
            entity.DueAt < DateTime.UtcNow && entity.Status is RfqStatus.Issued or RfqStatus.UnderEvaluation, CrmConcurrency.Encode(entity.RowVersion));
        var validLines = current.Where(x => x.WithdrawnAt is null && x.ValidUntil >= DateTime.UtcNow &&
            entity.Invitations.Any(v => v.VendorId == x.VendorId && v.Vendor.IsActive)).SelectMany(x => x.Lines).ToList();
        return new(header, entity.Currency, entity.Note,
            entity.Lines.OrderBy(x => x.Id).Select(x => new RfqLineResponse(x.Id, x.ProjectBoqLineId, x.ItemCode, x.Description, x.Unit,
                x.Quantity, x.BudgetUnitPrice, validLines.Where(l => l.RfqLineId == x.Id).Select(l => (decimal?)(l.UnitPrice * l.RfqBid.ExchangeRateToVnd)).Min())).ToList(),
            entity.Invitations.OrderBy(x => x.Id).Select(x => new RfqVendorResponse(x.VendorId, x.VendorName,
                x.Vendor.VendorType, x.Vendor.IsActive, x.PortalTokenHash != null, Utc(x.InvitationSentAt),
                x.InvitationDeliveryError)).ToList(),
            entity.Bids.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).Select(x => new RfqBidResponse(x.Id, x.VendorId, x.Revision,
                x.LeadTimeDays, x.PaymentTerms, Utc(x.ValidUntil), x.Note, Utc(x.SubmittedAt),
                x.SubmittedViaPortal
                    ? $"{entity.Invitations.Single(invitation => invitation.VendorId == x.VendorId).VendorName} (vendor portal)"
                    : x.SubmittedBy.FullName ?? x.SubmittedBy.PhoneNumber,
                Utc(x.WithdrawnAt),
                current.Contains(x), x.Lines.Count == entity.Lines.Count, eligible.Contains(x), eligible.Contains(x) && x.Total == lowest,
                x.Total, x.Lines.Select(l => new RfqBidLineResponse(l.RfqLineId, l.UnitPrice, l.Amount,
                    decimal.Round(l.UnitPrice * x.ExchangeRateToVnd, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(l.Amount * x.ExchangeRateToVnd, 4, MidpointRounding.AwayFromZero),
                    lineScores.GetValueOrDefault((x.Id, l.RfqLineId))?.Price,
                    lineScores.GetValueOrDefault((x.Id, l.RfqLineId))?.LeadTime,
                    lineScores.GetValueOrDefault((x.Id, l.RfqLineId))?.VendorRating,
                    lineScores.GetValueOrDefault((x.Id, l.RfqLineId))?.Weighted)).ToList(),
                x.Currency, x.ExchangeRateToVnd, x.Subtotal, x.FreightAmount, x.DiscountPercent,
                x.DiscountAmount, x.VatPercent, x.TotalOriginal, scores.GetValueOrDefault(x.Id)?.Price,
                scores.GetValueOrDefault(x.Id)?.LeadTime, scores.GetValueOrDefault(x.Id)?.VendorRating,
                x.CommercialScore, scores.GetValueOrDefault(x.Id)?.Weighted,
                x.EvaluationNote, x.SubmittedViaPortal)).ToList(),
            entity.Events.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Select(x => new RfqEventResponse(x.Action,
                x.ActorLabel ?? x.Actor.FullName ?? x.Actor.PhoneNumber, Utc(x.At), x.Reason)).ToList(),
            entity.Documents.Select(x => new RfqFileResponse(x.ProjectDocumentId, x.RfqBidId, x.ProjectDocument.OriginalFileName)).ToList(),
            entity.SelectedBidId, entity.ContractId, entity.Contract?.ContractNumber, Utc(entity.AwardedAt), entity.AwardedBy?.FullName,
            entity.AwardReason, entity.AwardSnapshotJson,
            new RfqScoringResponse(entity.PriceWeight, entity.LeadTimeWeight,
                entity.VendorRatingWeight, entity.CommercialWeight),
            entity.Awards.OrderBy(x => x.Id).Select(x => new RfqAwardResponse(x.Id, x.RfqBidId,
                x.VendorId, x.Vendor.CompanyName, x.ContractId, x.Contract.ContractNumber, x.Currency,
                x.ExchangeRateToVnd, x.OriginalValue, x.ValueVnd, Utc(x.AwardedAt),
                x.Lines.Select(line => new RfqAwardLineResponse(line.RfqLineId, line.Quantity,
                    line.UnitPrice, line.AmountOriginal, line.AmountVnd,
                    line.MaterialRequestAllocations.Select(allocation =>
                        new RfqMaterialRequestAllocationResponse(allocation.MaterialRequestLineId,
                            allocation.MaterialRequestLine.MaterialRequest.Code, allocation.Quantity)).ToList())).ToList())).ToList(),
            materialRequests ?? []);
    }
}
