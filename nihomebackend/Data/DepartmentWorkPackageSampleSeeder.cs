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
        SeedInternalPackage(db, actorId);
        db.SaveChanges();
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

    private static int ActiveUserForRole(AppDbContext db, string roleCode) =>
        db.Users.Where(user => user.IsActive && user.RoleEntity != null && user.RoleEntity.Code == roleCode)
            .OrderBy(user => user.Id).Select(user => user.Id).FirstOrDefault();
}
