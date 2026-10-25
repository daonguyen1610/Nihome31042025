using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>Development samples that demonstrate project-linked and internal departmental work.</summary>
public static class DepartmentWorkPackageSampleSeeder
{
    public static void Seed(AppDbContext db)
    {
        var actorId = db.Users.Where(user => user.IsActive).OrderBy(user => user.Id)
            .Select(user => user.Id).FirstOrDefault();
        if (actorId == 0) return;

        SeedProjectPackage(db, actorId);
        SeedPrecontractProcurementPackage(db, actorId);
        SeedCommercialFinanceReview(db, actorId);
        SeedTenderPlanningPackage(db, actorId);
        SeedInternalPackage(db, actorId);
        db.SaveChanges();
    }

    private static void SeedTenderPlanningPackage(AppDbContext db, int actorId)
    {
        const string code = "WP-DEMO-TENDER-PLAN";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;

        var tender = db.Tenders.OrderBy(item => item.Id)
            .FirstOrDefault(item => item.Code == "TD-SAMPLE-001" &&
                item.Status == TenderStatus.Preparing && item.OperationalProjectId != null);
        if (tender is null || tender.SubmissionDeadline <= DateTime.UtcNow.AddDays(3)) return;

        var leadCandidates = db.Users
            .Where(user => user.IsActive && user.RoleEntity != null && user.RoleEntity.IsActive &&
                user.RoleEntity.RoleGroupId != null && user.RoleEntity.RolePermissions.Any(rolePermission =>
                    rolePermission.Permission.IsActive &&
                    rolePermission.Permission.Module == "work.packages" &&
                    rolePermission.Permission.Action == "assign"))
            .OrderBy(user => user.Id)
            .Select(user => new { UserId = user.Id, GroupId = user.RoleEntity!.RoleGroupId!.Value })
            .ToList();
        var selected = leadCandidates
            .Select(candidate => new
            {
                LeadId = candidate.UserId,
                candidate.GroupId,
                AssigneeId = db.Users.Where(user => user.IsActive && user.Id != candidate.UserId &&
                        user.RoleEntity != null && user.RoleEntity.IsActive &&
                        user.RoleEntity.RoleGroupId == candidate.GroupId)
                    .OrderBy(user => user.Id).Select(user => user.Id).FirstOrDefault(),
            })
            .FirstOrDefault(candidate => candidate.AssigneeId != 0);
        if (selected is null) return;

        var start = tender.SubmissionDeadline.Date.AddDays(-21);
        var end = tender.SubmissionDeadline.Date.AddDays(-3);
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Project,
            Purpose = WorkPackagePurpose.TenderPlanning,
            Name = "Chuẩn bị hồ sơ pháp lý và năng lực dự thầu",
            Description = "Nhóm hồ sơ mẫu trong kế hoạch đấu thầu NICON, có người phụ trách, hạn nội bộ và bước duyệt độc lập.",
            OperationalProjectId = tender.OperationalProjectId,
            SourceTenderId = tender.Id,
            DepartmentRoleGroupId = selected.GroupId,
            LeadUserId = selected.LeadId,
            Status = WorkPackageStatus.Active,
            PlannedStart = start,
            PlannedEnd = end,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actorId,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = actorId,
            ParticipantGroups =
            [
                new DepartmentWorkPackageParticipantGroup { RoleGroupId = selected.GroupId },
            ],
            Tasks =
            [
                new DepartmentWorkTask
                {
                    WbsCode = "TD-PL-01",
                    Title = "Rà soát hồ sơ pháp lý và năng lực dùng chung",
                    Description = "Đối chiếu hồ sơ năng lực dùng chung với yêu cầu của hồ sơ mời thầu và ghi nhận phần cần bổ sung.",
                    Resources = "Hồ sơ mời thầu, thư viện hồ sơ năng lực dùng chung",
                    DepartmentRoleGroupId = selected.GroupId,
                    AssigneeUserId = selected.AssigneeId,
                    ApproverUserId = selected.LeadId,
                    Status = WorkTaskStatus.InProgress,
                    ProgressPercent = 40,
                    PlannedStart = start,
                    PlannedEnd = start.AddDays(7),
                    ActualStart = start,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = selected.LeadId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = selected.AssigneeId,
                },
            ],
        });
    }

    private static void SeedProjectPackage(AppDbContext db, int actorId)
    {
        const string code = "WP-DEMO-SUPPLY";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;
        var project = db.OperationalProjects.OrderBy(item => item.Id).FirstOrDefault();
        var groupId = db.RoleGroups.Where(item => item.Code == "PROCUREMENT" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "PROCUREMENT");
        var assigneeId = ActiveUserForRole(db, "WAREHOUSE");
        if (project is null || groupId == 0 || leadId == 0 || assigneeId == 0) return;

        var start = DateTime.UtcNow.Date.AddDays(-2);
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Project,
            Name = "Chuẩn bị vật tư phục vụ thi công",
            Description = "Nhóm việc mẫu nhận thông tin từ dự án chung và được phòng Cung ứng phân công nội bộ.",
            OperationalProjectId = project.Id,
            ContractId = db.Contracts.Where(item => item.OperationalProjectId == project.Id)
                .OrderBy(item => item.Id).Select(item => (int?)item.Id).FirstOrDefault(),
            DepartmentRoleGroupId = groupId,
            LeadUserId = leadId,
            Status = WorkPackageStatus.Active,
            PlannedStart = start,
            PlannedEnd = start.AddDays(21),
            CreatedAt = start,
            CreatedByUserId = actorId,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = actorId,
            ParticipantGroups =
            [
                new DepartmentWorkPackageParticipantGroup { RoleGroupId = groupId },
            ],
            Tasks =
            [
                new DepartmentWorkTask
                {
                    WbsCode = "SUP-01",
                    Title = "Lập kế hoạch cung ứng vật tư",
                    Description = "Đối chiếu nhu cầu vật tư với tiến độ và BOQ thi công đã được phê duyệt.",
                    Resources = "Tiến độ thi công, BOQ thi công được duyệt, danh sách nhà cung cấp",
                    DepartmentRoleGroupId = groupId,
                    AssigneeUserId = assigneeId,
                    ApproverUserId = leadId,
                    Status = WorkTaskStatus.InProgress,
                    ProgressPercent = 35,
                    PlannedStart = start,
                    PlannedEnd = start.AddDays(7),
                    ActualStart = start,
                    CreatedAt = start,
                    CreatedByUserId = leadId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = assigneeId,
                },
            ],
        });
    }

    private static void SeedInternalPackage(AppDbContext db, int actorId)
    {
        const string code = "WP-DEMO-TRAINING";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;
        var groupId = db.RoleGroups.Where(item => item.Code == "FINANCE" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var procurementGroupId = db.RoleGroups.Where(item => item.Code == "PROCUREMENT" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var crmGroupId = db.RoleGroups.Where(item => item.Code == "CRM" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "ACCOUNTANT");
        if (groupId == 0 || leadId == 0) return;

        var start = DateTime.UtcNow.Date.AddDays(3);
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Internal,
            Name = "Đào tạo quy trình phối hợp liên phòng",
            Description = "Công việc nội bộ không gắn khách hàng, hợp đồng hoặc dự án vận hành.",
            DepartmentRoleGroupId = groupId,
            LeadUserId = leadId,
            Status = WorkPackageStatus.Planned,
            PlannedStart = start,
            PlannedEnd = start.AddDays(5),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = leadId,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = leadId,
            ParticipantGroups = new[] { groupId, procurementGroupId, crmGroupId }
                .Where(id => id > 0).Distinct()
                .Select(id => new DepartmentWorkPackageParticipantGroup { RoleGroupId = id }).ToList(),
            Tasks =
            [
                new DepartmentWorkTask
                {
                    WbsCode = "INT-TRN-01",
                    Title = "Chuẩn bị nội dung đào tạo",
                    Description = "Tổng hợp luồng giao nhận công việc và tình huống xử lý mẫu.",
                    DepartmentRoleGroupId = groupId,
                    AssigneeUserId = leadId,
                    Status = WorkTaskStatus.Planned,
                    ProgressPercent = 0,
                    PlannedStart = start,
                    PlannedEnd = start.AddDays(2),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = leadId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = leadId,
                },
            ],
        });
    }

    private static void SeedPrecontractProcurementPackage(AppDbContext db, int actorId)
    {
        const string code = "WP-DEMO-INPUT-PRICE";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;
        var quote = db.Quotes.Where(item => item.OperationalProjectId != null)
            .OrderBy(item => item.Id).FirstOrDefault();
        if (quote?.OperationalProjectId is null) return;
        var groupId = db.RoleGroups.Where(item => item.Code == "PROCUREMENT" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "PROCUREMENT");
        var vendor = db.Vendors.Where(item => item.IsActive).OrderBy(item => item.Id).FirstOrDefault();
        if (groupId == 0 || leadId == 0 || vendor is null) return;

        var now = DateTime.UtcNow;
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Project,
            Purpose = WorkPackagePurpose.ProcurementInputPricing,
            Name = $"Cung ứng giá đầu vào cho báo giá {quote.Code}",
            Description = "Thu thập và duyệt giá nhà cung cấp/thầu phụ trước khi Kinh doanh lập báo giá đầu ra.",
            OperationalProjectId = quote.OperationalProjectId,
            SourceOpportunityId = quote.OpportunityId,
            SourceQuoteId = quote.Id,
            DepartmentRoleGroupId = groupId,
            LeadUserId = leadId,
            Status = WorkPackageStatus.Active,
            CreatedAt = now.AddDays(-5),
            CreatedByUserId = actorId,
            UpdatedAt = now,
            UpdatedByUserId = leadId,
            ParticipantGroups = [new DepartmentWorkPackageParticipantGroup { RoleGroupId = groupId }],
            InputPriceOffers =
            [
                new ProcurementInputPriceOffer
                {
                    VendorId = vendor.Id,
                    SupplierReference = "NCC-NICON-2026-042",
                    Currency = "VND",
                    ValidUntil = now.AddDays(25),
                    Status = InputPriceOfferStatus.Approved,
                    Note = "Giá giao đến công trình, chưa bao gồm chi phí lưu kho.",
                    SubmittedAt = now.AddDays(-3),
                    ApprovedAt = now.AddDays(-2),
                    ApprovedByUserId = leadId,
                    CreatedAt = now.AddDays(-4),
                    CreatedByUserId = actorId,
                    UpdatedAt = now.AddDays(-2),
                    UpdatedByUserId = leadId,
                    Lines =
                    [
                        new ProcurementInputPriceLine { ItemCode = "MEP-CABLE-01", Description = "Cáp điện chống cháy", Unit = "m", Quantity = 1200, UnitPrice = 85000, VatPercent = 10, Amount = 102000000, SortOrder = 1 },
                        new ProcurementInputPriceLine { ItemCode = "MEP-TRAY-01", Description = "Máng cáp mạ kẽm", Unit = "m", Quantity = 350, UnitPrice = 240000, VatPercent = 10, Amount = 84000000, SortOrder = 2 },
                    ],
                },
                new ProcurementInputPriceOffer
                {
                    VendorId = vendor.Id,
                    SupplierReference = "NCC-NICON-2026-043",
                    Currency = "VND",
                    ValidUntil = now.AddDays(25),
                    Status = InputPriceOfferStatus.Draft,
                    Note = "Phương án cáp và máng cáp thay thế đang được Cung ứng hoàn thiện trước khi gửi duyệt.",
                    CreatedAt = now.AddDays(-1),
                    CreatedByUserId = actorId,
                    UpdatedAt = now.AddDays(-1),
                    UpdatedByUserId = actorId,
                    Lines =
                    [
                        new ProcurementInputPriceLine { ItemCode = "MEP-CABLE-ALT-01", Description = "Cáp chống cháy phương án thay thế", Unit = "m", Quantity = 1200, UnitPrice = 82000, VatPercent = 10, Amount = 98400000, SortOrder = 1 },
                    ],
                },
            ],
        });
    }

    private static void SeedCommercialFinanceReview(AppDbContext db, int actorId)
    {
        const string code = "WP-DEMO-FIN-COMMERCIAL";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;
        var quote = db.Quotes.Where(item => item.OperationalProjectId != null)
            .OrderBy(item => item.Id).FirstOrDefault();
        var groupId = db.RoleGroups.Where(item => item.Code == "FINANCE" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "ACCOUNTANT");
        if (quote?.OperationalProjectId is null || groupId == 0 || leadId == 0) return;

        var now = DateTime.UtcNow;
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Project,
            Purpose = WorkPackagePurpose.FinanceCommercialReview,
            Name = "Rà soát điều kiện thương mại trước khi gửi báo giá",
            Description = "Tài chính – Kế toán kiểm tra điều kiện hợp đồng, bảo lãnh và thanh toán độc lập với người lập.",
            OperationalProjectId = quote.OperationalProjectId,
            SourceOpportunityId = quote.OpportunityId,
            SourceQuoteId = quote.Id,
            DepartmentRoleGroupId = groupId,
            LeadUserId = leadId,
            Status = WorkPackageStatus.Active,
            CreatedAt = now.AddDays(-3),
            CreatedByUserId = actorId,
            UpdatedAt = now,
            UpdatedByUserId = leadId,
            ParticipantGroups = [new DepartmentWorkPackageParticipantGroup { RoleGroupId = groupId }],
            FinanceReview = new DepartmentFinanceReview
            {
                Status = FinanceReviewStatus.Approved,
                ContractConditionsReviewed = true,
                GuaranteesReviewed = true,
                PaymentTermsReviewed = true,
                FinancialDossierReviewed = false,
                Findings = "Điều kiện tạm ứng và bảo lãnh đã phù hợp hồ sơ thương mại.",
                SubmittedAt = now.AddDays(-2),
                ReviewedAt = now.AddDays(-1),
                ReviewedByUserId = leadId,
                CreatedAt = now.AddDays(-3),
                CreatedByUserId = actorId,
                UpdatedAt = now.AddDays(-1),
                UpdatedByUserId = leadId,
            },
        });
    }

    private static int ActiveUserForRole(AppDbContext db, string roleCode) =>
        db.Users.Where(user => user.IsActive && user.RoleEntity != null && user.RoleEntity.Code == roleCode)
            .OrderBy(user => user.Id).Select(user => user.Id).FirstOrDefault();
}
