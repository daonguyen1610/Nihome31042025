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
        SeedCapabilityDocuments(db, owner, now, webRootPath);
        SeedTenders(db, owner, now);
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
    /// customers so the FE list, deadline badge and status filter all
    /// have populated data on a fresh DB. Idempotent — skipped when
    /// any sample tender already exists.
    /// </summary>
    private static void SeedTenders(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        if (db.Tenders.Any(t => t.Note != null && t.Note.StartsWith(SampleTenderMarker))) return;

        var customers = db.Customers.Where(c => c.Name.StartsWith(SampleTag))
            .OrderBy(c => c.Id).Take(3).ToList();
        if (customers.Count == 0) return;

        var templates = db.MasterDataOptions
            .Where(o => o.Category == "tender_checklist_default" && o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ToList();

        var year = now.Year;
        var nextSeq = 1 + db.Tenders.Count(t => t.Code.StartsWith($"TD-{year}-"));

        var seeds = new (string Name, int CustomerIdx, int DaysToDeadline, TenderStatus Status)[]
        {
            ("Gói thầu xây dựng Nhà máy Alpha",       0, 21,  TenderStatus.Preparing),
            ("Gói thầu MEP Nhà xưởng Beta",           1, 2,   TenderStatus.Preparing),   // deadline imminent
            ("Gói thầu hoàn thiện nội thất Gamma",    2, -5,  TenderStatus.Submitted),   // past deadline, already submitted
        };

        var i = 0;
        foreach (var (name, custIdx, daysToDeadline, status) in seeds)
        {
            if (custIdx >= customers.Count) continue;
            var customer = customers[custIdx];
            var code = $"TD-{year}-{nextSeq++:D4}";
            var deadline = now.AddDays(daysToDeadline);
            var tender = new Tender
            {
                Code = code,
                Name = name,
                CustomerId = customer.Id,
                OpeningDate = now.AddDays(daysToDeadline - 14),
                SubmissionDeadline = deadline,
                PreparerUserId = owner.Id,
                InfoSource = "Referral",
                Status = status,
                Note = $"{SampleTenderMarker} Sample tender for demo.",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-7 + i),
                UpdatedAt = now.AddDays(-1 + i),
            };
            db.Tenders.Add(tender);
            db.SaveChanges();

            // Attach default checklist derived from master data.
            var itemsToAdd = templates.Select((tpl, idx) => new TenderChecklistItem
            {
                TenderId = tender.Id,
                TemplateCode = tpl.Code,
                Title = tpl.Name,
                // Roughly half of items done on the "Preparing" tenders so the
                // % completion badge has something interesting to render.
                Status = status == TenderStatus.Submitted
                    ? TenderChecklistItemStatus.Submitted
                    : (idx < 3 ? TenderChecklistItemStatus.Done : TenderChecklistItemStatus.NotStarted),
                SortOrder = tpl.SortOrder != 0 ? tpl.SortOrder : idx + 1,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-1),
            }).ToList();
            db.TenderChecklistItems.AddRange(itemsToAdd);
            db.SaveChanges();
            i++;
        }
    }
}
