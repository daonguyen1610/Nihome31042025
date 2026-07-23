using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>
/// Populates a small, deterministic set of sample rows for the CRM features
/// (Leads + Customers + primary contact + one activity each) so a freshly
/// migrated database has data to demo bulk-select, filters and detail
/// dialogs without hand-crafting curl calls. Idempotent: every insert is
/// guarded so re-running is a no-op.
/// </summary>
public static class SampleCrmDataSeeder
{
    private const string SampleTag = "[SAMPLE]";

    public static void Seed(AppDbContext db, string? webRootPath = null)
    {
        var now = DateTime.UtcNow;

        // Owner defaults to the SALE test user seeded by DbSeeder, so the
        // sample data plays well with the RBAC ownership scoping tested in
        // integration + e2e specs. If SALE is missing (e.g. a partial seed),
        // fall back to the first admin.
        var owner = db.Users.FirstOrDefault(u => u.PhoneNumber == "0911000003")
                    ?? db.Users.FirstOrDefault(u => u.Role == UserRole.SUPER_ADMIN)
                    ?? db.Users.FirstOrDefault();
        if (owner is null) return;

        SeedLeads(db, owner, now);
        SeedCustomers(db, owner, now);
        SeedOpportunities(db, owner, now);
        SeedQuotes(db, owner, now);
        SeedContracts(db, owner, now, webRootPath);
        SeedCapabilityDocuments(db, owner, now, webRootPath);
        SeedTenders(db, owner, now);
        SeedSurveys(db, owner, now);
        SeedDesignProjects(db, owner, now);
        SeedPermitChecklists(db, owner, now);
        SeedConceptOptions(db, owner, now);
        SeedBasicDesignDocs(db, owner, now);
        SeedShopDrawings(db, owner, now);
        SeedDrawingRevisions(db, owner, now);
        SeedIfcReleases(db, owner, now);
        SeedConstructionTasks(db, owner, now);
        SeedSiteDiaries(db, owner, now);
        SeedPunchItems(db, owner, now);
        SeedAcceptanceRecords(db, owner, now);
        SeedAsBuiltDocuments(db, owner, now);
    }

    private static void SeedLeads(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new[]
        {
            new Lead
            {
                Name = $"{SampleTag} Nguyễn Minh Anh",
                Phone = "0900000101",
                Email = "minh.anh.sample@example.com",
                SourceCode = "marketing",
                Status = LeadStatus.New,
                Note = "Lead mẫu từ chiến dịch Facebook Ads.",
            },
            new Lead
            {
                Name = $"{SampleTag} Trần Thị Bích",
                CompanyName = "Công ty TNHH Bích Anh",
                Phone = "0900000102",
                Email = "bich.anh.sample@example.com",
                SourceCode = "referral",
                Status = LeadStatus.Contacted,
                Note = "Lead mẫu do khách hàng cũ giới thiệu.",
            },
            new Lead
            {
                Name = $"{SampleTag} Phạm Quốc Cường",
                Phone = "0900000103",
                SourceCode = "website",
                Status = LeadStatus.Interested,
                Note = "Đã liên hệ qua form website, quan tâm gói cao cấp.",
            },
            new Lead
            {
                Name = $"{SampleTag} Lê Hồng Duyên",
                Email = "duyen.sample@example.com",
                SourceCode = "event",
                Status = LeadStatus.New,
                Note = "Ghi nhận tại sự kiện triển lãm nội thất tháng 10.",
            },
            new Lead
            {
                Name = $"{SampleTag} Đỗ Thanh Ê",
                Phone = "0900000105",
                SourceCode = "cold-call",
                Status = LeadStatus.NotInterested,
                Note = "Chưa có nhu cầu, hẹn liên hệ lại quý sau.",
            },
        };

        foreach (var s in samples)
        {
            var exists = db.Leads.Any(l => l.Name == s.Name);
            if (exists) continue;

            s.OwnerUserId = owner.Id;
            s.CreatedByUserId = owner.Id;
            s.UpdatedByUserId = owner.Id;
            s.CreatedAt = now;
            s.UpdatedAt = now;

            db.Leads.Add(s);
            db.SaveChanges();

            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = s.Id,
                Type = LeadActivityType.Note,
                Content = "Ghi chú mẫu khởi tạo cho lead demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });
            db.SaveChanges();
        }
    }

    private static void SeedCustomers(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new List<(Customer Customer, CustomerContact Contact)>
        {
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Nguyễn Văn An",
                    SourceCode = "marketing",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng cá nhân mẫu — kênh Marketing.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Văn An",
                    Phone = "0900000201",
                    Email = "an.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Trần Thị Bảo",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng cá nhân đang trong quá trình tư vấn.",
                },
                new CustomerContact
                {
                    FullName = "Trần Thị Bảo",
                    Phone = "0900000202",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Xây dựng Alpha",
                    TaxId = "0100000001",
                    Address = "Số 1, Đường Alpha, Q.1, TP.HCM",
                    RepresentativeName = "Ông Nguyễn Văn Alpha",
                    SourceCode = "website",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng doanh nghiệp mẫu — mảng B2B.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Thị Anh",
                    Position = "Trưởng phòng Mua hàng",
                    Phone = "0900000301",
                    Email = "purchase.alpha.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty TNHH Beta Interior",
                    TaxId = "0100000002",
                    Address = "Số 2, Đường Beta, Q.3, TP.HCM",
                    RepresentativeName = "Bà Lê Thị Beta",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.Signed,
                    Note = "Khách hàng đã ký hợp đồng — dùng làm demo cho tab hợp đồng.",
                },
                new CustomerContact
                {
                    FullName = "Phạm Quốc Bảo",
                    Position = "Giám đốc dự án",
                    Phone = "0900000302",
                    Email = "pm.beta.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Gamma Home",
                    TaxId = "0100000003",
                    Address = "Số 3, Đường Gamma, Q.7, TP.HCM",
                    RepresentativeName = "Ông Trần Văn Gamma",
                    SourceCode = "event",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng gặp tại sự kiện triển lãm.",
                },
                new CustomerContact
                {
                    FullName = "Lê Hồng Cường",
                    Position = "Kế toán trưởng",
                    Phone = "0900000303",
                    IsPrimary = true,
                }
            ),
        };

        foreach (var (customer, contact) in samples)
        {
            if (db.Customers.Any(c => c.Name == customer.Name)) continue;

            customer.OwnerUserId = owner.Id;
            customer.CreatedByUserId = owner.Id;
            customer.UpdatedByUserId = owner.Id;
            customer.CreatedAt = now;
            customer.UpdatedAt = now;

            db.Customers.Add(customer);
            db.SaveChanges();

            contact.CustomerId = customer.Id;
            contact.CreatedAt = now;
            contact.UpdatedAt = now;
            db.CustomerContacts.Add(contact);

            db.CustomerActivities.Add(new CustomerActivity
            {
                CustomerId = customer.Id,
                Type = CustomerActivityType.Note,
                OccurredAt = now,
                Content = "Ghi chú mẫu khởi tạo cho khách hàng demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });

            db.SaveChanges();
        }
    }

    private static void SeedOpportunities(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var sampleCustomers = db.Customers
            .Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id)
            .ToList();
        if (sampleCustomers.Count == 0) return;

        var samples = new (string Name, decimal Value, int Probability, OpportunityStage Stage, int CloseDaysFromNow)[]
        {
            ($"{SampleTag} Nhà máy Alpha - Giai đoạn 1", 4_500_000_000m, 60, OpportunityStage.Qualification, 45),
            ($"{SampleTag} Beta Interior - Showroom Q3", 2_100_000_000m, 35, OpportunityStage.Prospecting, 90),
            ($"{SampleTag} Gamma Home - Cải tạo văn phòng", 1_250_000_000m, 75, OpportunityStage.Proposal, 30),
            ($"{SampleTag} Beta Interior - Nhà xưởng phụ", 3_800_000_000m, 55, OpportunityStage.Negotiation, 60),
            ($"{SampleTag} Alpha - Mở rộng kho", 800_000_000m, 100, OpportunityStage.Won, -10),
            ($"{SampleTag} Trần Thị Bảo - Nhà phố", 350_000_000m, 0, OpportunityStage.Lost, -20),
        };

        for (var i = 0; i < samples.Length; i++)
        {
            var (name, value, probability, stage, closeDays) = samples[i];
            if (db.Opportunities.Any(o => o.Name == name)) continue;

            var customer = sampleCustomers[i % sampleCustomers.Count];
            var closeDate = now.AddDays(closeDays);

            var op = new Opportunity
            {
                Name = name,
                CustomerId = customer.Id,
                OwnerUserId = owner.Id,
                EstimatedValue = value,
                WinProbability = probability,
                ExpectedCloseDate = closeDate,
                Stage = stage,
                ClosedAt = stage is OpportunityStage.Won or OpportunityStage.Lost ? closeDate : null,
                LostReasonCode = stage == OpportunityStage.Lost ? "price" : null,
                LostNote = stage == OpportunityStage.Lost ? "Khách hàng cân nhắc lại vì ngân sách." : null,
                Note = "Cơ hội mẫu — demo pipeline & stage transition.",
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            };
            db.Opportunities.Add(op);
            db.SaveChanges();

            db.OpportunityActivities.Add(new OpportunityActivity
            {
                OpportunityId = op.Id,
                Type = OpportunityActivityType.Note,
                OccurredAt = now,
                Content = "Ghi chú mẫu khởi tạo cho cơ hội demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });
            db.SaveChanges();
        }
    }

    private const string SampleQuoteNoteMarker = "[SAMPLE_QUOTE]";

    private static void SeedQuotes(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        // Guard: seeder-managed quotes carry a canonical marker in Note. If
        // any exist we treat the module as already seeded and bail — that way
        // subsequent boot cycles are a no-op while still letting real users
        // author their own quotes freely.
        if (db.Quotes.Any(q => q.Note != null && q.Note.StartsWith(SampleQuoteNoteMarker))) return;

        var sampleOpps = db.Opportunities
            .Where(o => o.Name.StartsWith(SampleTag))
            .OrderBy(o => o.Id)
            .ToList();
        if (sampleOpps.Count == 0) return;

        // One curated sample per QuoteStatus so every filter/badge/workflow
        // branch has real data to render (plus a versioned pair to exercise
        // the Versions tab and QuoteVersionSnapshot table).
        var seeds = new (int OppIdx, QuoteMethod Method, QuoteStatus Status, int ValidDays, string Label)[]
        {
            (0, QuoteMethod.UnitCost, QuoteStatus.Draft,             45, "Nháp · Suất đầu tư"),
            (1, QuoteMethod.Boq,      QuoteStatus.Draft,             45, "Nháp · BOQ"),
            (2, QuoteMethod.Boq,      QuoteStatus.PendingApproval,   30, "Chờ duyệt nội bộ"),
            (3, QuoteMethod.UnitCost, QuoteStatus.Approved,          30, "Đã duyệt · sẵn gửi khách"),
            (4, QuoteMethod.Boq,      QuoteStatus.SentToCustomer,     2, "Đã gửi khách · sắp hết hạn"),
            (0, QuoteMethod.UnitCost, QuoteStatus.CustomerApproved,  60, "Khách đã duyệt · terminal"),
            (1, QuoteMethod.Boq,      QuoteStatus.Rejected,          30, "Khách từ chối · terminal"),
            (2, QuoteMethod.UnitCost, QuoteStatus.Expired,           -5, "Hết hạn · quá hạn 5 ngày"),
            (3, QuoteMethod.Boq,      QuoteStatus.Cancelled,         30, "Đã huỷ · terminal"),
        };

        var year = now.Year;
        var nextSeq = 1 + (db.Quotes.AsNoTracking()
            .Where(q => q.Code.StartsWith($"QT-{year}-"))
            .Select(q => q.Code)
            .AsEnumerable()
            .Select(c => int.TryParse(c.AsSpan($"QT-{year}-".Length), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max());

        var lastCreatedId = 0;
        for (var i = 0; i < seeds.Length; i++)
        {
            var (oppIdx, method, status, validDays, label) = seeds[i];
            if (oppIdx >= sampleOpps.Count) continue;

            var opp = sampleOpps[oppIdx];
            var code = $"QT-{year}-{nextSeq++:D4}";
            var validUntil = now.AddDays(validDays);

            var quote = new Quote
            {
                Code = code,
                OpportunityId = opp.Id,
                OwnerUserId = opp.OwnerUserId,
                Method = method,
                Version = 1,
                DiscountPercent = i % 3 == 0 ? 0m : (i % 3 == 1 ? 5m : 10m),
                VatPercent = method == QuoteMethod.Boq ? 10m : 8m,
                ValidUntil = validUntil,
                Note = $"{SampleQuoteNoteMarker} {label}",
                Status = status,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddHours(-1),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            };

            if (method == QuoteMethod.UnitCost)
            {
                quote.AreaSqm = 100m + i * 20m;
                quote.UnitPricePerSqm = 6_500_000m + i * 800_000m;
                quote.PackageDescription = "Gói mẫu bao gồm thi công phần thô + hoàn thiện cơ bản.";
            }
            else
            {
                quote.Items = new List<QuoteItem>
                {
                    NewItem("BOQ-001", "Bê tông móng M300",       "m3", 25m,    1_450_000m, 1),
                    NewItem("BOQ-002", "Cốt thép D16",             "kg", 1_200m,   24_500m, 2),
                    NewItem("BOQ-003", "Ván khuôn thép",           "m2", 180m,    85_000m, 3),
                    NewItem("BOQ-004", "Sơn Dulux nội thất",       "m2", 350m,    75_000m, 4),
                    NewItem("BOQ-005", "Cửa nhôm kính Xingfa",     "bộ", 8m,   5_500_000m, 5),
                };
            }

            RecomputeTotals(quote);
            StampWorkflowTimestamps(quote, owner.Id, now);

            db.Quotes.Add(quote);
            db.SaveChanges();
            lastCreatedId = quote.Id;

            WriteApprovalLogs(db, quote, owner.Id, now);
            db.SaveChanges();
        }

        // Bonus: give the LAST seeded quote a V2 snapshot so the /versions
        // tab and diff have something to render out of the box.
        if (lastCreatedId > 0)
        {
            AttachVersionSnapshot(db, lastCreatedId, owner.Id, now);
        }
    }

    /// <summary>
    /// Sets Submitted/Approved/Sent/Closed timestamps consistent with the
    /// seeded <see cref="QuoteStatus"/> so timeline UI reads sensibly.
    /// </summary>
    private static void StampWorkflowTimestamps(Quote q, int userId, DateTime now)
    {
        switch (q.Status)
        {
            case QuoteStatus.PendingApproval:
                q.SubmittedAt = now.AddHours(-3);
                q.SubmittedByUserId = userId;
                break;
            case QuoteStatus.Approved:
                q.SubmittedAt = now.AddDays(-1);
                q.SubmittedByUserId = userId;
                q.ApprovedAt = now.AddHours(-5);
                q.ApprovedByUserId = userId;
                break;
            case QuoteStatus.SentToCustomer:
                q.SubmittedAt = now.AddDays(-2);
                q.SubmittedByUserId = userId;
                q.ApprovedAt = now.AddDays(-1);
                q.ApprovedByUserId = userId;
                q.SentAt = now.AddHours(-4);
                q.SentByUserId = userId;
                break;
            case QuoteStatus.CustomerApproved:
                q.SubmittedAt = now.AddDays(-4);
                q.SubmittedByUserId = userId;
                q.ApprovedAt = now.AddDays(-3);
                q.ApprovedByUserId = userId;
                q.SentAt = now.AddDays(-2);
                q.SentByUserId = userId;
                q.ClosedAt = now.AddHours(-2);
                break;
            case QuoteStatus.Rejected:
                q.SubmittedAt = now.AddDays(-5);
                q.SubmittedByUserId = userId;
                q.ApprovedAt = now.AddDays(-4);
                q.ApprovedByUserId = userId;
                q.SentAt = now.AddDays(-3);
                q.SentByUserId = userId;
                q.ClosedAt = now.AddDays(-1);
                break;
            case QuoteStatus.Expired:
                q.SubmittedAt = now.AddDays(-15);
                q.SubmittedByUserId = userId;
                q.ApprovedAt = now.AddDays(-14);
                q.ApprovedByUserId = userId;
                q.SentAt = now.AddDays(-12);
                q.SentByUserId = userId;
                break;
            case QuoteStatus.Cancelled:
                q.ClosedAt = now.AddHours(-6);
                break;
        }
    }

    /// <summary>Emits a Create log plus every intermediate transition matching the final status.</summary>
    private static void WriteApprovalLogs(AppDbContext db, Quote quote, int userId, DateTime now)
    {
        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quote.Id,
            Action = QuoteWorkflowAction.Create,
            FromStatus = null,
            ToStatus = QuoteStatus.Draft,
            ByUserId = userId,
            Note = "Seed mẫu.",
            CreatedAt = quote.CreatedAt,
        });

        // For every status we traversed, emit the matching action log.
        var chain = quote.Status switch
        {
            QuoteStatus.PendingApproval => new[] { QuoteWorkflowAction.Submit },
            QuoteStatus.Approved => new[] { QuoteWorkflowAction.Submit, QuoteWorkflowAction.Approve },
            QuoteStatus.SentToCustomer => new[] { QuoteWorkflowAction.Submit, QuoteWorkflowAction.Approve, QuoteWorkflowAction.Send },
            QuoteStatus.CustomerApproved => new[] { QuoteWorkflowAction.Submit, QuoteWorkflowAction.Approve, QuoteWorkflowAction.Send, QuoteWorkflowAction.CustomerApprove },
            QuoteStatus.Rejected => new[] { QuoteWorkflowAction.Submit, QuoteWorkflowAction.Approve, QuoteWorkflowAction.Send, QuoteWorkflowAction.CustomerReject },
            QuoteStatus.Expired => new[] { QuoteWorkflowAction.Submit, QuoteWorkflowAction.Approve, QuoteWorkflowAction.Send },
            QuoteStatus.Cancelled => new[] { QuoteWorkflowAction.Cancel },
            _ => Array.Empty<QuoteWorkflowAction>(),
        };
        var progress = QuoteStatus.Draft;
        var stamp = quote.CreatedAt;
        foreach (var action in chain)
        {
            var (to, at) = action switch
            {
                QuoteWorkflowAction.Submit => (QuoteStatus.PendingApproval, quote.SubmittedAt ?? stamp.AddMinutes(5)),
                QuoteWorkflowAction.Approve => (QuoteStatus.Approved, quote.ApprovedAt ?? stamp.AddMinutes(10)),
                QuoteWorkflowAction.Send => (QuoteStatus.SentToCustomer, quote.SentAt ?? stamp.AddMinutes(15)),
                QuoteWorkflowAction.CustomerApprove => (QuoteStatus.CustomerApproved, quote.ClosedAt ?? stamp.AddMinutes(20)),
                QuoteWorkflowAction.CustomerReject => (QuoteStatus.Rejected, quote.ClosedAt ?? stamp.AddMinutes(20)),
                QuoteWorkflowAction.Cancel => (QuoteStatus.Cancelled, quote.ClosedAt ?? stamp.AddMinutes(20)),
                _ => (progress, stamp),
            };
            AppendLog(db, quote.Id, action, progress, to, userId, at, action == QuoteWorkflowAction.CustomerReject ? "Khách yêu cầu giảm giá." : null);
            progress = to;
            stamp = at;
        }
        _ = now;
    }

    /// <summary>
    /// Snapshots the current quote into <see cref="QuoteVersionSnapshot"/>
    /// and bumps Version to 2 so the Versions tab has V1 (frozen) + V2
    /// (current) to compare.
    /// </summary>
    private static void AttachVersionSnapshot(AppDbContext db, int quoteId, int userId, DateTime now)
    {
        var quote = db.Quotes.Include(q => q.Items).FirstOrDefault(q => q.Id == quoteId);
        if (quote is null) return;

        db.QuoteVersionSnapshots.Add(new QuoteVersionSnapshot
        {
            QuoteId = quote.Id,
            VersionNumber = quote.Version,
            Method = quote.Method,
            AreaSqm = quote.AreaSqm,
            UnitPricePerSqm = quote.UnitPricePerSqm,
            PackageDescription = quote.PackageDescription,
            Subtotal = quote.Subtotal,
            DiscountPercent = quote.DiscountPercent,
            VatPercent = quote.VatPercent,
            GrandTotal = quote.GrandTotal,
            ItemsJson = System.Text.Json.JsonSerializer.Serialize(
                quote.Items.OrderBy(i => i.SortOrder)
                    .Select(i => new { i.Id, i.ItemCode, i.Name, i.Unit, i.Quantity, i.UnitPrice, i.Amount, i.SortOrder })),
            CreatedAt = now.AddHours(-2),
            CreatedByUserId = userId,
        });
        quote.Version = 2;
        // Bump the totals a bit so V1 vs V2 diff is visible.
        if (quote.Method == QuoteMethod.UnitCost && quote.AreaSqm.HasValue)
        {
            quote.AreaSqm = quote.AreaSqm.Value + 20m;
        }
        else if (quote.Items.Count > 0)
        {
            quote.Items[0].Quantity += 5m;
            quote.Items[0].Amount = decimal.Round(quote.Items[0].Quantity * quote.Items[0].UnitPrice, 2, MidpointRounding.AwayFromZero);
        }
        RecomputeTotals(quote);
        AppendLog(db, quote.Id, QuoteWorkflowAction.NewVersion, QuoteStatus.Cancelled, QuoteStatus.Cancelled, userId, now.AddHours(-1),
            $"Bumped to V{quote.Version} on edit-after-approval (sample).");
        db.SaveChanges();
    }

    private static QuoteItem NewItem(string code, string name, string unit, decimal qty, decimal price, int sort) => new()
    {
        ItemCode = code,
        Name = name,
        Unit = unit,
        Quantity = qty,
        UnitPrice = price,
        Amount = decimal.Round(qty * price, 2, MidpointRounding.AwayFromZero),
        SortOrder = sort,
    };

    private static void RecomputeTotals(Quote q)
    {
        var subtotal = q.Method == QuoteMethod.Boq
            ? q.Items.Sum(i => i.Amount)
            : decimal.Round((q.AreaSqm ?? 0m) * (q.UnitPricePerSqm ?? 0m), 2, MidpointRounding.AwayFromZero);
        var afterDiscount = subtotal * (1 - q.DiscountPercent / 100m);
        var vat = afterDiscount * (q.VatPercent / 100m);
        q.Subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        q.GrandTotal = decimal.Round(afterDiscount + vat, 2, MidpointRounding.AwayFromZero);
    }

    private static void AppendLog(AppDbContext db, int quoteId, QuoteWorkflowAction action,
        QuoteStatus from, QuoteStatus to, int userId, DateTime at, string? note = null)
    {
        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quoteId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ByUserId = userId,
            Note = note,
            CreatedAt = at,
        });
    }

    private const string SampleContractMarker = "[SAMPLE_CONTRACT]";

    /// <summary>
    /// Seeds a curated set of sample contracts spanning every
    /// <see cref="ContractStatus"/> so filters, badges and the
    /// "deadline within 30 days" warning have real rows to render.
    /// Uses existing sample customers so the FK is always valid.
    /// Idempotent via the note-marker guard used elsewhere in this file.
    /// </summary>
    private static void SeedContracts(AppDbContext db, ApplicationUser owner, DateTime now, string? webRootPath = null)
    {
        var alreadySeeded = db.Contracts.Any(c => c.Note != null && c.Note.StartsWith(SampleContractMarker));
        if (!alreadySeeded)
        {
            SeedContractHeaders(db, owner, now);
        }
        SeedContractMilestones(db, now);
        SeedContractAppendices(db, owner, now);
        SeedContractAttachments(db, owner, now, webRootPath);
    }

    private static void SeedContractHeaders(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var sampleCustomers = db.Customers
            .Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id)
            .Take(6)
            .ToList();
        if (sampleCustomers.Count == 0) return;

        var year = now.Year;
        var nextSeq = 1 + (db.Contracts.AsNoTracking()
            .Where(c => c.ContractNumber.StartsWith($"HD-{year}-"))
            .Select(c => c.ContractNumber)
            .AsEnumerable()
            .Select(n => int.TryParse(n.AsSpan($"HD-{year}-".Length), out var s) ? s : 0)
            .DefaultIfEmpty(0)
            .Max());

        // (custIdx, status, signedOffsetDays, durationDays, value, label)
        // The InProgress row uses a short remaining window on purpose so
        // the FE red badge (endDate - now ≤ 30 days) has a live example.
        var seeds = new (int CustIdx, ContractStatus Status, int SignedOffset, int DurationDays, decimal Value, string Label)[]
        {
            (0, ContractStatus.Draft,       0,   180, 250_000_000m, "Bản nháp — chờ 2 bên chốt"),
            (1, ContractStatus.Signed,     -20, 200,  850_000_000m, "Đã ký — chuẩn bị khởi công"),
            (2, ContractStatus.InProgress, -90, 100, 1_500_000_000m, "Đang thi công — sắp kết thúc"),
            (3, ContractStatus.InProgress, -30, 240,  620_000_000m, "Đang thi công — mới bắt đầu"),
            (4, ContractStatus.OnHold,     -60, 180,  480_000_000m, "Tạm dừng theo yêu cầu KH"),
            (5, ContractStatus.Completed, -240, 180,  980_000_000m, "Hoàn thành, đã bàn giao"),
        };

        foreach (var (custIdx, status, signedOffset, durationDays, value, label) in seeds)
        {
            if (custIdx >= sampleCustomers.Count) continue;
            var customer = sampleCustomers[custIdx];
            var signedDate = status == ContractStatus.Draft ? (DateTime?)null : now.AddDays(signedOffset);
            var startDate = signedDate?.AddDays(7);
            var endDate = startDate?.AddDays(durationDays);
            var number = $"HD-{year}-{nextSeq++:D4}";

            db.Contracts.Add(new Contract
            {
                ContractNumber = number,
                CustomerId = customer.Id,
                OwnerUserId = owner.Id,
                Status = status,
                SignedDate = signedDate,
                StartDate = startDate,
                EndDate = endDate,
                Value = value,
                ScopeOfWork = "Phạm vi thi công phần thô và hoàn thiện theo hồ sơ thiết kế kèm theo.",
                Note = $"{SampleContractMarker} {label}",
                CreatedAt = now.AddDays(signedOffset).AddDays(-3),
                UpdatedAt = now.AddHours(-1),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
            });
        }

        db.SaveChanges();
    }

    /// <summary>
    /// Attach a canonical 30-30-30-10 payment schedule to every non-draft
    /// sample contract that does not already have milestones so the payment
    /// schedule (NIH-103) has real data out of the box. Drafts intentionally
    /// have none — they mirror the "in progress" state of a real drafting
    /// session.
    ///
    /// Runs independently of the header seeder so upgrading an install that
    /// already had contracts (but no milestones) still back-fills them.
    /// </summary>
    private static void SeedContractMilestones(AppDbContext db, DateTime now)
    {
        var contractsMissingSchedule = db.Contracts
            .Where(c => c.Note != null && c.Note.StartsWith(SampleContractMarker)
                     && c.Status != ContractStatus.Draft
                     && c.SignedDate != null
                     && !db.ContractPaymentMilestones.Any(m => m.ContractId == c.Id))
            .ToList();
        if (contractsMissingSchedule.Count == 0) return;

        foreach (var c in contractsMissingSchedule)
        {
            var start = c.StartDate ?? c.SignedDate!.Value;
            var end = c.EndDate ?? start.AddDays(180);
            var mid = start.AddTicks((end - start).Ticks / 2);
            db.ContractPaymentMilestones.AddRange(
                new ContractPaymentMilestone { ContractId = c.Id, Order = 1, Name = "Đợt 1 - Tạm ứng khi ký HĐ", PercentValue = 30m, DueDate = c.SignedDate!.Value.AddDays(7), Status = PaymentMilestoneStatus.Paid, CreatedAt = now, UpdatedAt = now },
                new ContractPaymentMilestone { ContractId = c.Id, Order = 2, Name = "Đợt 2 - Nghiệm thu 50%", PercentValue = 30m, DueDate = mid, Status = c.Status == ContractStatus.Completed ? PaymentMilestoneStatus.Paid : PaymentMilestoneStatus.Requested, CreatedAt = now, UpdatedAt = now },
                new ContractPaymentMilestone { ContractId = c.Id, Order = 3, Name = "Đợt 3 - Bàn giao", PercentValue = 30m, DueDate = end, Status = c.Status == ContractStatus.Completed ? PaymentMilestoneStatus.Paid : PaymentMilestoneStatus.Pending, CreatedAt = now, UpdatedAt = now },
                new ContractPaymentMilestone { ContractId = c.Id, Order = 4, Name = "Đợt 4 - Quyết toán bảo hành", PercentValue = 10m, DueDate = end.AddDays(30), Status = c.Status == ContractStatus.Completed ? PaymentMilestoneStatus.Paid : PaymentMilestoneStatus.Pending, CreatedAt = now, UpdatedAt = now });
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Seed a variety of Variation Orders so every VO status (Draft,
    /// Submitted, Approved, Rejected) appears on at least one sample
    /// contract and reviewers land on a realistic mix of pending / decided
    /// rows. Idempotent — skips a contract that already has any VOs.
    /// </summary>
    private static void SeedContractAppendices(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var contracts = db.Contracts
            .Where(c => c.Note != null && c.Note.StartsWith(SampleContractMarker)
                     && c.Status != ContractStatus.Draft)
            .OrderBy(c => c.Id)
            .ToList();
        foreach (var c in contracts)
        {
            if (db.ContractAppendices.Any(v => v.ContractId == c.Id)) continue;
            switch (c.Status)
            {
                case ContractStatus.InProgress:
                    // Two VOs so header currentValue reflects a realistic
                    // stack, plus a rejected one so reviewers see the
                    // "Rejected" style out of the box.
                    db.ContractAppendices.AddRange(
                        new ContractAppendix
                        {
                            ContractId = c.Id,
                            VoNumber = 1,
                            Title = "Bổ sung hạng mục vách kính",
                            Reason = "Khách hàng yêu cầu thêm 2 vách kính cường lực + phụ kiện.",
                            ValueDelta = 45_000_000m,
                            Status = ContractAppendixStatus.Approved,
                            SubmittedAt = now.AddDays(-14),
                            SubmittedByUserId = owner.Id,
                            DecidedAt = now.AddDays(-12),
                            DecidedByUserId = owner.Id,
                            DecisionNote = "Đã đối chiếu bảng chi phí — chấp thuận.",
                            CreatedByUserId = owner.Id,
                            UpdatedByUserId = owner.Id,
                            CreatedAt = now.AddDays(-16),
                            UpdatedAt = now.AddDays(-12),
                        },
                        new ContractAppendix
                        {
                            ContractId = c.Id,
                            VoNumber = 2,
                            Title = "Thay đổi màu sơn ngoại thất",
                            Reason = "Đội thiết kế đề xuất bảng màu mới; chờ Sales Manager duyệt.",
                            ValueDelta = -8_500_000m,
                            Status = ContractAppendixStatus.Rejected,
                            SubmittedAt = now.AddDays(-4),
                            SubmittedByUserId = owner.Id,
                            DecidedAt = now.AddDays(-2),
                            DecidedByUserId = owner.Id,
                            DecisionNote = "Cần bổ sung mẫu vật liệu trước khi trình lại.",
                            CreatedByUserId = owner.Id,
                            UpdatedByUserId = owner.Id,
                            CreatedAt = now.AddDays(-6),
                            UpdatedAt = now.AddDays(-2),
                        });
                    break;
                case ContractStatus.Completed:
                    db.ContractAppendices.Add(new ContractAppendix
                    {
                        ContractId = c.Id,
                        VoNumber = 1,
                        Title = "Nâng cấp hệ thống chiếu sáng",
                        Reason = "Bổ sung 12 đèn downlight LED theo yêu cầu KH.",
                        ValueDelta = 18_000_000m,
                        Status = ContractAppendixStatus.Approved,
                        SubmittedAt = now.AddDays(-200),
                        SubmittedByUserId = owner.Id,
                        DecidedAt = now.AddDays(-195),
                        DecidedByUserId = owner.Id,
                        DecisionNote = "Đã bàn giao — quyết toán.",
                        CreatedByUserId = owner.Id,
                        UpdatedByUserId = owner.Id,
                        CreatedAt = now.AddDays(-205),
                        UpdatedAt = now.AddDays(-195),
                    });
                    break;
                case ContractStatus.Signed:
                    // Signed contracts have a Draft VO so Sales can see
                    // the editable state on first login.
                    db.ContractAppendices.Add(new ContractAppendix
                    {
                        ContractId = c.Id,
                        VoNumber = 1,
                        Title = "Đề xuất bổ sung sàn gỗ tự nhiên",
                        Reason = "Nháp — đang lấy báo giá vật liệu.",
                        ValueDelta = 32_000_000m,
                        Status = ContractAppendixStatus.Draft,
                        CreatedByUserId = owner.Id,
                        UpdatedByUserId = owner.Id,
                        CreatedAt = now.AddDays(-2),
                        UpdatedAt = now.AddDays(-1),
                    });
                    break;
                case ContractStatus.OnHold:
                    db.ContractAppendices.Add(new ContractAppendix
                    {
                        ContractId = c.Id,
                        VoNumber = 1,
                        Title = "Điều chỉnh vật liệu ốp lát",
                        Reason = "Chuyển đá nhân tạo sang đá tự nhiên theo đề xuất khách.",
                        ValueDelta = 28_000_000m,
                        Status = ContractAppendixStatus.Submitted,
                        SubmittedAt = now.AddDays(-3),
                        SubmittedByUserId = owner.Id,
                        CreatedByUserId = owner.Id,
                        UpdatedByUserId = owner.Id,
                        CreatedAt = now.AddDays(-5),
                        UpdatedAt = now.AddDays(-3),
                    });
                    break;
            }
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Seed contract attachments — one signed-scan placeholder for every
    /// non-Draft sample contract (needed to unlock the Signed→InProgress
    /// transition), plus a supporting document on InProgress rows so the
    /// Documents tab shows a realistic mixed list.
    ///
    /// When <paramref name="webRootPath"/> is supplied the seeder also
    /// self-heals a physical placeholder PDF at
    /// <c>wwwroot/files/contracts/sample-scan.pdf</c> so the download link
    /// on every seeded scan row actually resolves on a fresh install.
    /// </summary>
    private static void SeedContractAttachments(
        AppDbContext db, ApplicationUser owner, DateTime now, string? webRootPath = null)
    {
        const string SamplePlaceholderPath = "/files/contracts/sample-scan.pdf";
        const string SamplePlaceholderName = "sample-scan.pdf";
        long placeholderSize = 128_000;

        // Phase 1 — self-heal physical placeholder file whenever webRoot is
        // known. Runs on every boot so wiping the file store rehydrates
        // without a DB reset.
        if (!string.IsNullOrEmpty(webRootPath))
        {
            var storageDir = Path.Combine(webRootPath, "files", "contracts");
            Directory.CreateDirectory(storageDir);
            var fullPath = Path.Combine(storageDir, SamplePlaceholderName);
            if (!File.Exists(fullPath))
            {
                File.WriteAllBytes(fullPath, BuildPlaceholderPdf("Sample signed contract scan"));
            }
            placeholderSize = new FileInfo(fullPath).Length;
        }

        var contracts = db.Contracts
            .Where(c => c.Note != null && c.Note.StartsWith(SampleContractMarker)
                     && c.Status != ContractStatus.Draft)
            .ToList();
        foreach (var c in contracts)
        {
            if (db.ContractAttachments.Any(a => a.ContractId == c.Id)) continue;

            db.ContractAttachments.Add(new ContractAttachment
            {
                ContractId = c.Id,
                Kind = ContractAttachmentKind.SignedScan,
                FilePath = SamplePlaceholderPath,
                OriginalFileName = $"{c.ContractNumber}-scan.pdf",
                FileSize = placeholderSize,
                ContentType = "application/pdf",
                Label = "Bản scan hợp đồng đã ký",
                CreatedAt = now.AddDays(-1),
                UploadedByUserId = owner.Id,
            });

            // Supporting document on InProgress rows so the Documents tab
            // demonstrates a mixed list + delete flow out of the box.
            if (c.Status == ContractStatus.InProgress)
            {
                db.ContractAttachments.Add(new ContractAttachment
                {
                    ContractId = c.Id,
                    Kind = ContractAttachmentKind.Supporting,
                    FilePath = SamplePlaceholderPath,
                    OriginalFileName = $"{c.ContractNumber}-boq.pdf",
                    FileSize = placeholderSize,
                    ContentType = "application/pdf",
                    Label = "Bảng khối lượng chi tiết",
                    CreatedAt = now.AddHours(-4),
                    UploadedByUserId = owner.Id,
                });
            }
        }

        // Phase 2 — patch FileSize for existing sample rows so they match
        // the actual placeholder file size (regenerated at boot).
        if (!string.IsNullOrEmpty(webRootPath))
        {
            var existing = db.ContractAttachments
                .Where(a => a.FilePath == SamplePlaceholderPath && a.FileSize != placeholderSize)
                .ToList();
            foreach (var row in existing)
            {
                row.FileSize = placeholderSize;
            }
        }

        db.SaveChanges();
    }

    private const string SampleCapabilityMarker = "[SAMPLE_CAP]";

    /// <summary>
    /// Seeds a small shared capability-document library (NIH-98) so every
    /// tag chip and expiry badge on the FE has real data out-of-the-box.
    /// When <paramref name="webRootPath"/> is supplied the seeder also
    /// drops tiny placeholder PDFs under <c>wwwroot/files/capability/</c>
    /// so the download link on each row resolves on a fresh install. The
    /// physical-file check runs on every boot (not just the first) so
    /// deployments that lose the file store still self-heal, while the
    /// DB rows are inserted only once.
    /// </summary>
    private static void SeedCapabilityDocuments(AppDbContext db, ApplicationUser owner, DateTime now, string? webRootPath = null)
    {
        // Curated to cover every tag + every expiry-state band so the FE
        // filters have at least one row each to render.
        var seeds = new (string Name, string Tag, int? IssuedDaysAgo, int? ExpiryDaysFromNow, string File)[]
        {
            ("Giấy chứng nhận đăng ký doanh nghiệp", "phap-nhan", -365 * 3, null,                    "phap-nhan-erc.pdf"),
            ("Portfolio Kiến trúc 2026",              "kien-truc", -120,      null,                    "portfolio-kien-truc-2026.pdf"),
            ("Hồ sơ Kết cấu — Nhà máy Alpha",         "ket-cau",   -200,      365,                     "ho-so-ket-cau-alpha.pdf"),
            ("Hồ sơ MEP — Nhà xưởng Beta",            "mep",       -180,      45,                      "ho-so-mep-beta.pdf"),
            ("Chứng nhận ISO 9001:2015",              "iso",       -400,      -30,                     "iso-9001-2015.pdf"),
            ("Giấy phép xây dựng tổng thầu",          "giay-phep", -365 * 2, 25,                      "giay-phep-xay-dung.pdf"),
            ("Hồ sơ năng lực tổng hợp 2026",          "khac",      -30,       null,                    "ho-so-nang-luc-2026.pdf"),
        };

        // Phase 1 — self-heal physical files whenever webRoot is known.
        // Runs on every boot so a wiped/rebuilt file store repopulates
        // without requiring a DB reset. Also patches the FileSize column
        // on existing sample rows so the FE grid reflects the actual
        // placeholder file size rather than the estimate we wrote on
        // first insert.
        string? storageDir = null;
        if (!string.IsNullOrEmpty(webRootPath))
        {
            storageDir = Path.Combine(webRootPath, "files", "capability");
            Directory.CreateDirectory(storageDir);
            var fileSizes = new Dictionary<string, long>();
            foreach (var (name, _, _, _, file) in seeds)
            {
                var fullPath = Path.Combine(storageDir, file);
                if (!File.Exists(fullPath))
                {
                    File.WriteAllBytes(fullPath, BuildPlaceholderPdf(name));
                }
                fileSizes[$"/files/capability/{file}"] = new FileInfo(fullPath).Length;
            }
            var paths = fileSizes.Keys.ToList();
            var existing = db.CapabilityDocuments.Where(d => paths.Contains(d.FilePath)).ToList();
            var patched = false;
            foreach (var row in existing)
            {
                if (fileSizes.TryGetValue(row.FilePath, out var actualSize) && row.FileSize != actualSize)
                {
                    row.FileSize = actualSize;
                    patched = true;
                }
            }
            if (patched) db.SaveChanges();
        }

        // Phase 2 — DB rows are inserted only when the sample set is
        // absent, so admin edits (e.g. renaming a sample row) are not
        // clobbered on subsequent boots.
        if (db.CapabilityDocuments.Any(d => d.Description != null
            && d.Description.StartsWith(SampleCapabilityMarker))) return;

        var i = 0;
        foreach (var (name, tag, issuedDaysAgo, expiryDaysFromNow, file) in seeds)
        {
            long size = 512 * 1024 * (i + 1);
            if (storageDir is not null)
            {
                var fullPath = Path.Combine(storageDir, file);
                if (File.Exists(fullPath)) size = new FileInfo(fullPath).Length;
            }

            var doc = new CapabilityDocument
            {
                Name = name,
                TagCode = tag,
                IssuedDate = issuedDaysAgo.HasValue ? now.AddDays(issuedDaysAgo.Value) : null,
                ExpiryDate = expiryDaysFromNow.HasValue ? now.AddDays(expiryDaysFromNow.Value) : null,
                Description = $"{SampleCapabilityMarker} Sample capability document.",
                FilePath = $"/files/capability/{file}",
                OriginalFileName = file,
                FileSize = size,
                ContentType = "application/pdf",
                CurrentVersion = 1,
                UploadedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-14 + i),
                UpdatedAt = now.AddDays(-14 + i),
            };
            db.CapabilityDocuments.Add(doc);
            i++;
        }

        db.SaveChanges();
    }

    /// <summary>
    /// Build a minimal valid single-page PDF containing the document's
    /// display name. Kept intentionally small (well under 1KB) — real
    /// customer files still replace these via the upload endpoint.
    /// </summary>
    private static byte[] BuildPlaceholderPdf(string title)
    {
        // Escape PDF-string metacharacters and strip anything outside
        // WinAnsi so the built-in Helvetica font can render it.
        var safeTitle = new string(title
            .Where(c => c is >= (char)0x20 and < (char)0x7F)
            .ToArray())
            .Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Nihome capability document (sample)";

        // Hand-rolled PDF — six objects, one page, one Helvetica text run.
        var streamContent = $"BT /F1 14 Tf 40 740 Td ({safeTitle}) Tj ET";
        var streamBytes = System.Text.Encoding.ASCII.GetBytes(streamContent);
        var sb = new System.Text.StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void WriteObj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
        }
        WriteObj("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
        WriteObj("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n");
        WriteObj("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n");
        WriteObj($"4 0 obj<</Length {streamBytes.Length}>>stream\n{streamContent}\nendstream\nendobj\n");
        WriteObj("5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n");
        var xrefOffset = sb.Length;
        sb.Append("xref\n0 6\n0000000000 65535 f\n");
        foreach (var off in offsets)
        {
            sb.Append($"{off:D10} 00000 n\n");
        }
        sb.Append("trailer<</Size 6/Root 1 0 R>>\n");
        sb.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private const string SampleTenderMarker = "[SAMPLE_TENDER]";

    /// <summary>
    /// Seed a curated set of tender rows tied to existing sample
    /// customers so the FE list, deadline badge, status filter and the
    /// NIH-97 detail page (checklist / result tabs) all have populated
    /// data on a fresh DB. Idempotent — skipped when any sample tender
    /// already exists.
    /// </summary>
    private static void SeedTenders(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        if (db.Tenders.Any(t => t.Note != null && t.Note.StartsWith(SampleTenderMarker))) return;

        var customers = db.Customers.Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id).Take(5).ToList();
        if (customers.Count == 0) return;

        var templates = db.MasterDataOptions
            .Where(o => o.Category == "tender_checklist_default" && o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ToList();

        // Pick one capability doc to attach to the "richly populated"
        // checklist rows so the detail page has a downloadable file to
        // demonstrate the library-attach flow.
        var libraryDoc = db.CapabilityDocuments
            .Where(d => d.Description != null && d.Description.StartsWith(SampleCapabilityMarker))
            .OrderBy(d => d.Id)
            .FirstOrDefault();

        var sampleOpportunities = db.Opportunities
            .Where(o => o.Name.StartsWith(SampleTag))
            .OrderBy(o => o.Id)
            .ToList();

        var year = now.Year;
        var nextSeq = 1 + db.Tenders.Count(t => t.Code.StartsWith($"TD-{year}-"));

        // Curated scenarios so every state (Preparing / imminent / Submitted /
        // Won / Lost) has at least one row on the list, and the detail-page
        // checklist has ownership + internal deadlines + attached files.
        var seeds = new (string Name, int CustomerIdx, int DaysToDeadline, TenderStatus Status,
            bool RichChecklist, string? LostReason, string? LostNote)[]
        {
            ("Gói thầu xây dựng Nhà máy Alpha",         0, 21,  TenderStatus.Preparing, true,  null,       null),
            ("Gói thầu MEP Nhà xưởng Beta",             1, 2,   TenderStatus.Preparing, true,  null,       null),
            ("Gói thầu hoàn thiện nội thất Gamma",      2, -5,  TenderStatus.Submitted, false, null,       null),
            ("Gói thầu mở rộng kho Alpha",              0, -20, TenderStatus.Won,       false, null,       null),
            ("Gói thầu nội thất căn hộ mẫu Beta",       1, -30, TenderStatus.Lost,      false, "price",    "Khách chốt với đối thủ vì giá thấp hơn 8%."),
        };

        var i = 0;
        foreach (var (name, custIdx, daysToDeadline, status, richChecklist, lostReason, lostNote) in seeds)
        {
            if (custIdx >= customers.Count) continue;
            var customer = customers[custIdx];
            var code = $"TD-{year}-{nextSeq++:D4}";
            var deadline = now.AddDays(daysToDeadline);

            // Won tenders need a linked opportunity so the detail-page
            // Result tab renders the "Cơ hội đã gán" row. Pick the first
            // seeded opportunity for the same customer, else fall back to
            // any sample opportunity so the demo isn't empty.
            int? wonOppId = null;
            if (status == TenderStatus.Won)
            {
                wonOppId = sampleOpportunities.FirstOrDefault(o => o.CustomerId == customer.Id)?.Id
                    ?? sampleOpportunities.FirstOrDefault()?.Id;
            }

            var closedAt = status is TenderStatus.Won or TenderStatus.Lost
                ? now.AddDays(daysToDeadline + 3)
                : (DateTime?)null;

            var tender = new Tender
            {
                Code = code,
                Name = name,
                CustomerId = customer.Id,
                OpeningDate = now.AddDays(daysToDeadline - 14),
                SubmissionDeadline = deadline,
                PreparerUserId = owner.Id,
                InfoSource = i % 2 == 0 ? "Referral" : "Website",
                Status = status,
                Note = $"{SampleTenderMarker} Sample tender for demo.",
                WonOpportunityId = wonOppId,
                LostReasonCode = lostReason,
                LostNote = lostNote,
                ClosedAt = closedAt,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-7 + i),
                UpdatedAt = now.AddDays(-1 + i),
            };
            db.Tenders.Add(tender);
            db.SaveChanges();

            // Build the checklist. Rich rows carry ownership, an internal
            // deadline, and an attached file (path borrowed from a sample
            // capability document so the download link works). Everyone
            // else gets a mix of Done / NotStarted so % != 0 and != 100.
            var itemsToAdd = new List<TenderChecklistItem>();
            for (var idx = 0; idx < templates.Count; idx++)
            {
                var tpl = templates[idx];
                TenderChecklistItemStatus itemStatus;
                if (status == TenderStatus.Submitted || status == TenderStatus.Won)
                {
                    itemStatus = TenderChecklistItemStatus.Submitted;
                }
                else if (status == TenderStatus.Lost)
                {
                    // Lost tenders keep whatever they had — mostly done but
                    // not all submitted (bid never made it out).
                    itemStatus = idx < 4 ? TenderChecklistItemStatus.Done : TenderChecklistItemStatus.NotStarted;
                }
                else if (richChecklist)
                {
                    // Preparing — spread across every status so the
                    // detail-page dropdown shows variety.
                    itemStatus = idx switch
                    {
                        0 => TenderChecklistItemStatus.Done,
                        1 => TenderChecklistItemStatus.Done,
                        2 => TenderChecklistItemStatus.Preparing,
                        3 => TenderChecklistItemStatus.Preparing,
                        _ => TenderChecklistItemStatus.NotStarted,
                    };
                }
                else
                {
                    itemStatus = idx < 3
                        ? TenderChecklistItemStatus.Done
                        : TenderChecklistItemStatus.NotStarted;
                }

                var item = new TenderChecklistItem
                {
                    TenderId = tender.Id,
                    TemplateCode = tpl.Code,
                    Title = tpl.Name,
                    Status = itemStatus,
                    SortOrder = tpl.SortOrder != 0 ? tpl.SortOrder : idx + 1,
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-1),
                };

                if (richChecklist)
                {
                    // First two rows show ownership so the "Người phụ trách"
                    // column has data on the detail page. Falls back
                    // silently when the sample DB has no second user.
                    if (idx <= 1) item.OwnerUserId = owner.Id;
                    // Give one row an internal deadline in the near future.
                    if (idx == 1) item.InternalDeadline = now.AddDays(3);
                    // Attach a real file to the first "Done" row so the
                    // Download link works.
                    if (idx == 0 && libraryDoc is not null)
                    {
                        item.FilePath = libraryDoc.FilePath;
                        item.OriginalFileName = libraryDoc.OriginalFileName;
                    }
                }

                itemsToAdd.Add(item);
            }
            db.TenderChecklistItems.AddRange(itemsToAdd);
            db.SaveChanges();
            i++;
        }
    }

    private const string SampleSurveyMarker = "[SAMPLE_SURVEY]";

    /// <summary>
    /// Seed a curated set of survey rows so the NIH-99 list view has
    /// something to render on a fresh DB: every Drive sync state, a
    /// project-linked row, an opportunity-linked row, and one recent
    /// visit + one older row so the default DESC sort is visible.
    /// Idempotent — skipped when any sample survey already exists.
    /// </summary>
    private static void SeedSurveys(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        if (db.Surveys.Any(s => s.Note != null && s.Note.StartsWith(SampleSurveyMarker))) return;

        var sampleOpportunities = db.Opportunities
            .Where(o => o.Name.StartsWith(SampleTag))
            .OrderBy(o => o.Id)
            .ToList();

        // Projects are content-side and not always available on a bare seed
        // — pick the first row if present, else leave the link empty.
        var sampleProject = db.Projects.OrderBy(p => p.Id).FirstOrDefault();

        var year = now.Year;
        var nextSeq = 1 + db.Surveys.Count(s => s.Code.StartsWith($"SV-{year}-"));

        // (Location, ConstructionTypeCode, DaysAgo, DriveSync, DriveError, LinkProject, LinkOpportunity)
        var seeds = new (string Location, string ConstructionCode, int DaysAgo,
            SurveyDriveSyncStatus DriveSync, string? DriveError, bool LinkProject, int? LinkOppIdx)[]
        {
            ("Lô A5, KCN Bắc Ninh",           "industrial",     2,   SurveyDriveSyncStatus.Synced,    null,                                           true,  0),
            ("Số 12 Nguyễn Trãi, Q. Thanh Xuân, Hà Nội",   "residential",    5,   SurveyDriveSyncStatus.Syncing,   null,                                           false, 1),
            ("Toà nhà văn phòng Green Tower, Q.1, TP.HCM", "commercial",     8,   SurveyDriveSyncStatus.Failed,    "Quota Drive vượt hạn mức, cần cấp quyền lại.", false, null),
            ("Khu đô thị Sunbay, Nha Trang",  "mixed-use",      14,  SurveyDriveSyncStatus.NotSynced, null,                                           false, null),
            ("Showroom nội thất Đông Anh, Hà Nội",         "interior",       30,  SurveyDriveSyncStatus.Synced,    null,                                           false, null),
        };

        var i = 0;
        foreach (var (location, code, daysAgo, driveSync, driveError, linkProject, oppIdx) in seeds)
        {
            var seq = nextSeq++;
            int? linkedOppId = null;
            if (oppIdx.HasValue && oppIdx.Value < sampleOpportunities.Count)
            {
                linkedOppId = sampleOpportunities[oppIdx.Value].Id;
            }

            var survey = new Survey
            {
                Code = $"SV-{year}-{seq:D4}",
                Location = location,
                ConstructionTypeCode = code,
                SurveyDate = now.AddDays(-daysAgo),
                SurveyorUserId = owner.Id,
                LinkedProjectId = linkProject ? sampleProject?.Id : null,
                LinkedOpportunityId = linkedOppId,
                Note = $"{SampleSurveyMarker} Sample survey for demo.",
                DriveSyncStatus = driveSync,
                DriveSyncError = driveError,
                LastSyncedAt = driveSync == SurveyDriveSyncStatus.NotSynced ? null : now.AddDays(-daysAgo + 1),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-daysAgo),
                UpdatedAt = now.AddDays(-daysAgo + 1),
            };
            db.Surveys.Add(survey);
            i++;
        }
        db.SaveChanges();
    }

    private const string SampleDesignProjectMarker = "[SAMPLE_DP]";

    /// <summary>
    /// Seed a small demo set of design projects so the NIH-113 overview
    /// list has variety on a fresh DB (one per stage, plus one On-hold to
    /// exercise the status badge). Idempotent — guarded on the marker
    /// suffix on <c>Note</c>.
    /// </summary>
    private static void SeedDesignProjects(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        if (db.DesignProjects.Any(dp => dp.Note != null && dp.Note.StartsWith(SampleDesignProjectMarker))) return;

        // We need at least one customer to satisfy the FK. Sample customers
        // are created upstream in SeedCustomers; fall back to the first
        // real customer if none exist yet.
        var sampleCustomer = db.Customers
            .Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id)
            .FirstOrDefault()
            ?? db.Customers.OrderBy(c => c.Id).FirstOrDefault();
        if (sampleCustomer is null) return;

        // Prefer the sample contract that has already been pushed to
        // InProgress by the contracts seed so the auto-created project
        // matches production behaviour.
        var sampleContract = db.Contracts
            .Where(c => c.CustomerId == sampleCustomer.Id && c.Status == ContractStatus.InProgress)
            .OrderBy(c => c.Id)
            .FirstOrDefault();

        var year = now.Year;
        var nextSeq = 1 + db.DesignProjects.Count(dp => dp.ProjectCode.StartsWith($"DP-{year}-"));

        // (Name, Stage, Status, StartDaysAgo, DeadlineDaysAhead, LinkContract)
        var seeds = new (string Name, DesignProjectStage Stage, DesignProjectStatus Status,
            int StartDaysAgo, int DeadlineDaysAhead, bool LinkContract)[]
        {
            ("Nhà máy Alpha - Giai đoạn 1",   DesignProjectStage.Concept,     DesignProjectStatus.Active, 3,  90, true),
            ("Villa Bãi Dài - Nha Trang",     DesignProjectStage.BasicDesign, DesignProjectStatus.Active, 30, 120, false),
            ("Showroom nội thất Đông Anh",    DesignProjectStage.ShopDrawing, DesignProjectStatus.Active, 60, 30,  false),
            ("Nhà kho lạnh KCN Bắc Ninh",     DesignProjectStage.BasicDesign, DesignProjectStatus.OnHold, 45, -5,  false),
        };

        foreach (var (name, stage, status, startDaysAgo, deadlineDaysAhead, linkContract) in seeds)
        {
            var seq = nextSeq++;
            var dp = new DesignProject
            {
                ProjectCode = $"DP-{year}-{seq:D4}",
                Name = $"{SampleTag} {name}",
                CustomerId = sampleCustomer.Id,
                ContractId = linkContract ? sampleContract?.Id : null,
                ProjectManagerUserId = owner.Id,
                DesignLeadUserId = owner.Id,
                StartDate = now.AddDays(-startDaysAgo),
                Deadline = now.AddDays(deadlineDaysAhead),
                CurrentStage = stage,
                Status = status,
                Note = $"{SampleDesignProjectMarker} Sample design project for demo.",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-startDaysAgo),
                UpdatedAt = now.AddDays(-1),
            };
            db.DesignProjects.Add(dp);
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Seed the M3 permit checklist for every design project on a fresh
    /// DB. Idempotent — the (DesignProjectId, PermitTypeCode) unique index
    /// keeps re-runs a no-op. Populates a handful of dates + status
    /// transitions on the first project so the risk badges have data.
    /// </summary>
    private static void SeedPermitChecklists(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var templateCodes = db.MasterDataOptions
            .Where(m => m.Category == "permit_type" && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .Select(m => m.Code)
            .ToList();
        if (templateCodes.Count == 0) return;

        var designProjects = db.DesignProjects
            .OrderBy(dp => dp.Id)
            .ToList();
        if (designProjects.Count == 0) return;

        // Existing (project, permit) pairs so re-runs are a no-op.
        var existing = db.PermitChecklistItems
            .Select(p => new { p.DesignProjectId, p.PermitTypeCode })
            .ToHashSet();

        int seq = 0;
        foreach (var dp in designProjects)
        {
            foreach (var code in templateCodes)
            {
                if (existing.Contains(new { DesignProjectId = dp.Id, PermitTypeCode = code })) continue;

                var item = new PermitChecklistItem
                {
                    DesignProjectId = dp.Id,
                    PermitTypeCode = code,
                    Status = PermitStatus.NotStarted,
                    CreatedByUserId = owner.Id,
                    UpdatedByUserId = owner.Id,
                    CreatedAt = now.AddDays(-30),
                    UpdatedAt = now.AddDays(-1),
                };

                // Sprinkle status + dates on the first project so the risk
                // register + badges have visible data on a fresh DB.
                if (dp == designProjects.First())
                {
                    switch (code)
                    {
                        case "gpxd":
                            item.Status = PermitStatus.Submitted;
                            item.IssuingAgency = "Sở Xây dựng";
                            item.OwnerUserId = owner.Id;
                            item.TargetDeadline = now.AddDays(3);   // due soon
                            item.SubmittedAt = now.AddDays(-5);
                            break;
                        case "pccc":
                            item.Status = PermitStatus.Issued;
                            item.IssuingAgency = "Cảnh sát PCCC";
                            item.OwnerUserId = owner.Id;
                            item.TargetDeadline = now.AddDays(-40);
                            item.SubmittedAt = now.AddDays(-30);
                            item.IssuedAt = now.AddDays(-20);
                            item.ExpiresAt = now.AddDays(20);       // expiring soon
                            break;
                        case "electricity":
                            item.Status = PermitStatus.Preparing;
                            item.IssuingAgency = "Điện lực địa phương";
                            item.OwnerUserId = owner.Id;
                            item.TargetDeadline = now.AddDays(-2);  // overdue
                            break;
                    }
                }

                db.PermitChecklistItems.Add(item);
                seq++;
            }
        }
        if (seq > 0) db.SaveChanges();
    }

    /// <summary>
    /// Seed 3 concept options for the first sample design project so the
    /// NIH-114 UI has variety on a fresh DB (one Drafting, one
    /// PresentedToClient, one Discarded). Idempotent — guarded on the
    /// (design_project_id, name) uniqueness of the sample rows.
    /// </summary>
    private static void SeedConceptOptions(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.Concept)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;
        if (db.ConceptOptions.Any(c => c.DesignProjectId == project.Id
                                     && c.Description != null
                                     && c.Description.StartsWith(SampleTag))) return;

        var seeds = new (string Name, string Description, ConceptOptionStatus Status, int DaysAgo, bool Presented)[]
        {
            ("Phương án A - Hiện đại",  $"{SampleTag} Concept option A — tone hiện đại, tối giản.", ConceptOptionStatus.PresentedToClient,     3,  true),
            ("Phương án B - Truyền thống", $"{SampleTag} Concept option B — mái ngói, sân trong.",    ConceptOptionStatus.Drafting,               1,  false),
            ("Phương án C - Cổ điển",   $"{SampleTag} Concept option C — bỏ ngang do khách không thích tone màu.", ConceptOptionStatus.Discarded, 5, true),
        };

        foreach (var (name, description, status, daysAgo, presented) in seeds)
        {
            db.ConceptOptions.Add(new ConceptOption
            {
                DesignProjectId = project.Id,
                Name = name,
                Description = description,
                InternalNote = "Sample option — theo dõi demo.",
                OwnerUserId = owner.Id,
                PresentedAt = presented ? now.AddDays(-daysAgo) : null,
                Status = status,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-daysAgo - 3),
                UpdatedAt = now.AddDays(-daysAgo),
            });
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Seed a handful of Basic Design documents on the first design
    /// project that is at the BasicDesign stage so the NIH-115 tab has
    /// data on a fresh DB. Two disciplines already reach
    /// <c>InternallyApproved</c>; the third stays In Progress so the
    /// readiness gate is a partial-not-yet-ready state (exercises both
    /// UI paths). Idempotent — guarded on the "Sample" marker in Title.
    /// </summary>
    private static void SeedBasicDesignDocs(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_BD]";

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.BasicDesign)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;
        if (db.BasicDesignDocs.Any(d => d.DesignProjectId == project.Id
                                     && d.Note != null
                                     && d.Note.StartsWith(SampleMarker))) return;

        var seeds = new (string Discipline, string Prefix, string Title, BasicDesignDocStatus Status, int DaysAgo)[]
        {
            ("architecture", "KT-BD",  "Mặt bằng tầng 1 — Phương án chính thức",  BasicDesignDocStatus.InternallyApproved, 4),
            ("architecture", "KT-BD",  "Mặt đứng chính — Trục A-M",                BasicDesignDocStatus.SubmittedForReview, 2),
            ("structure",    "KC-BD",  "Kết cấu móng cọc — Bản vẽ tổng thể",       BasicDesignDocStatus.InternallyApproved, 6),
            ("mep",          "MEP-BD", "Hệ thống điện — Sơ đồ nguyên lý",          BasicDesignDocStatus.InProgress,         3),
        };

        var perDisciplineSeq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (discipline, prefix, title, status, daysAgo) in seeds)
        {
            perDisciplineSeq.TryGetValue(discipline, out var current);
            perDisciplineSeq[discipline] = current + 1;
            var seq = perDisciplineSeq[discipline];
            db.BasicDesignDocs.Add(new BasicDesignDoc
            {
                DesignProjectId = project.Id,
                DisciplineCode = discipline,
                DocumentCode = $"{prefix}-{seq:D3}",
                Title = title,
                OwnerUserId = owner.Id,
                Status = status,
                Note = $"{SampleMarker} Sample basic design document.",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-daysAgo - 3),
                UpdatedAt = now.AddDays(-daysAgo),
            });
        }
        db.SaveChanges();
    }

    /// <summary>
    /// NIH-116 Shop Drawing showcase. Attaches to the first sample design
    /// project already at ShopDrawing stage (currently "Showroom nội thất
    /// Đông Anh") so the tab lights up with drawings across every
    /// discipline + every state in the slice-1 state machine. Idempotent —
    /// guarded on the "[SAMPLE_SD]" marker in Note.
    /// </summary>
    private static void SeedShopDrawings(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_SD]";

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;
        if (db.ShopDrawings.Any(d => d.DesignProjectId == project.Id
                                  && d.Note != null
                                  && d.Note.StartsWith(SampleMarker))) return;

        var seeds = new (string Discipline, string Prefix, string Item, string Title, ShopDrawingStatus Status, int DaysAgo)[]
        {
            ("architecture", "KT-SD",  "Mặt bằng bố trí showroom tầng 1", "Bản vẽ bố trí cửa hàng — trục A-D", ShopDrawingStatus.Approved,   3),
            ("architecture", "KT-SD",  "Mặt bằng bố trí showroom tầng 1", "Chi tiết vách kính lễ tân",         ShopDrawingStatus.InReview,   1),
            ("structure",    "KC-SD",  "Móng cột chính",                  "Bố trí cốt thép móng M1",           ShopDrawingStatus.Drafting,   0),
            ("mep",          "MEP-SD", "Cấp thoát nước tầng 1",           "Sơ đồ nguyên lý cấp nước",          ShopDrawingStatus.Approved,   4),
            ("mep",          "MEP-SD", "Hệ điện chiếu sáng tầng 1",       "Sơ đồ nguyên lý chiếu sáng",        ShopDrawingStatus.PendingIfc, 2),
            ("interior",     "NT-SD",  "Trần thạch cao khu trưng bày",    "Chi tiết trần dán gỗ óc chó",       ShopDrawingStatus.Rejected,   5),
        };

        var perDisciplineSeq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (discipline, prefix, item, title, status, daysAgo) in seeds)
        {
            perDisciplineSeq.TryGetValue(discipline, out var current);
            perDisciplineSeq[discipline] = current + 1;
            var seq = perDisciplineSeq[discipline];
            db.ShopDrawings.Add(new ShopDrawing
            {
                DesignProjectId = project.Id,
                DisciplineCode = discipline,
                ConstructionItem = item,
                DrawingCode = $"{prefix}-{seq:D3}",
                Title = title,
                OwnerUserId = owner.Id,
                Status = status,
                Note = $"{SampleMarker} Sample shop drawing.",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-daysAgo - 3),
                UpdatedAt = now.AddDays(-daysAgo),
            });
        }
        db.SaveChanges();
    }

    /// <summary>
    /// NIH-117 Drawing Revision showcase. Attaches R1 revisions to the
    /// first sample shop drawing and the first sample basic design doc
    /// so the revision tab lights up on a fresh DB. Idempotent — guarded
    /// on the "[SAMPLE_REV]" marker in Note.
    /// </summary>
    private static void SeedDrawingRevisions(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_REV]";
        if (db.DrawingRevisions.Any(r => r.Note.StartsWith(SampleMarker))) return;

        // First seeded ShopDrawing — bump it with a client-request R1 +
        // a follow-up MEP-coordination R2 so the FE has real diff data
        // to show.
        var firstShop = db.ShopDrawings
            .OrderBy(s => s.Id)
            .FirstOrDefault();
        if (firstShop is not null)
        {
            db.DrawingRevisions.Add(new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.ShopDrawing,
                TargetId = firstShop.Id,
                RevisionNumber = 1,
                ReasonCode = "client-request",
                Note = $"{SampleMarker} Khách yêu cầu đổi vị trí cửa chính sang trục B để mở view.",
                IsCurrent = false,
                CreatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-2),
            });
            db.DrawingRevisions.Add(new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.ShopDrawing,
                TargetId = firstShop.Id,
                RevisionNumber = 2,
                ReasonCode = "mep-sync",
                Note = $"{SampleMarker} Đồng bộ hộp kỹ thuật MEP với bản vẽ điều hoà.",
                IsCurrent = true,
                CreatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-1),
            });
        }

        // First seeded BasicDesignDoc — one revision showing the reason
        // matrix flows across both drawing families.
        var firstBasic = db.BasicDesignDocs
            .OrderBy(b => b.Id)
            .FirstOrDefault();
        if (firstBasic is not null)
        {
            db.DrawingRevisions.Add(new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.BasicDesignDoc,
                TargetId = firstBasic.Id,
                RevisionNumber = 1,
                ReasonCode = "technical-fix",
                Note = $"{SampleMarker} Cập nhật cao độ tim cột theo khảo sát thực địa.",
                IsCurrent = true,
                CreatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-3),
            });
        }

        db.SaveChanges();
    }

    /// <summary>
    /// NIH-118 IFC Release showcase. Attaches one Draft phi\u1ebfu to the
    /// sample ShopDrawing-stage project so the FE has a working row to
    /// walk through. Idempotent — guarded on the "[SAMPLE_IFC]" marker.
    /// </summary>
    private static void SeedIfcReleases(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_IFC]";
        if (db.IfcReleases.Any(r => r.Note != null && r.Note.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        // Pick the seeded approved shop drawings on this project so the
        // Draft phi\u1ebfu can walk through the release action without
        // manual data setup.
        var approvedDrawings = db.ShopDrawings
            .Where(s => s.DesignProjectId == project.Id
                     && (s.Status == ShopDrawingStatus.Approved
                      || s.Status == ShopDrawingStatus.PendingIfc))
            .OrderBy(s => s.Id)
            .Take(3)
            .ToList();
        if (approvedDrawings.Count == 0) return;

        var release = new IfcRelease
        {
            DesignProjectId = project.Id,
            ReleaseNumber = $"IFC-{now.Year}-001",
            Title = "B\u00e0n giao t\u1ea7ng 1 — s\u1ea3n showroom",
            Status = IfcReleaseStatus.Draft,
            Note = $"{SampleMarker} Sample IFC packet — bundled the first approved shop drawings.",
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1),
            Items = approvedDrawings.Select(d => new IfcReleaseItem
            {
                ShopDrawingId = d.Id,
            }).ToList(),
            Recipients = new List<IfcReleaseRecipient>
            {
                new() { Name = "C\u00f4ng ty CP x\u00e2y d\u1ef1ng ABC", RecipientTypeCode = "main-contractor" },
                new() { Name = "T\u01b0 v\u1ea5n gi\u00e1m s\u00e1t XYZ", RecipientTypeCode = "supervisor" },
                new() { Name = "Ch\u1ee7 \u0111\u1ea7u t\u01b0 [SAMPLE] Nguy\u1ec5n V\u0103n An", RecipientTypeCode = "client" },
            },
        };
        db.IfcReleases.Add(release);
        db.SaveChanges();
    }

    /// <summary>
    /// M4 sample tasks (NIH-141) — seed a small dependent chain on the
    /// first ShopDrawing-stage project so the Gantt page has something
    /// to draw (planned/actual/overdue/progress mixed) without hand-crafted
    /// API calls. Idempotent: guarded by a marker in Description.
    /// </summary>
    private static void SeedConstructionTasks(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_CONSTR]";
        if (db.ConstructionTasks.Any(t => t.Description != null && t.Description.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        var today = DateOnly.FromDateTime(now);

        // Task 1 — Completed on time.
        var mobilization = new ConstructionTask
        {
            DesignProjectId = project.Id,
            TaskCode = "T-001",
            Wbs = "1.1",
            Name = "Huy \u0111\u1ed9ng nh\u00e2n l\u1ef1c & thi\u1ebft b\u1ecb",
            Description = $"{SampleMarker} Mobilise crew, cranes and lay down area.",
            PlannedStart = today.AddDays(-40),
            PlannedEnd = today.AddDays(-30),
            ActualStart = today.AddDays(-40),
            ActualEnd = today.AddDays(-31),
            ProgressPercent = 100,
            OwnerUserId = owner.Id,
            Status = ConstructionTaskStatus.Completed,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-45),
            UpdatedAt = now.AddDays(-31),
        };

        // Task 2 — In progress, on track.
        var foundation = new ConstructionTask
        {
            DesignProjectId = project.Id,
            TaskCode = "T-002",
            Wbs = "1.2",
            Name = "\u0110\u00e0o m\u00f3ng & \u0111\u1ed5 b\u00ea t\u00f4ng l\u00f3t",
            Description = $"{SampleMarker} Excavate footings and pour blinding concrete.",
            PlannedStart = today.AddDays(-29),
            PlannedEnd = today.AddDays(-5),
            ActualStart = today.AddDays(-28),
            ActualEnd = null,
            ProgressPercent = 65,
            OwnerUserId = owner.Id,
            Status = ConstructionTaskStatus.InProgress,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-45),
            UpdatedAt = now.AddDays(-1),
        };

        // Task 3 — Planned, would start after T-002 completes.
        var superstructure = new ConstructionTask
        {
            DesignProjectId = project.Id,
            TaskCode = "T-003",
            Wbs = "1.3",
            Name = "K\u1ebft c\u1ea5u b\u00ea t\u00f4ng c\u1ed9t & d\u1ea7m",
            Description = $"{SampleMarker} Columns and beams once footings cured.",
            PlannedStart = today.AddDays(-4),
            PlannedEnd = today.AddDays(20),
            ProgressPercent = 0,
            OwnerUserId = owner.Id,
            Status = ConstructionTaskStatus.Planned,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-45),
            UpdatedAt = now.AddDays(-45),
        };

        // Task 4 — Overdue (was due yesterday, still InProgress).
        var mepRoughIn = new ConstructionTask
        {
            DesignProjectId = project.Id,
            TaskCode = "T-004",
            Wbs = "2.1",
            Name = "MEP rough-in t\u1ea7ng 1",
            Description = $"{SampleMarker} MEP first fix — behind schedule for demo.",
            PlannedStart = today.AddDays(-10),
            PlannedEnd = today.AddDays(-1),
            ActualStart = today.AddDays(-10),
            ActualEnd = null,
            ProgressPercent = 80,
            OwnerUserId = owner.Id,
            Status = ConstructionTaskStatus.InProgress,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-45),
            UpdatedAt = now.AddDays(-1),
        };

        db.ConstructionTasks.AddRange(mobilization, foundation, superstructure, mepRoughIn);
        db.SaveChanges();

        // Dependencies: T-002 depends on T-001, T-003 depends on T-002.
        db.ConstructionTaskDependencies.AddRange(
            new ConstructionTaskDependency { TaskId = foundation.Id, PredecessorTaskId = mobilization.Id },
            new ConstructionTaskDependency { TaskId = superstructure.Id, PredecessorTaskId = foundation.Id });
        db.SaveChanges();
    }

    /// <summary>
    /// M4 sample site diaries (NIH-142) — three entries on the first
    /// ShopDrawing-stage project spanning the whole Draft → Submitted →
    /// Confirmed workflow so the daily-log page always has something to
    /// showcase (each status pill is exercised). Idempotent via a
    /// marker in <c>Note</c>.
    /// </summary>
    private static void SeedSiteDiaries(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_DIARY]";
        if (db.SiteDiaries.Any(d => d.Note != null && d.Note.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        // Only seed weather + diary if the master data seeded first (it
        // will have, but be defensive so a partial seed doesn't crash).
        if (!db.MasterDataOptions.Any(m => m.Category == "diary_weather" && m.Code == "sunny")) return;

        var today = DateOnly.FromDateTime(now);
        var confirmed = new SiteDiary
        {
            DesignProjectId = project.Id,
            DiaryDate = today.AddDays(-2),
            WeatherCode = "sunny",
            WeatherNote = "N\u1eafng nh\u1eb9, 29\u00b0C",
            HeadcountLabor = 42,
            HeadcountEngineers = 3,
            HeadcountSupervisors = 2,
            HeadcountSubcontractors = 8,
            MachinesSummary = "1 c\u1ea9u th\u00e1p, 2 m\u00e1y \u0111\u00e0o, 1 xe b\u00ea t\u00f4ng",
            MaterialsReceived = "18 T th\u00e9p \u0111\u00f4i #16, 45 m\u00b3 b\u00ea t\u00f4ng th\u01b0\u01a1ng ph\u1ea9m C30",
            WorkPerformed = "\u0110\u1ed5 b\u00ea t\u00f4ng m\u00f3ng b\u0103ng tr\u1ee5c A-D. Ho\u00e0n th\u00e0nh 60% l\u01b0\u1ee3ng \u0111\u1ed5 c\u1ee7a ng\u00e0y.",
            Incidents = "Kh\u00f4ng c\u00f3 s\u1ef1 c\u1ed1.",
            Note = $"{SampleMarker} \u0110\u00e3 x\u00e1c nh\u1eadn - m\u1eabu",
            Status = SiteDiaryStatus.Confirmed,
            SubmittedAt = now.AddDays(-2).AddHours(17),
            SubmittedByUserId = owner.Id,
            ConfirmedAt = now.AddDays(-1).AddHours(9),
            ConfirmedByUserId = owner.Id,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-1).AddHours(9),
        };

        var submitted = new SiteDiary
        {
            DesignProjectId = project.Id,
            DiaryDate = today.AddDays(-1),
            WeatherCode = "cloudy",
            WeatherNote = "Nhi\u1ec1u m\u00e2y, gi\u00f3 nh\u1eb9",
            HeadcountLabor = 38,
            HeadcountEngineers = 3,
            HeadcountSupervisors = 2,
            HeadcountSubcontractors = 5,
            MachinesSummary = "1 c\u1ea9u th\u00e1p, 2 m\u00e1y \u0111\u00e0o",
            MaterialsReceived = "Kh\u00f4ng nh\u1eadn v\u1eadt t\u01b0 m\u1edbi.",
            WorkPerformed = "L\u1eafp c\u1ed1t th\u00e9p m\u00f3ng tr\u1ee5c E-G, chu\u1ea9n b\u1ecb v\u00e1n khu\u00f4n.",
            Incidents = "1 c\u00f4ng nh\u00e2n c\u1eaft tay nh\u1eb9 khi c\u1eaft th\u00e9p, \u0111\u00e3 s\u01a1 c\u1ee9u.",
            Note = $"{SampleMarker} \u0110\u00e3 g\u1eedi ch\u1edd x\u00e1c nh\u1eadn - m\u1eabu",
            Status = SiteDiaryStatus.Submitted,
            SubmittedAt = now.AddDays(-1).AddHours(18),
            SubmittedByUserId = owner.Id,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1).AddHours(18),
        };

        var draft = new SiteDiary
        {
            DesignProjectId = project.Id,
            DiaryDate = today,
            WeatherCode = "rain",
            WeatherNote = "M\u01b0a nh\u1eb9 t\u1eeb 14h, t\u1ea1m d\u1eebng \u0111\u1ed5 b\u00ea t\u00f4ng",
            HeadcountLabor = 20,
            HeadcountEngineers = 2,
            HeadcountSupervisors = 1,
            HeadcountSubcontractors = 0,
            MachinesSummary = "1 c\u1ea9u th\u00e1p (\u0111\u1ec3 y\u00ean, gi\u00f3 kh\u00f4ng \u0111\u1ea1t ng\u01b0\u1ee1ng)",
            MaterialsReceived = null,
            WorkPerformed = "Bu\u1ed5i s\u00e1ng: gia c\u00f4ng c\u1ed1t th\u00e9p trong l\u00e1n. Chi\u1ec1u: t\u1ea1m d\u1eebng do m\u01b0a.",
            Incidents = null,
            Note = $"{SampleMarker} Nh\u00e1p - m\u1eabu",
            Status = SiteDiaryStatus.Draft,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.SiteDiaries.AddRange(confirmed, submitted, draft);
        db.SaveChanges();
    }

    /// <summary>
    /// M4 sample punch items (NIH-146) — 5 items across every status
    /// so the punch-list page always has a hit on each stat pill and
    /// one overdue row on a fresh boot. Idempotent via <c>Note</c>
    /// marker.
    /// </summary>
    private static void SeedPunchItems(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_PUNCH]";
        if (db.PunchItems.Any(p => p.Note != null && p.Note.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        var today = DateOnly.FromDateTime(now);
        var items = new[]
        {
            new PunchItem
            {
                DesignProjectId = project.Id,
                PunchCode = "P-001",
                Title = "T\u01b0\u1eddng khu WC t\u1ea7ng 1 b\u1ecb r\u1ea1n ch\u00e2n chim",
                Description = "Xu\u1ea5t hi\u1ec7n v\u1ebft n\u1ee9t nh\u1ecf tr\u1ea3i d\u1ecdc theo m\u1ea3ng t\u01b0\u1eddng ph\u00eda \u0111\u00f4ng.",
                Location = "T\u1ea7ng 1 - khu WC nam",
                Severity = PunchSeverity.Medium,
                AssigneeUserId = owner.Id,
                Deadline = today.AddDays(-2), // overdue
                Status = PunchStatus.Open,
                Note = $"{SampleMarker} m\u1eabu - m\u1edf, tr\u1ec5",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-8),
            },
            new PunchItem
            {
                DesignProjectId = project.Id,
                PunchCode = "P-002",
                Title = "\u1ed4 c\u1eafm \u0111i\u1ec7n ph\u00f2ng 302 kh\u00f4ng ho\u1ea1t \u0111\u1ed9ng",
                Location = "T\u1ea7ng 3 - ph\u00f2ng 302",
                Severity = PunchSeverity.High,
                AssigneeUserId = owner.Id,
                Deadline = today.AddDays(3),
                Status = PunchStatus.InProgress,
                Note = $"{SampleMarker} m\u1eabu - \u0111ang x\u1eed l\u00fd",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-1),
            },
            new PunchItem
            {
                DesignProjectId = project.Id,
                PunchCode = "P-003",
                Title = "K\u00ednh c\u1eeda s\u1ed5 x\u01b0\u1edbng b\u1ecb x\u01b0\u1edbc nh\u1eb9",
                Location = "T\u1ea7ng 2 - x\u01b0\u1edbng may",
                Severity = PunchSeverity.Low,
                AssigneeUserId = owner.Id,
                Status = PunchStatus.Fixed,
                ResolutionNote = "\u0110\u00e3 \u0111\u00e1nh b\u00f3ng b\u1ec1 m\u1eb7t k\u00ednh, \u0111ang ch\u1edd nghi\u1ec7m thu.",
                Note = $"{SampleMarker} m\u1eabu - ch\u1edd nghi\u1ec7m thu",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-1),
            },
            new PunchItem
            {
                DesignProjectId = project.Id,
                PunchCode = "P-004",
                Title = "S\u00e0n \u0111\u00e1 hoa c\u01b0\u01a1ng sanh chi\u1ebfu ngh\u1ec9 kh\u00f4ng b\u1eb1ng ph\u1eb3ng",
                Location = "Chi\u1ebfu ngh\u1ec9 t\u1ea7ng 2-3",
                Severity = PunchSeverity.Critical,
                AssigneeUserId = owner.Id,
                Status = PunchStatus.Verified,
                ResolutionNote = "\u0110\u00e3 c\u1eaft l\u1ea1i mi\u1ebfng \u0111\u00e1 v\u00e0 c\u00e2n b\u1eb1ng th\u1ee7 c\u00f4ng.",
                VerifiedAt = now.AddDays(-2),
                VerifiedByUserId = owner.Id,
                Note = $"{SampleMarker} m\u1eabu - \u0111\u00e3 \u0111\u00f3ng",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-2),
            },
            new PunchItem
            {
                DesignProjectId = project.Id,
                PunchCode = "P-005",
                Title = "\u0110\u1ec1 ngh\u1ecb thay quy c\u00e1ch tay v\u1ecbn",
                Location = "C\u1ea7u thang b\u1ed9",
                Severity = PunchSeverity.Low,
                Status = PunchStatus.Cancelled,
                Note = $"{SampleMarker} m\u1eabu - hu\u1ef7 do CDT \u0111\u1ed3ng \u00fd gi\u1eef quy c\u00e1ch c\u0169",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now.AddDays(-12),
            },
        };
        db.PunchItems.AddRange(items);
        db.SaveChanges();
    }

    private static void SeedAcceptanceRecords(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_ACCEPT]";
        if (db.AcceptanceRecords.Any(a => a.Description != null && a.Description.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        var task = db.ConstructionTasks
            .Where(t => t.DesignProjectId == project.Id)
            .OrderBy(t => t.TaskCode)
            .FirstOrDefault();

        var today = DateOnly.FromDateTime(now);

        var draft = new AcceptanceRecord
        {
            DesignProjectId = project.Id,
            ConstructionTaskId = task?.Id,
            AcceptanceCode = "A-001",
            Title = "Nghiệm thu móng phần cột trục A",
            Description = $"{SampleMarker} Draft record — waiting for site walk-through.",
            AcceptanceDate = today.AddDays(3),
            Location = "Trục A – Tầng hầm",
            Participants = "CĐT, TVGS, Nhà thầu chính",
            Findings = "Kiểm tra cường độ bê tông theo lô đúc 28 ngày.",
            Status = AcceptanceStatus.Draft,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-1),
        };

        var submitted = new AcceptanceRecord
        {
            DesignProjectId = project.Id,
            ConstructionTaskId = task?.Id,
            AcceptanceCode = "A-002",
            Title = "Nghiệm thu thép cột trục B (đợt 1)",
            Description = $"{SampleMarker} Submitted — awaiting client approval.",
            AcceptanceDate = today.AddDays(1),
            Location = "Trục B – Tầng 1",
            Participants = "CĐT, TVGS, Nhà thầu chính, Nhà thầu phụ MEP",
            Findings = "Đã kiểm tra chủng loại thép và vị trí lắp đặt.",
            Status = AcceptanceStatus.Submitted,
            SubmittedAt = now.AddHours(-6),
            SubmittedByUserId = owner.Id,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddHours(-6),
        };

        var overdue = new AcceptanceRecord
        {
            DesignProjectId = project.Id,
            AcceptanceCode = "A-003",
            Title = "Nghiệm thu chống thấm hầm — quá hạn",
            Description = $"{SampleMarker} Overdue submission for waterproofing acceptance.",
            AcceptanceDate = today.AddDays(-4),
            Location = "Tầng hầm B1",
            Participants = "CĐT, TVGS, Nhà thầu chống thấm",
            Findings = "Đợi khắc phục 2 điểm rò rỉ đã ghi nhận.",
            Status = AcceptanceStatus.Draft,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-4),
        };

        var approved = new AcceptanceRecord
        {
            DesignProjectId = project.Id,
            ConstructionTaskId = task?.Id,
            AcceptanceCode = "A-004",
            Title = "Nghiệm thu cốt nền phần móng đơn",
            Description = $"{SampleMarker} Approved by client, signed minutes archived.",
            AcceptanceDate = today.AddDays(-8),
            Location = "Toàn bộ móng đơn",
            Participants = "CĐT, TVGS, Nhà thầu chính",
            Findings = "Đạt yêu cầu theo hồ sơ thiết kế.",
            ResolutionNote = "Đã ký biên bản, chuyển bước.",
            Status = AcceptanceStatus.Approved,
            SubmittedAt = now.AddDays(-9),
            SubmittedByUserId = owner.Id,
            ApprovedAt = now.AddDays(-8),
            ApprovedByUserId = owner.Id,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-15),
            UpdatedAt = now.AddDays(-8),
        };

        var rejected = new AcceptanceRecord
        {
            DesignProjectId = project.Id,
            AcceptanceCode = "A-005",
            Title = "Nghiệm thu bê tông cột — cần khắc phục",
            Description = $"{SampleMarker} Rejected due to concrete finish issues.",
            AcceptanceDate = today.AddDays(-2),
            Location = "Trục C – Cột C1",
            Participants = "CĐT, TVGS",
            Findings = "Bề mặt bê tông có rỗ tại 3 vị trí, cần trát bù.",
            ResolutionNote = "Yêu cầu nhà thầu khắc phục và tái nộp trong tuần.",
            Status = AcceptanceStatus.Rejected,
            SubmittedAt = now.AddDays(-3),
            SubmittedByUserId = owner.Id,
            RejectedAt = now.AddDays(-2),
            RejectedByUserId = owner.Id,
            RevisionCount = 0,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-2),
        };

        db.AcceptanceRecords.AddRange(draft, submitted, overdue, approved, rejected);
        db.SaveChanges();
    }

    /// <summary>
    /// M4 sample as-built dossier (NIH-145) — 6 documents covering
    /// every category across Draft/Submitted/Approved/Archived so the
    /// completeness roll-up on the list page has real numbers on a
    /// fresh boot. Idempotent via <c>Note</c> marker.
    /// </summary>
    private static void SeedAsBuiltDocuments(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        const string SampleMarker = "[SAMPLE_ASBUILT]";
        if (db.AsBuiltDocuments.Any(a => a.Note != null && a.Note.StartsWith(SampleMarker))) return;

        var project = db.DesignProjects
            .Where(dp => dp.CurrentStage == DesignProjectStage.ShopDrawing)
            .OrderBy(dp => dp.Id)
            .FirstOrDefault();
        if (project is null) return;

        var docs = new[]
        {
            // Approved drawing — counts toward completeness.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-001",
                Title = "B\u1ea3n v\u1ebd ho\u00e0n c\u00f4ng ki\u1ebfn tr\u00fac t\u1ea7ng 1",
                Category = AsBuiltCategory.Drawing,
                Status = AsBuiltStatus.Approved,
                FileUrl = "/files/asbuilt/sample-arch-l1.pdf",
                Note = $"{SampleMarker} \u0110\u00e3 duy\u1ec7t - m\u1eabu",
                SubmittedAt = now.AddDays(-6),
                SubmittedByUserId = owner.Id,
                ApprovedAt = now.AddDays(-4),
                ApprovedByUserId = owner.Id,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-4),
            },
            // Archived acceptance minute — final.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-002",
                Title = "Bi\u00ean b\u1ea3n nghi\u1ec7m thu m\u00f3ng",
                Category = AsBuiltCategory.AcceptanceMinute,
                Status = AsBuiltStatus.Archived,
                FileUrl = "/files/asbuilt/sample-minute-foundation.pdf",
                Note = $"{SampleMarker} \u0110\u00e3 l\u01b0u tr\u1eef - m\u1eabu",
                SubmittedAt = now.AddDays(-14),
                SubmittedByUserId = owner.Id,
                ApprovedAt = now.AddDays(-12),
                ApprovedByUserId = owner.Id,
                ArchivedAt = now.AddDays(-1),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-1),
            },
            // Approved test report.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-003",
                Title = "B\u00e1o c\u00e1o th\u00ed nghi\u1ec7m b\u00ea t\u00f4ng C30",
                Category = AsBuiltCategory.TestReport,
                Status = AsBuiltStatus.Approved,
                FileUrl = "/files/asbuilt/sample-test-c30.pdf",
                Note = $"{SampleMarker} \u0110\u00e3 duy\u1ec7t - m\u1eabu",
                SubmittedAt = now.AddDays(-8),
                SubmittedByUserId = owner.Id,
                ApprovedAt = now.AddDays(-7),
                ApprovedByUserId = owner.Id,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-12),
                UpdatedAt = now.AddDays(-7),
            },
            // Submitted warranty — waiting on PM approval, keeps
            // completeness at 3/4 so the header pill shows a gap.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-004",
                Title = "Ch\u1ee9ng ch\u1ec9 b\u1ea3o h\u00e0nh th\u1ea7u ph\u1ee5 MEP",
                Category = AsBuiltCategory.WarrantyCertificate,
                Status = AsBuiltStatus.Submitted,
                FileUrl = "/files/asbuilt/sample-warranty-mep.pdf",
                Note = $"{SampleMarker} Ch\u1edd duy\u1ec7t - m\u1eabu",
                SubmittedAt = now.AddDays(-1),
                SubmittedByUserId = owner.Id,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-1),
            },
            // Draft — editable, useful for demo edit/submit flow.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-005",
                Title = "T\u1eadp \u1ea3nh ho\u00e0n c\u00f4ng ph\u1ea7n ngo\u1ea1i th\u1ea5t",
                Category = AsBuiltCategory.Other,
                Status = AsBuiltStatus.Draft,
                Note = $"{SampleMarker} Nh\u00e1p - m\u1eabu",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddHours(-6),
                UpdatedAt = now.AddHours(-6),
            },
            // Cancelled — showcase soft-remove path.
            new AsBuiltDocument
            {
                DesignProjectId = project.Id,
                DocumentCode = "AB-006",
                Title = "B\u1ea3n v\u1ebd sai phi\u00ean b\u1ea3n - c\u1ea7n hu\u1ef7",
                Category = AsBuiltCategory.Drawing,
                Status = AsBuiltStatus.Cancelled,
                Note = $"{SampleMarker} \u0110\u00e3 hu\u1ef7 - m\u1eabu",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now.AddDays(-14),
            },
        };
        db.AsBuiltDocuments.AddRange(docs);
        db.SaveChanges();
    }
}
