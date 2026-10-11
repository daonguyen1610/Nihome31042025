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
        const string code = "WP-DEMO-DESIGN";
        if (db.DepartmentWorkPackages.Any(item => item.Code == code)) return;
        var project = db.OperationalProjects.OrderBy(item => item.Id).FirstOrDefault();
        var groupId = db.RoleGroups.Where(item => item.Code == "DESIGN" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "DESIGN_LEAD");
        var assigneeId = ActiveUserForRole(db, "ARCHITECT");
        if (project is null || groupId == 0 || leadId == 0 || assigneeId == 0) return;

        var start = DateTime.UtcNow.Date.AddDays(-2);
        db.DepartmentWorkPackages.Add(new DepartmentWorkPackage
        {
            Code = code,
            Kind = WorkPackageKind.Project,
            Name = "Triển khai hồ sơ thiết kế kỹ thuật",
            Description = "Nhóm việc mẫu nhận thông tin từ dự án chung và được phòng Thiết kế phân công nội bộ.",
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
            Tasks =
            [
                new DepartmentWorkTask
                {
                    WbsCode = "DES-01",
                    Title = "Hoàn thiện mặt bằng kiến trúc",
                    Description = "Rà soát yêu cầu khách hàng và phát hành hồ sơ để trưởng nhóm kiểm tra.",
                    Resources = "Bản vẽ khảo sát, biên bản yêu cầu khách hàng",
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
        var groupId = db.RoleGroups.Where(item => item.Code == "HR_ADMIN" && item.IsActive)
            .Select(item => item.Id).FirstOrDefault();
        var leadId = ActiveUserForRole(db, "LEGAL_OFFICER");
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
            Tasks =
            [
                new DepartmentWorkTask
                {
                    WbsCode = "INT-TRN-01",
                    Title = "Chuẩn bị nội dung đào tạo",
                    Description = "Tổng hợp luồng giao nhận công việc và tình huống xử lý mẫu.",
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
