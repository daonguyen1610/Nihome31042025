using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

/// <summary>
/// Development-only samples for the CM Office and Site Command Board workspaces.
/// The CRM sample seeder already covers schedule, diary, punch, acceptance,
/// as-built and handover; this seeder fills the operational deliverables that
/// the CM/BCH screens otherwise show empty: preparation, QA/QC, QS and HSE.
/// </summary>
public static class ConstructionSampleSeeder
{
    public static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var project = db.OperationalProjects.FirstOrDefault(x => x.Code == "PJ-SAMPLE-003");
        if (project is null) return;

        var cm = ResolveUser(db, RbacRoleCodes.ProjectManager);
        if (cm is null) return;
        var site = ResolveUser(db, RbacRoleCodes.SiteCommander) ?? cm;
        var qa = ResolveUser(db, RbacRoleCodes.SiteQaQc) ?? site;
        var qs = ResolveUser(db, RbacRoleCodes.QuantitySurveyor) ?? site;

        EnsureMember(db, project.Id, site.Id, "Chỉ huy trưởng công trường", cm.Id, now);
        EnsureMember(db, project.Id, qa.Id, "Kỹ sư QA/QC công trường", cm.Id, now);
        EnsureMember(db, project.Id, qs.Id, "QS công trường nhà máy", cm.Id, now);
        db.SaveChanges();

        SeedPreparation(db, project.Id, cm.Id, now);
        var templates = SeedQaQcTemplates(db, qa.Id, now);
        db.SaveChanges();
        SeedQaQcDossiers(db, project.Id, templates, qa.Id, cm.Id, site.Id, now);
        SeedQsBatches(db, project.Id, qa.Id, cm.Id, now);
        SeedHseViolations(db, project.Id, site.Id, qa.Id, cm.Id, now);
        db.SaveChanges();
    }

    private static ApplicationUser? ResolveUser(AppDbContext db, string roleCode) =>
        db.Users.Where(x => x.IsActive && x.RoleEntity != null && x.RoleEntity.Code == roleCode)
            .OrderBy(x => x.Id).FirstOrDefault();

    private static void EnsureMember(AppDbContext db, int projectId, int userId, string position, int actorId, DateTime now)
    {
        if (db.OperationalProjectMembers.Any(x => x.OperationalProjectId == projectId && x.UserId == userId)) return;
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = projectId,
            UserId = userId,
            Position = position,
            StartedAt = now.AddDays(-45),
            Source = "Sample",
            SourceReference = "ConstructionSampleSeeder",
            CreatedByUserId = actorId,
            UpdatedByUserId = actorId,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    private static void SeedPreparation(AppDbContext db, int projectId, int actorId, DateTime now)
    {
        if (db.ConstructionPreparations.Any(x => x.OperationalProjectId == projectId && x.WbsBaselineReference == "WBS-BASELINE-ANPHU-R1")) return;
        db.ConstructionPreparations.Add(new ConstructionPreparation
        {
            OperationalProjectId = projectId,
            Version = 1,
            Status = ConstructionPreparationStatus.Published,
            WbsBaselineReference = "WBS-BASELINE-ANPHU-R1",
            ScheduleBaselineReference = "CM-SCHEDULE-BASELINE-ANPHU-R1",
            SCurveBaselineReference = "S-CURVE-BASELINE-ANPHU-R1",
            BchOrganizationReference = "BCH-ORG-ANPHU-2026",
            ResourcePlanReference = "RESOURCE-PLAN-ANPHU-R1",
            BudgetBaselineReference = "BUDGET-BASELINE-ANPHU-R1",
            BudgetBaselineLocked = true,
            SiteHandoverRequired = true,
            MethodStatementRequired = true,
            HsePlanRequired = true,
            PublishedAt = now.AddDays(-30),
            PublishedByUserId = actorId,
            CreatedByUserId = actorId,
            UpdatedByUserId = actorId,
            CreatedAt = now.AddDays(-32),
            UpdatedAt = now.AddDays(-30),
        });
    }

    private static List<QaQcTemplate> SeedQaQcTemplates(AppDbContext db, int actorId, DateTime now)
    {
        var samples = new[]
        {
            new QaQcTemplate
            {
                Code = "QA-SAMPLE-CONCRETE",
                Version = 1,
                NameVi = "Nghiệm thu bê tông kết cấu",
                Name = "Structural concrete inspection",
                NameZh = "结构混凝土验收",
                NameJa = "構造コンクリート検査",
                SchemaJson = """{"items":[{"code":"C1","title":"Cao độ và tim trục đúng thiết kế"},{"code":"C2","title":"Cốp pha kín, không rò rỉ"},{"code":"C3","title":"Cốt thép đúng chủng loại và khoảng cách"},{"code":"C4","title":"Độ sụt bê tông trong giới hạn cho phép"},{"code":"C5","title":"Bảo dưỡng bê tông theo phương án"}]}""",
            },
            new QaQcTemplate
            {
                Code = "QA-SAMPLE-MEP",
                Version = 1,
                NameVi = "Nghiệm thu ống gió và đường ống MEP",
                Name = "MEP ducting and piping inspection",
                NameZh = "MEP风管与管道验收",
                NameJa = "MEPダクト・配管検査",
                SchemaJson = """{"items":[{"code":"M1","title":"Cao độ treo ống gió đúng bản vẽ"},{"code":"M2","title":"Mối nối ống gió kín, có keo làm kín"},{"code":"M3","title":"Thử kín đường ống nước"},{"code":"M4","title":"Tiếp địa và liên kết thiết bị điện"}]}""",
            },
        };
        var result = new List<QaQcTemplate>();
        foreach (var sample in samples)
        {
            var existing = db.QaQcTemplates.FirstOrDefault(x => x.Code == sample.Code && x.Version == sample.Version);
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }
            sample.IsPublished = true;
            sample.PublishedAt = now.AddDays(-25);
            sample.CreatedByUserId = actorId;
            sample.CreatedAt = now.AddDays(-27);
            db.QaQcTemplates.Add(sample);
            result.Add(sample);
        }
        return result;
    }

    private static void SeedQaQcDossiers(
        AppDbContext db, int projectId, List<QaQcTemplate> templates, int authorId, int reviewerId, int siteId, DateTime now)
    {
        if (db.QaQcDossiers.Any(x => x.OperationalProjectId == projectId && x.Code.StartsWith("QA-SAMPLE-"))) return;
        var concrete = templates.FirstOrDefault(x => x.Code == "QA-SAMPLE-CONCRETE");
        var mep = templates.FirstOrDefault(x => x.Code == "QA-SAMPLE-MEP");
        if (concrete is null || mep is null) return;

        var ifcReleaseId = db.IfcReleases
            .Where(x => x.DesignProject.OperationalProjectId == projectId && x.Status == IfcReleaseStatus.Released)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

        var samples = new[]
        {
            new QaQcDossier
            {
                Code = "QA-SAMPLE-001",
                QaQcTemplate = concrete,
                TemplateVersion = concrete.Version,
                TemplateSnapshotJson = concrete.SchemaJson,
                IfcReleaseId = ifcReleaseId,
                WbsCode = "WBS-3.1-FORM",
                Title = "Nghiệm thu bê tông sàn tầng 2 — trục A–D",
                ChecklistResultsJson = """[{"code":"C1","result":"Pass","note":"Sai số 4 mm, trong dung sai"},{"code":"C2","result":"Pass"},{"code":"C3","result":"Pass","note":"Đã đối chiếu thép chủ D16"},{"code":"C4","result":"Pass","note":"Độ sụt 120 mm"},{"code":"C5","result":"Pass"}]""",
                Status = ConstructionReviewStatus.CmApproved,
                SubmittedAt = now.AddDays(-9),
                CmReviewedAt = now.AddDays(-8),
                CmReviewedByUserId = reviewerId,
                CmDecisionReason = "Hồ sơ đầy đủ, kết quả kiểm tra đạt yêu cầu thiết kế.",
            },
            new QaQcDossier
            {
                Code = "QA-SAMPLE-002",
                QaQcTemplate = mep,
                TemplateVersion = mep.Version,
                TemplateSnapshotJson = mep.SchemaJson,
                IfcReleaseId = ifcReleaseId,
                WbsCode = "WBS-3.4-MEP",
                Title = "Nghiệm thu ống gió tầng 1 — khu vực xưởng may",
                ChecklistResultsJson = """[{"code":"M1","result":"Pass"},{"code":"M2","result":"Fail","note":"Còn 2 mối nối chưa làm kín tại trục C"}]""",
                Status = ConstructionReviewStatus.PendingCmReview,
                SubmittedAt = now.AddDays(-2),
            },
            new QaQcDossier
            {
                Code = "QA-SAMPLE-003",
                QaQcTemplate = concrete,
                TemplateVersion = concrete.Version,
                TemplateSnapshotJson = concrete.SchemaJson,
                IfcReleaseId = ifcReleaseId,
                WbsCode = "WBS-3.1-COL",
                Title = "Nghiệm thu cốt thép cột tầng 3 — trục B",
                ChecklistResultsJson = """[{"code":"C3","result":"Pending","note":"Chờ biên bản kiểm tra thép"}]""",
                Status = ConstructionReviewStatus.Draft,
            },
            new QaQcDossier
            {
                Code = "QA-SAMPLE-004",
                QaQcTemplate = mep,
                TemplateVersion = mep.Version,
                TemplateSnapshotJson = mep.SchemaJson,
                IfcReleaseId = ifcReleaseId,
                WbsCode = "WBS-3.4-ELEC",
                Title = "Nghiệm thu tiếp địa tủ điện tổng",
                ChecklistResultsJson = """[{"code":"M4","result":"Fail","note":"Chưa có biên bản đo điện trở tiếp địa"}]""",
                Status = ConstructionReviewStatus.CmRejected,
                SubmittedAt = now.AddDays(-5),
                CmReviewedAt = now.AddDays(-4),
                CmReviewedByUserId = reviewerId,
                CmDecisionReason = "Thiếu biên bản đo điện trở tiếp địa. Bổ sung trước khi trình lại.",
            },
        };

        foreach (var sample in samples)
        {
            sample.OperationalProjectId = projectId;
            sample.CreatedByUserId = authorId;
            sample.LastEditedByUserId = authorId;
            sample.CreatedAt = sample.SubmittedAt?.AddDays(-1) ?? now.AddDays(-1);
            sample.UpdatedAt = sample.CmReviewedAt ?? sample.SubmittedAt ?? sample.CreatedAt;
            db.QaQcDossiers.Add(sample);
        }
    }

    private static void SeedQsBatches(AppDbContext db, int projectId, int authorId, int reviewerId, DateTime now)
    {
        if (db.ConstructionQsBatches.Any(x => x.OperationalProjectId == projectId && x.Code.StartsWith("QS-SAMPLE-"))) return;

        var upstreamContract = db.Contracts
            .Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream)
            .OrderBy(x => x.Id)
            .FirstOrDefault();
        var downstreamContract = db.Contracts
            .Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Downstream)
            .OrderBy(x => x.Id)
            .FirstOrDefault();
        if (upstreamContract is null) return;

        var upstreamMilestone = db.ContractPaymentMilestones
            .Where(x => x.ContractId == upstreamContract.Id)
            .OrderBy(x => x.Order)
            .Skip(1)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

        AddBatch(db, projectId, authorId, now, new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-001",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.CmApproved,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = upstreamMilestone,
            WbsCode = "WBS-3.1",
            PeriodFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Description = "Nghiệm thu khối lượng kết cấu tầng 1–2, đợt 1",
            SubmittedAt = now.AddDays(-12),
            CmReviewedAt = now.AddDays(-11),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Khối lượng khớp bản vẽ và biên bản hiện trường.",
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-01", Description = "Cốp pha sàn tầng 2", Unit = "m2", Quantity = 120, UnitPrice = 850_000, Amount = 102_000_000, SortOrder = 1 },
                new ConstructionQsBatchLine { ItemCode = "CT-02", Description = "Bê tông sàn tầng 2", Unit = "m3", Quantity = 45, UnitPrice = 1_650_000, Amount = 74_250_000, SortOrder = 2 },
            },
        });

        AddBatch(db, projectId, authorId, now, new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-002",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.PendingCmReview,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = upstreamMilestone,
            WbsCode = "WBS-3.2",
            PeriodFrom = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            Description = "Nghiệm thu khối lượng kết cấu tầng 3, đợt 2",
            SubmittedAt = now.AddDays(-1),
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-03", Description = "Cốt thép cột tầng 3", Unit = "tấn", Quantity = 12.5m, UnitPrice = 18_500_000, Amount = 231_250_000, SortOrder = 1 },
            },
        });

        if (downstreamContract is not null)
        {
            AddBatch(db, projectId, authorId, now, new ConstructionQsBatch
            {
                Code = "QS-SAMPLE-003",
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.Draft,
                ContractId = downstreamContract.Id,
                WbsCode = "WBS-3.4",
                PeriodFrom = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodTo = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                Description = "Khối lượng nhà thầu phụ MEP trình kỳ tháng 10",
                Lines =
                {
                    new ConstructionQsBatchLine { ItemCode = "MEP-01", Description = "Ống gió tầng 1", Unit = "m2", Quantity = 86, UnitPrice = 420_000, Amount = 36_120_000, SortOrder = 1 },
                },
            });
        }
    }

    private static void AddBatch(
        AppDbContext db, int projectId, int authorId, DateTime now, ConstructionQsBatch batch)
    {
        batch.OperationalProjectId = projectId;
        batch.CertifiedAmount = batch.Lines.Sum(x => x.Amount);
        batch.RecordState = ConstructionQsRecordState.Active;
        batch.IsActiveLineage = true;
        batch.CreatedByUserId = authorId;
        batch.LastEditedByUserId = authorId;
        batch.CreatedAt = now.AddDays(-15);
        batch.UpdatedAt = batch.CmReviewedAt ?? batch.SubmittedAt ?? batch.CreatedAt;
        db.ConstructionQsBatches.Add(batch);
    }

    private static void SeedHseViolations(
        AppDbContext db, int projectId, int siteId, int qaId, int reviewerId, DateTime now)
    {
        if (db.HseViolations.Any(x => x.OperationalProjectId == projectId && x.Code.StartsWith("HSE-SAMPLE-"))) return;

        var samples = new[]
        {
            new HseViolation
            {
                Code = "HSE-SAMPLE-001",
                OccurredAt = now.AddDays(-3).Date.AddHours(8).AddMinutes(15),
                Location = "Tầng 1 – khu vực gia công cốp pha",
                Category = "Không đeo kính bảo hộ khi cắt thép",
                Severity = HseViolationSeverity.Medium,
                Description = "03 công nhân cắt thép bằng máy cắt cầm tay không đeo kính bảo hộ. Đã nhắc nhở ngay tại hiện trường.",
                RegulatoryReference = "Nội quy an toàn công trường – Điều 6",
                Status = HseViolationStatus.Draft,
            },
            new HseViolation
            {
                Code = "HSE-SAMPLE-002",
                OccurredAt = now.AddDays(-2).Date.AddHours(14),
                Location = "Tầng 2 – giàn giáo trục B-C",
                Category = "Giàn giáo thiếu lan can bảo vệ",
                Severity = HseViolationSeverity.High,
                Description = "Đoạn giàn giáo dài khoảng 6 m tại trục B-C chưa lắp lan can và lưới chắn. Công nhân vẫn đi lại phía trên.",
                RegulatoryReference = "Nội quy an toàn công trường – Điều 9",
                Status = HseViolationStatus.Reported,
                ReportedAt = now.AddDays(-2).Date.AddHours(15),
                ReportedByUserId = siteId,
            },
            new HseViolation
            {
                Code = "HSE-SAMPLE-003",
                OccurredAt = now.AddDays(-6).Date.AddHours(9).AddMinutes(30),
                Location = "Sân công trường – khu vực tập kết vật tư",
                Category = "Xếp vật liệu sai vị trí, che lối thoát hiểm",
                Severity = HseViolationSeverity.Medium,
                Description = "Các tấm cốp pha được xếp chồng trước cửa thoát hiểm số 2, che khuất biển chỉ dẫn.",
                RegulatoryReference = "Nội quy an toàn công trường – Điều 12",
                Status = HseViolationStatus.Confirmed,
                ReportedAt = now.AddDays(-6).Date.AddHours(10),
                ReportedByUserId = siteId,
                ConfirmedAt = now.AddDays(-5).Date.AddHours(8),
                ConfirmedByUserId = reviewerId,
                RemediationOwnerUserId = siteId,
                RemediationDeadline = now.AddDays(2),
            },
            new HseViolation
            {
                Code = "HSE-SAMPLE-004",
                OccurredAt = now.AddDays(-10).Date.AddHours(7).AddMinutes(45),
                Location = "Cổng công trường – khu vực xuất nhập vật tư",
                Category = "Xe tải chở vật tư không chằng buộc",
                Severity = HseViolationSeverity.Critical,
                Description = "Xe tải biển số 51C-123.45 chở thép cuộn không chằng buộc khi ra khỏi cổng công trường.",
                RegulatoryReference = "Nội quy an toàn công trường – Điều 15",
                Status = HseViolationStatus.Remediated,
                ReportedAt = now.AddDays(-10).Date.AddHours(8),
                ReportedByUserId = siteId,
                ConfirmedAt = now.AddDays(-9).Date.AddHours(9),
                ConfirmedByUserId = reviewerId,
                RemediationOwnerUserId = siteId,
                RemediationNote = "Đã dừng xe, yêu cầu chằng buộc lại và nhắc nhở nhà thầu vận tải. Ảnh xác nhận đã lưu.",
                RemediationDeadline = now.AddDays(-8),
                PenaltyReference = "BB-AT-2026-014",
                PenaltyAmount = 2_000_000,
            },
            new HseViolation
            {
                Code = "HSE-SAMPLE-005",
                OccurredAt = now.AddDays(-20).Date.AddHours(10),
                Location = "Tầng 1 – khu vực hàn kết cấu",
                Category = "Không có người giám sát khi hàn điện",
                Severity = HseViolationSeverity.High,
                Description = "Công nhân hàn điện làm việc một mình, không có người giám sát và không có bình chữa cháy tại chỗ.",
                RegulatoryReference = "Nội quy an toàn công trường – Điều 8",
                Status = HseViolationStatus.Closed,
                ReportedAt = now.AddDays(-20).Date.AddHours(11),
                ReportedByUserId = siteId,
                ConfirmedAt = now.AddDays(-19).Date.AddHours(8),
                ConfirmedByUserId = reviewerId,
                RemediationOwnerUserId = siteId,
                RemediationNote = "Đã bố trí người giám sát và trang bị bình chữa cháy tại vị trí hàn.",
                ClosedAt = now.AddDays(-15).Date.AddHours(16),
                ClosedByUserId = reviewerId,
            },
        };

        foreach (var sample in samples)
        {
            sample.OperationalProjectId = projectId;
            sample.OfflineClientId = Guid.NewGuid().ToString();
            sample.ResponsibleSiteUserId = siteId;
            sample.CreatedByUserId = sample.Code is "HSE-SAMPLE-001" ? qaId : siteId;
            sample.UpdatedByUserId = sample.ConfirmedByUserId ?? sample.CreatedByUserId;
            sample.CreatedAt = sample.OccurredAt.AddHours(1);
            sample.UpdatedAt = sample.ClosedAt ?? sample.ConfirmedAt ?? sample.ReportedAt ?? sample.CreatedAt;
            sample.Events.Add(new HseViolationEvent
            {
                Type = HseViolationEventType.Created,
                ToStatus = HseViolationStatus.Draft,
                CreatedByUserId = sample.CreatedByUserId,
                CreatedAt = sample.CreatedAt,
            });
            if (sample.ReportedAt is not null)
                sample.Events.Add(new HseViolationEvent
                {
                    Type = HseViolationEventType.Transition,
                    FromStatus = HseViolationStatus.Draft,
                    ToStatus = HseViolationStatus.Reported,
                    CreatedByUserId = sample.ReportedByUserId ?? sample.CreatedByUserId,
                    CreatedAt = sample.ReportedAt.Value,
                });
            if (sample.ConfirmedAt is not null)
                sample.Events.Add(new HseViolationEvent
                {
                    Type = HseViolationEventType.Transition,
                    FromStatus = HseViolationStatus.Reported,
                    ToStatus = HseViolationStatus.Confirmed,
                    CreatedByUserId = sample.ConfirmedByUserId ?? reviewerId,
                    CreatedAt = sample.ConfirmedAt.Value,
                });
            if (sample.RemediationNote is not null)
                sample.Events.Add(new HseViolationEvent
                {
                    Type = HseViolationEventType.Transition,
                    FromStatus = HseViolationStatus.Confirmed,
                    ToStatus = HseViolationStatus.Remediated,
                    Reason = sample.RemediationNote,
                    CreatedByUserId = siteId,
                    CreatedAt = sample.ConfirmedAt?.AddDays(1) ?? now,
                });
            if (sample.ClosedAt is not null)
                sample.Events.Add(new HseViolationEvent
                {
                    Type = HseViolationEventType.Transition,
                    FromStatus = HseViolationStatus.Remediated,
                    ToStatus = HseViolationStatus.Closed,
                    CreatedByUserId = sample.ClosedByUserId ?? reviewerId,
                    CreatedAt = sample.ClosedAt.Value,
                });
            db.HseViolations.Add(sample);
        }
    }
}
