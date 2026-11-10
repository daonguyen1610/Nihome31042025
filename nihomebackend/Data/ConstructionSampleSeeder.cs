using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

/// <summary>
/// Development-only samples for the CM Office and Site Command Board workspaces.
/// The CRM sample seeder already covers schedule, diary, punch, acceptance,
/// as-built and handover; this seeder fills the operational deliverables that
/// the CM/BCH screens otherwise show empty: preparation, resource handoff,
/// QA/QC, QS and HSE. Every record is scoped to the An Phu factory sample and
/// is created only when <see cref="DbSeeder"/> runs with development samples.
/// </summary>
public static class ConstructionSampleSeeder
{
    private const string EvidenceRoot = "/files/business-documents/construction";

    private static readonly (string Key, string FileName, string Title)[] EvidenceSamples =
    [
        ("wbs-baseline", "WBS-baseline-An-Phu-R1.pdf", "WBS baseline An Phu factory R1"),
        ("schedule-baseline", "Tien-do-baseline-An-Phu-R1.pdf", "Schedule baseline An Phu factory R1"),
        ("s-curve-baseline", "S-curve-baseline-An-Phu-R1.pdf", "S-curve baseline An Phu factory R1"),
        ("bch-organization", "So-do-to-chuc-BCH-An-Phu-2026.pdf", "Site command organization An Phu 2026"),
        ("resource-plan", "Ke-hoach-nguon-luc-An-Phu-R1.pdf", "Resource plan An Phu factory R1"),
        ("budget-baseline", "Ngan-sach-baseline-An-Phu-R1.pdf", "Budget baseline An Phu factory R1"),
        ("site-handover", "Bien-ban-ban-giao-mat-bang.pdf", "Site handover minute An Phu factory"),
        ("method-statement", "Bien-phap-thi-cong-ket-cau.pdf", "Structural method statement approval"),
        ("hse-plan", "Ke-hoach-an-toan-lao-dong.pdf", "HSE plan approval An Phu factory"),
        ("concrete-test", "Ket-qua-thi-nghiem-be-tong-C30.pdf", "C30 concrete test report"),
        ("rebar-inspection", "Bien-ban-kiem-tra-cot-thep.pdf", "Rebar inspection record floor 2"),
        ("mep-duct-inspection", "Bien-ban-nghiem-thu-ong-gio.pdf", "MEP duct inspection record floor 1"),
        ("qs-measurement", "Bang-do-khoi-luong-thang-9.pdf", "Quantity measurement sheet September"),
    ];

    public static void Seed(AppDbContext db, string? webRootPath = null)
    {
        var now = DateTime.UtcNow;
        var project = db.OperationalProjects.FirstOrDefault(x => x.Code == "PJ-SAMPLE-003");
        if (project is null) return;

        var cm = ResolveUser(db, RbacRoleCodes.ProjectManager);
        if (cm is null) return;
        var site = ResolveUser(db, RbacRoleCodes.SiteCommander) ?? cm;
        var qa = ResolveUser(db, RbacRoleCodes.SiteQaQc) ?? site;
        var qs = ResolveUser(db, RbacRoleCodes.QuantitySurveyor) ?? site;
        var procurement = ResolveUser(db, RbacRoleCodes.Procurement) ?? qs;

        EnsureMember(db, project.Id, site.Id, "Chỉ huy trưởng công trường", cm.Id, now);
        EnsureMember(db, project.Id, qa.Id, "Kỹ sư QA/QC công trường", cm.Id, now);
        EnsureMember(db, project.Id, qs.Id, "QS công trường nhà máy", cm.Id, now);
        EnsureMember(db, project.Id, procurement.Id, "Cung ứng công trình nhà máy", cm.Id, now);
        db.SaveChanges();

        var documents = SeedEvidenceDocuments(db, project.Id, qa.Id, webRootPath, now);
        var boqLines = SeedBoqRevision(db, project, qs.Id, cm.Id, now);
        SeedPreparation(db, project.Id, cm.Id, documents, now);
        var templates = SeedQaQcTemplates(db, qa.Id, now);
        db.SaveChanges();
        SeedQaQcDossiers(db, project.Id, templates, documents, qa.Id, cm.Id, now);
        SeedQsBatches(db, project.Id, documents, qs.Id, cm.Id, now);
        SeedResourceRequests(db, project.Id, site.Id, procurement.Id, cm.Id, boqLines, now);
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

    /// <summary>
    /// Materialises the evidence library used by preparation, QA/QC and QS
    /// samples. Rows are matched by their stable storage path so re-running the
    /// seeder self-heals the database and the physical placeholder files.
    /// </summary>
    private static Dictionary<string, long> SeedEvidenceDocuments(
        AppDbContext db, int projectId, int actorId, string? webRootPath, DateTime now)
    {
        string? storageDirectory = null;
        if (!string.IsNullOrWhiteSpace(webRootPath))
        {
            var root = Path.GetFullPath(webRootPath);
            var candidate = Path.GetFullPath(Path.Combine(root, "files", "business-documents", "construction"));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (candidate.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                Directory.CreateDirectory(candidate);
                storageDirectory = candidate;
            }
        }

        var resolved = new List<(string Key, ProjectDocument Document)>();
        foreach (var sample in EvidenceSamples)
        {
            var localPath = $"{EvidenceRoot}/{sample.FileName}";
            var document = db.ProjectDocuments.FirstOrDefault(x =>
                x.OperationalProjectId == projectId && x.LocalPath == localPath && x.DeletedAt == null);

            var bytes = SampleCrmDataSeeder.BuildPlaceholderPdf(sample.Title);
            if (storageDirectory is not null)
            {
                var fullPath = Path.Combine(storageDirectory, sample.FileName);
                if (!File.Exists(fullPath)) File.WriteAllBytes(fullPath, bytes);
                bytes = File.ReadAllBytes(fullPath);
            }
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            if (document is null)
            {
                document = new ProjectDocument
                {
                    OperationalProjectId = projectId,
                    Category = ProjectDocumentCategory.ConstructionAcceptance,
                    SourceModule = ProjectDocumentSourceModule.Construction,
                    SourceType = ProjectDocumentSourceType.ManualUpload,
                    LocalPath = localPath,
                    OriginalFileName = sample.FileName,
                    ContentType = "application/pdf",
                    Size = bytes.Length,
                    Sha256 = sha256,
                    Origin = ProjectDocumentOrigin.Nicon,
                    DesiredOperation = ProjectDocumentDesiredOperation.None,
                    SyncStatus = ProjectDocumentSyncStatus.Synced,
                    IsDownloadable = true,
                    CreatedAt = now.AddDays(-36),
                    CreatedByUserId = actorId,
                    UpdatedAt = now.AddDays(-36),
                    UpdatedByUserId = actorId,
                };
                db.ProjectDocuments.Add(document);
            }
            else if (document.Size != bytes.Length || document.Sha256 != sha256)
            {
                document.Size = bytes.Length;
                document.Sha256 = sha256;
                document.UpdatedAt = now;
                document.UpdatedByUserId = actorId;
            }
            resolved.Add((sample.Key, document));
        }
        db.SaveChanges();
        return resolved.ToDictionary(x => x.Key, x => x.Document.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Seeds the approved construction BOQ revision that the BCH material
    /// requests and QS batches reference. Projects created without a BOQ would
    /// leave the resource picker empty and block the material handoff flow.
    /// </summary>
    private static Dictionary<string, ProjectBoqLine> SeedBoqRevision(
        AppDbContext db, OperationalProject project, int preparedByUserId, int approvedByUserId, DateTime now)
    {
        var revision = db.ProjectBoqRevisions.Include(x => x.Lines)
            .Where(x => x.OperationalProjectId == project.Id && x.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(x => x.RevisionNumber)
            .FirstOrDefault();
        if (revision is null)
        {
            var upstreamContractId = db.Contracts
                .Where(x => x.OperationalProjectId == project.Id && x.Direction == ContractDirection.Upstream &&
                    x.Type == ContractType.DesignAndBuild)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
            revision = new ProjectBoqRevision
            {
                OperationalProjectId = project.Id,
                UpstreamContractId = upstreamContractId,
                RevisionNumber = 1,
                Currency = "VND",
                Status = ProjectBoqRevisionStatus.Approved,
                PreparedByUserId = preparedByUserId,
                SubmittedByUserId = preparedByUserId,
                SubmittedAt = now.AddDays(-32),
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = now.AddDays(-30),
                FinalSelectedAt = now.AddDays(-30),
                CreatedAt = now.AddDays(-35),
                UpdatedAt = now.AddDays(-30),
                Lines =
                {
                    new ProjectBoqLine { ItemCode = "CT-01", Description = "Cốp pha sàn tầng 2", Unit = "m2", ApprovedQuantity = 1_200, BudgetUnitPrice = 420_000, Amount = 504_000_000, SortOrder = 0 },
                    new ProjectBoqLine { ItemCode = "CT-02", Description = "Cốt thép D16 cột và sàn", Unit = "tấn", ApprovedQuantity = 42, BudgetUnitPrice = 18_500_000, Amount = 777_000_000, SortOrder = 1 },
                    new ProjectBoqLine { ItemCode = "BT-01", Description = "Bê tông thương phẩm C30", Unit = "m3", ApprovedQuantity = 80, BudgetUnitPrice = 1_650_000, Amount = 132_000_000, SortOrder = 2 },
                    new ProjectBoqLine { ItemCode = "MEP-01", Description = "Ống gió tôn tráng kẽm", Unit = "m2", ApprovedQuantity = 86, BudgetUnitPrice = 420_000, Amount = 36_120_000, SortOrder = 3 },
                },
            };
            revision.CostTotal = revision.Lines.Sum(x => x.Amount);
            db.ProjectBoqRevisions.Add(revision);
            db.SaveChanges();
        }
        if (project.FinalProjectBoqRevisionId != revision.Id)
        {
            project.FinalProjectBoqRevisionId = revision.Id;
            db.SaveChanges();
        }
        return revision.Lines.ToDictionary(x => x.ItemCode, StringComparer.Ordinal);
    }

    private static void SeedPreparation(
        AppDbContext db, int projectId, int actorId, IReadOnlyDictionary<string, long> documents, DateTime now)
    {
        var item = db.ConstructionPreparations.FirstOrDefault(x =>
            x.OperationalProjectId == projectId && x.WbsBaselineReference == "WBS-BASELINE-ANPHU-R1");
        if (item is null)
        {
            item = new ConstructionPreparation
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
                SiteHandoverRequired = true,
                MethodStatementRequired = true,
                HsePlanRequired = true,
                PublishedAt = now.AddDays(-30),
                PublishedByUserId = actorId,
                CreatedByUserId = actorId,
                CreatedAt = now.AddDays(-32),
            };
            db.ConstructionPreparations.Add(item);
        }

        var needsEvidence = item.WbsBaselineDocumentId is null || item.ScheduleBaselineDocumentId is null ||
            item.SCurveBaselineDocumentId is null || item.BchOrganizationDocumentId is null ||
            item.ResourcePlanDocumentId is null || item.BudgetBaselineDocumentId is null ||
            item.SiteHandoverDocumentId is null || item.MethodStatementDocumentId is null ||
            item.HsePlanDocumentId is null;
        if (needsEvidence)
        {
            item.WbsBaselineDocumentId ??= documents["wbs-baseline"];
            item.ScheduleBaselineDocumentId ??= documents["schedule-baseline"];
            item.SCurveBaselineDocumentId ??= documents["s-curve-baseline"];
            item.BchOrganizationDocumentId ??= documents["bch-organization"];
            item.ResourcePlanDocumentId ??= documents["resource-plan"];
            item.BudgetBaselineDocumentId ??= documents["budget-baseline"];
            item.SiteHandoverDocumentId ??= documents["site-handover"];
            item.MethodStatementDocumentId ??= documents["method-statement"];
            item.HsePlanDocumentId ??= documents["hse-plan"];
            item.BudgetBaselineLocked = true;
            item.SiteHandoverComplete = true;
            item.MethodStatementApproved = true;
            item.HsePlanApproved = true;
            item.UpdatedAt = now;
            item.UpdatedByUserId = actorId;
        }
        db.SaveChanges();
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
                SchemaJson = """{"schemaVersion":1,"items":[{"code":"C1","title":"Cao độ và tim trục","acceptanceCriteria":"Đúng bản vẽ IFC đã phát hành"},{"code":"C2","title":"Độ kín của cốp pha","acceptanceCriteria":"Kín khít, không có vị trí gây rò rỉ vữa"},{"code":"C3","title":"Chủng loại và khoảng cách cốt thép","acceptanceCriteria":"Đúng bản vẽ IFC và biện pháp thi công đã duyệt"},{"code":"C4","title":"Độ sụt bê tông","acceptanceCriteria":"Nằm trong giới hạn của cấp phối được duyệt"},{"code":"C5","title":"Bảo dưỡng bê tông","acceptanceCriteria":"Đúng thời gian và phương án bảo dưỡng đã duyệt"}]}""",
            },
            new QaQcTemplate
            {
                Code = "QA-SAMPLE-MEP",
                Version = 1,
                NameVi = "Nghiệm thu ống gió và đường ống MEP",
                Name = "MEP ducting and piping inspection",
                NameZh = "MEP风管与管道验收",
                NameJa = "MEPダクト・配管検査",
                SchemaJson = """{"schemaVersion":1,"items":[{"code":"M1","title":"Cao độ treo ống gió","acceptanceCriteria":"Đúng bản vẽ IFC đã phát hành"},{"code":"M2","title":"Độ kín mối nối ống gió","acceptanceCriteria":"Mối nối kín và có keo làm kín theo biện pháp được duyệt"},{"code":"M3","title":"Thử kín đường ống nước","acceptanceCriteria":"Đạt áp lực và thời gian thử theo yêu cầu kỹ thuật"},{"code":"M4","title":"Tiếp địa thiết bị điện","acceptanceCriteria":"Đủ liên kết và đạt điện trở theo yêu cầu thiết kế"}]}""",
            },
        };
        var result = new List<QaQcTemplate>();
        foreach (var sample in samples)
        {
            var existing = db.QaQcTemplates.FirstOrDefault(x => x.Code == sample.Code && x.Version == sample.Version);
            if (existing is not null)
            {
                // Sample templates are owned by this seeder. Refresh their wording
                // and checklist so an existing development database reflects the
                // current sample; dossiers keep their stored template snapshot.
                if (existing.SchemaJson != sample.SchemaJson
                    || existing.NameVi != sample.NameVi || existing.Name != sample.Name
                    || existing.NameZh != sample.NameZh || existing.NameJa != sample.NameJa)
                {
                    existing.NameVi = sample.NameVi;
                    existing.Name = sample.Name;
                    existing.NameZh = sample.NameZh;
                    existing.NameJa = sample.NameJa;
                    existing.SchemaJson = sample.SchemaJson;
                }
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
        AppDbContext db, int projectId, List<QaQcTemplate> templates, IReadOnlyDictionary<string, long> documents,
        int authorId, int reviewerId, DateTime now)
    {
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
                Evidence =
                {
                    new QaQcDossierEvidence { ProjectDocumentId = documents["concrete-test"], EvidenceType = QaQcEvidenceType.LaboratoryResult },
                    new QaQcDossierEvidence { ProjectDocumentId = documents["rebar-inspection"], EvidenceType = QaQcEvidenceType.AcceptanceRecord },
                },
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
                Evidence =
                {
                    new QaQcDossierEvidence { ProjectDocumentId = documents["mep-duct-inspection"], EvidenceType = QaQcEvidenceType.AcceptanceRecord },
                },
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
            new QaQcDossier
            {
                Code = "QA-SAMPLE-005",
                QaQcTemplate = concrete,
                TemplateVersion = concrete.Version,
                TemplateSnapshotJson = concrete.SchemaJson,
                IfcReleaseId = ifcReleaseId,
                WbsCode = "WBS-3.1-FOUND",
                Title = "Nghiệm thu bê tông móng — trục 1–2",
                ChecklistResultsJson = """[{"code":"C1","result":"Pass"},{"code":"C2","result":"Pass"},{"code":"C3","result":"Pass","note":"Thép móng đã được nghiệm thu trước khi đổ"},{"code":"C4","result":"Pass","note":"Độ sụt 110 mm"},{"code":"C5","result":"NotApplicable","note":"Đang theo dõi bảo dưỡng, chưa đến hạn đánh giá"}]""",
                Status = ConstructionReviewStatus.PendingCmReview,
                SubmittedAt = now.AddHours(-6),
                Evidence =
                {
                    new QaQcDossierEvidence { ProjectDocumentId = documents["concrete-test"], EvidenceType = QaQcEvidenceType.LaboratoryResult },
                },
            },
        };

        var existing = db.QaQcDossiers.Include(x => x.Evidence)
            .Where(x => x.OperationalProjectId == projectId && x.Code.StartsWith("QA-SAMPLE-"))
            .ToList();

        foreach (var sample in samples)
        {
            sample.OperationalProjectId = projectId;
            sample.CreatedByUserId = authorId;
            sample.LastEditedByUserId = authorId;
            sample.CreatedAt = sample.SubmittedAt?.AddDays(-1) ?? now.AddDays(-1);
            sample.UpdatedAt = sample.CmReviewedAt ?? sample.SubmittedAt ?? sample.CreatedAt;

            var current = existing.FirstOrDefault(x => x.Code == sample.Code);
            if (current is null)
            {
                db.QaQcDossiers.Add(sample);
                continue;
            }
            foreach (var evidence in sample.Evidence)
            {
                if (current.Evidence.All(x => x.ProjectDocumentId != evidence.ProjectDocumentId))
                    current.Evidence.Add(new QaQcDossierEvidence
                    {
                        ProjectDocumentId = evidence.ProjectDocumentId,
                        EvidenceType = evidence.EvidenceType,
                    });
            }
        }
    }

    /// <summary>
    /// Covers every QS direction, review status, adjustment kind and record
    /// state: original, pending, draft, superseding correction and reversal.
    /// </summary>
    private static void SeedQsBatches(
        AppDbContext db, int projectId, IReadOnlyDictionary<string, long> documents,
        int authorId, int reviewerId, DateTime now)
    {
        var upstreamContract = db.Contracts
            .Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream)
            .OrderBy(x => x.Id)
            .FirstOrDefault();
        var downstreamContract = db.Contracts
            .Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Downstream)
            .OrderBy(x => x.Id)
            .FirstOrDefault();
        if (upstreamContract is null) return;

        var milestones = db.ContractPaymentMilestones
            .Where(x => x.ContractId == upstreamContract.Id)
            .OrderBy(x => x.Order)
            .Select(x => x.Id)
            .ToList();
        int? Milestone(int index) => index < milestones.Count ? milestones[index] : null;
        var firstMilestone = Milestone(0);
        var secondMilestone = Milestone(1);
        var boqRevisionId = db.ProjectBoqRevisions
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(x => x.RevisionNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

        var batches = db.ConstructionQsBatches.Include(x => x.Evidence)
            .Where(x => x.OperationalProjectId == projectId && x.Code.StartsWith("QS-SAMPLE-"))
            .ToList();
        var byCode = batches.ToDictionary(x => x.Code, StringComparer.Ordinal);

        ConstructionQsBatch Ensure(string code, Func<ConstructionQsBatch> build)
        {
            if (byCode.TryGetValue(code, out var current)) return current;
            var batch = build();
            batch.OperationalProjectId = projectId;
            batch.CertifiedAmount = batch.Lines.Sum(x => x.Amount);
            batch.CreatedByUserId = authorId;
            batch.LastEditedByUserId = authorId;
            batch.CreatedAt = now.AddDays(-15);
            batch.UpdatedAt = batch.CmReviewedAt ?? batch.SubmittedAt ?? batch.CreatedAt;
            db.ConstructionQsBatches.Add(batch);
            byCode[code] = batch;
            return batch;
        }

        void EnsureEvidence(ConstructionQsBatch batch, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (batch.Evidence.All(x => x.ProjectDocumentId != documents[key]))
                    batch.Evidence.Add(new ConstructionQsBatchEvidence { ProjectDocumentId = documents[key] });
            }
        }

        var qs001 = Ensure("QS-SAMPLE-001", () => new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-001",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.CmApproved,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = secondMilestone,
            ProjectBoqRevisionId = boqRevisionId,
            WbsCode = "WBS-3.1",
            PeriodFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Description = "Nghiệm thu khối lượng kết cấu tầng 1–2, đợt 1",
            SubmittedAt = now.AddDays(-12),
            CmReviewedAt = now.AddDays(-11),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Khối lượng khớp bản vẽ và biên bản hiện trường.",
            RecordState = ConstructionQsRecordState.Active,
            IsActiveLineage = true,
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-01", Description = "Cốp pha sàn tầng 2", Unit = "m2", Quantity = 120, UnitPrice = 850_000, Amount = 102_000_000, SortOrder = 1 },
                new ConstructionQsBatchLine { ItemCode = "CT-02", Description = "Bê tông sàn tầng 2", Unit = "m3", Quantity = 45, UnitPrice = 1_650_000, Amount = 74_250_000, SortOrder = 2 },
            },
        });
        EnsureEvidence(qs001, "qs-measurement");

        var qs002 = Ensure("QS-SAMPLE-002", () => new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-002",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.PendingCmReview,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = secondMilestone,
            ProjectBoqRevisionId = boqRevisionId,
            WbsCode = "WBS-3.2",
            PeriodFrom = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            Description = "Nghiệm thu khối lượng kết cấu tầng 3, đợt 2",
            SubmittedAt = now.AddDays(-1),
            RecordState = ConstructionQsRecordState.Active,
            IsActiveLineage = true,
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-03", Description = "Cốt thép cột tầng 3", Unit = "tấn", Quantity = 12.5m, UnitPrice = 18_500_000, Amount = 231_250_000, SortOrder = 1 },
            },
        });
        EnsureEvidence(qs002, "qs-measurement");

        if (downstreamContract is not null)
        {
            Ensure("QS-SAMPLE-003", () => new ConstructionQsBatch
            {
                Code = "QS-SAMPLE-003",
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.Draft,
                ContractId = downstreamContract.Id,
                ProjectBoqRevisionId = boqRevisionId,
                WbsCode = "WBS-3.4",
                PeriodFrom = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodTo = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                Description = "Khối lượng nhà thầu phụ MEP trình kỳ tháng 10",
                RecordState = ConstructionQsRecordState.Active,
                Lines =
                {
                    new ConstructionQsBatchLine { ItemCode = "MEP-01", Description = "Ống gió tầng 1", Unit = "m2", Quantity = 86, UnitPrice = 420_000, Amount = 36_120_000, SortOrder = 1 },
                },
            });
        }

        var qs005 = Ensure("QS-SAMPLE-005", () => new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-005",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.CmApproved,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = firstMilestone,
            ProjectBoqRevisionId = boqRevisionId,
            WbsCode = "WBS-3.3",
            PeriodFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Description = "Nghiệm thu khối lượng bê tông móng, đợt 0",
            SubmittedAt = now.AddDays(-20),
            CmReviewedAt = now.AddDays(-19),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Khối lượng móng khớp biên bản nghiệm thu.",
            RecordState = ConstructionQsRecordState.Active,
            IsActiveLineage = true,
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-05", Description = "Bê tông móng C30", Unit = "m3", Quantity = 30, UnitPrice = 1_650_000, Amount = 49_500_000, SortOrder = 1 },
            },
        });
        EnsureEvidence(qs005, "qs-measurement");

        if (downstreamContract is not null)
        {
            var qs007 = Ensure("QS-SAMPLE-007", () => new ConstructionQsBatch
            {
                Code = "QS-SAMPLE-007",
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmApproved,
                ContractId = downstreamContract.Id,
                ProjectBoqRevisionId = boqRevisionId,
                WbsCode = "WBS-3.4",
                PeriodFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                Description = "Khối lượng nhà thầu phụ MEP kỳ tháng 9",
                SubmittedAt = now.AddDays(-10),
                CmReviewedAt = now.AddDays(-9),
                CmReviewedByUserId = reviewerId,
                CmDecisionReason = "Khối lượng được xác nhận theo biên bản hiện trường.",
                RecordState = ConstructionQsRecordState.Active,
                IsActiveLineage = true,
                Lines =
                {
                    new ConstructionQsBatchLine { ItemCode = "MEP-02", Description = "Ống gió tầng 2", Unit = "m2", Quantity = 120, UnitPrice = 420_000, Amount = 50_400_000, SortOrder = 1 },
                },
            });
            EnsureEvidence(qs007, "qs-measurement");

            Ensure("QS-SAMPLE-008", () => new ConstructionQsBatch
            {
                Code = "QS-SAMPLE-008",
                Direction = ConstructionQsDirection.Downstream,
                Status = ConstructionReviewStatus.CmRejected,
                ContractId = downstreamContract.Id,
                ProjectBoqRevisionId = boqRevisionId,
                WbsCode = "WBS-3.4",
                PeriodFrom = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodTo = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                Description = "Khối lượng miệng gió nhà thầu phụ trình kỳ tháng 10",
                SubmittedAt = now.AddDays(-3),
                CmReviewedAt = now.AddDays(-2),
                CmReviewedByUserId = reviewerId,
                CmDecisionReason = "Khối lượng chưa khớp biên bản nghiệm thu hiện trường. Đề nghị đo lại.",
                RecordState = ConstructionQsRecordState.Active,
                Lines =
                {
                    new ConstructionQsBatchLine { ItemCode = "MEP-03", Description = "Miệng gió thổi", Unit = "bộ", Quantity = 24, UnitPrice = 650_000, Amount = 15_600_000, SortOrder = 1 },
                },
            });
        }

        db.SaveChanges();

        var qs004 = Ensure("QS-SAMPLE-004", () => new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-004",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.CmApproved,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = secondMilestone,
            ProjectBoqRevisionId = boqRevisionId,
            WbsCode = "WBS-3.1",
            PeriodFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Description = "Điều chỉnh khối lượng cốp pha sàn tầng 2 theo biên bản đo",
            SubmittedAt = now.AddDays(-7),
            CmReviewedAt = now.AddDays(-6),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Chấp thuận điều chỉnh tăng 5 m2 cốp pha theo biên bản đo hiện trường.",
            SupersedesBatchId = qs001.Id,
            AdjustmentKind = ConstructionQsAdjustmentKind.SupersedingCorrection,
            RecordState = ConstructionQsRecordState.Active,
            IsActiveLineage = true,
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-01", Description = "Cốp pha sàn tầng 2", Unit = "m2", Quantity = 125, UnitPrice = 850_000, Amount = 106_250_000, SortOrder = 1 },
                new ConstructionQsBatchLine { ItemCode = "CT-02", Description = "Bê tông sàn tầng 2", Unit = "m3", Quantity = 45, UnitPrice = 1_650_000, Amount = 74_250_000, SortOrder = 2 },
            },
        });
        EnsureEvidence(qs004, "qs-measurement");
        if (qs001.RecordState != ConstructionQsRecordState.Superseded)
        {
            qs001.RecordState = ConstructionQsRecordState.Superseded;
            qs001.IsActiveLineage = false;
            qs001.UpdatedAt = now.AddDays(-6);
        }

        Ensure("QS-SAMPLE-006", () => new ConstructionQsBatch
        {
            Code = "QS-SAMPLE-006",
            Direction = ConstructionQsDirection.Upstream,
            Status = ConstructionReviewStatus.CmApproved,
            ContractId = upstreamContract.Id,
            ContractPaymentMilestoneId = firstMilestone,
            ProjectBoqRevisionId = boqRevisionId,
            WbsCode = "WBS-3.3",
            PeriodFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            Description = "Đảo toàn bộ lô nghiệm thu bê tông móng do sai khối lượng",
            SubmittedAt = now.AddDays(-16),
            CmReviewedAt = now.AddDays(-15),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Đảo lô do biên bản nghiệm thu móng bị thay thế.",
            ReversesBatchId = qs005.Id,
            AdjustmentKind = ConstructionQsAdjustmentKind.Reversal,
            RecordState = ConstructionQsRecordState.Active,
            IsActiveLineage = true,
            Lines =
            {
                new ConstructionQsBatchLine { ItemCode = "CT-05", Description = "Bê tông móng C30", Unit = "m3", Quantity = 30, UnitPrice = 1_650_000, Amount = 49_500_000, SortOrder = 1 },
            },
        });
        if (qs005.RecordState != ConstructionQsRecordState.Reversed)
        {
            qs005.RecordState = ConstructionQsRecordState.Reversed;
            qs005.IsActiveLineage = false;
            qs005.UpdatedAt = now.AddDays(-15);
        }
    }

    private static void SeedResourceRequests(
        AppDbContext db, int projectId, int siteUserId, int procurementUserId, int reviewerId,
        IReadOnlyDictionary<string, ProjectBoqLine> boqLines, DateTime now)
    {
        MaterialRequest? ductMaterialRequest = null;
        if (boqLines.TryGetValue("MEP-01", out var handoffDuctLine))
        {
            ductMaterialRequest = db.MaterialRequests
                .FirstOrDefault(x => x.OperationalProjectId == projectId && x.Code == "MR-SAMPLE-ANPHU-DUCT");
            if (ductMaterialRequest is null)
            {
                ductMaterialRequest = new MaterialRequest
                {
                    OperationalProjectId = projectId,
                    Code = "MR-SAMPLE-ANPHU-DUCT",
                    Status = MaterialRequestStatus.Submitted,
                    SiteRequesterUserId = siteUserId,
                    ResponsibleSiteUserId = siteUserId,
                    AssignedProcurementUserId = procurementUserId,
                    RequiredAt = now.Date.AddDays(10),
                    Note = "Tạo tự động khi CM duyệt yêu cầu nguồn lực RR-SAMPLE-ANPHU-DUCT.",
                    SubmittedAt = now.AddDays(-5),
                    CreatedAt = now.AddDays(-6),
                    UpdatedAt = now.AddDays(-5),
                    Lines =
                    {
                        new MaterialRequestLine { ProjectBoqLineId = handoffDuctLine.Id, RequestedQuantity = 86, SortOrder = 0 },
                    },
                };
                db.MaterialRequests.Add(ductMaterialRequest);
                db.SaveChanges();
            }
        }

        var samples = new List<ConstructionResourceRequest>();
        if (boqLines.TryGetValue("CT-02", out var rebarLine))
            samples.Add(new ConstructionResourceRequest
            {
                Code = "RR-SAMPLE-ANPHU-REBAR",
                Type = ConstructionResourceType.Material,
                Status = ConstructionReviewStatus.Draft,
                WbsCode = "WBS-3.1-COL",
                Quantity = 12.5m,
                Unit = "tấn",
                Reason = "Bổ sung thép D16 cho cột tầng 3 theo biên bản kiểm tra cốt thép.",
                ProjectBoqLineId = rebarLine.Id,
                HandoffStatus = ConstructionHandoffStatus.NotReady,
            });
        if (boqLines.TryGetValue("BT-01", out var concreteLine))
            samples.Add(new ConstructionResourceRequest
            {
                Code = "RR-SAMPLE-ANPHU-CONCRETE",
                Type = ConstructionResourceType.Material,
                Status = ConstructionReviewStatus.PendingCmReview,
                WbsCode = "WBS-3.1-FORM",
                Quantity = 45,
                Unit = "m3",
                Reason = "Bê tông C30 cho sàn tầng 2, đợt đổ tiếp theo.",
                ProjectBoqLineId = concreteLine.Id,
                SubmittedAt = now.AddDays(-1),
                HandoffStatus = ConstructionHandoffStatus.NotReady,
            });
        if (boqLines.TryGetValue("MEP-01", out var ductLine))
            samples.Add(new ConstructionResourceRequest
            {
                Code = "RR-SAMPLE-ANPHU-DUCT",
                Type = ConstructionResourceType.Material,
                Status = ConstructionReviewStatus.CmApproved,
                WbsCode = "WBS-3.4-MEP",
                Quantity = 86,
                Unit = "m2",
                Reason = "Ống gió tôn tráng kẽm cho tầng 1 khu vực xưởng may.",
                ProjectBoqLineId = ductLine.Id,
                SubmittedAt = now.AddDays(-6),
                CmReviewedAt = now.AddDays(-5),
                CmReviewedByUserId = reviewerId,
                CmDecisionReason = "Nhu cầu khớp BOQ và tiến độ MEP tầng 1.",
                HandoffStatus = ConstructionHandoffStatus.MaterialRequestCreated,
                HandoffEntityType = "MaterialRequest",
                HandoffEntityId = ductMaterialRequest?.Id,
            });
        samples.Add(new ConstructionResourceRequest
        {
            Code = "RR-SAMPLE-ANPHU-SCAFFOLD",
            Type = ConstructionResourceType.Equipment,
            Status = ConstructionReviewStatus.CmRejected,
            WbsCode = "WBS-3.1-FORM",
            Quantity = 24,
            Unit = "bộ",
            Reason = "Giàn giáo di động phục vụ thi công sàn tầng 2.",
            SubmittedAt = now.AddDays(-4),
            CmReviewedAt = now.AddDays(-3),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Chưa có biện pháp an toàn và chứng nhận kiểm định giàn giáo. Bổ sung trước khi trình lại.",
            HandoffStatus = ConstructionHandoffStatus.NotReady,
        });
        samples.Add(new ConstructionResourceRequest
        {
            Code = "RR-SAMPLE-ANPHU-STEELCREW",
            Type = ConstructionResourceType.Labour,
            Status = ConstructionReviewStatus.CmApproved,
            WbsCode = "WBS-3.1-COL",
            Quantity = 8,
            Unit = "người",
            Reason = "Tổ thép bổ sung cho cột tầng 3 để bám tiến độ.",
            SubmittedAt = now.AddDays(-3),
            CmReviewedAt = now.AddDays(-2),
            CmReviewedByUserId = reviewerId,
            CmDecisionReason = "Đã đối chiếu kế hoạch nguồn lực và biện pháp thi công.",
            HandoffStatus = ConstructionHandoffStatus.ReadyForProcurementAndFinance,
        });
        samples.Add(new ConstructionResourceRequest
        {
            Code = "RR-SAMPLE-ANPHU-EXCAVATOR",
            Type = ConstructionResourceType.Equipment,
            Status = ConstructionReviewStatus.Draft,
            WbsCode = "WBS-3.2",
            Quantity = 1,
            Unit = "bộ",
            Reason = "Máy đào phục vụ san lấp khu vực mở rộng nhà xưởng.",
            HandoffStatus = ConstructionHandoffStatus.NotReady,
        });

        var added = new List<ConstructionResourceRequest>();
        foreach (var sample in samples)
        {
            if (db.ConstructionResourceRequests.Any(x => x.OperationalProjectId == projectId && x.Code == sample.Code)) continue;
            sample.OperationalProjectId = projectId;
            sample.NeedByDate = now.Date.AddDays(14);
            sample.ResponsibleSiteUserId = siteUserId;
            sample.AssignedProcurementUserId = procurementUserId;
            sample.CreatedByUserId = siteUserId;
            sample.LastEditedByUserId = siteUserId;
            sample.CreatedAt = sample.SubmittedAt?.AddDays(-1) ?? now.AddDays(-2);
            sample.UpdatedAt = sample.CmReviewedAt ?? sample.SubmittedAt ?? sample.CreatedAt;
            db.ConstructionResourceRequests.Add(sample);
            added.Add(sample);
        }
        db.SaveChanges();
        foreach (var request in added.Where(x =>
                     x.HandoffStatus == ConstructionHandoffStatus.ReadyForProcurementAndFinance &&
                     x.HandoffEntityId is null))
        {
            request.HandoffEntityType = nameof(ConstructionResourceRequest);
            request.HandoffEntityId = request.Id;
        }
        db.SaveChanges();
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
