using System.Text.Json;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Data;

public static class LegalChecklistTemplateSampleSeeder
{
    public static void Seed(AppDbContext db)
    {
        const string code = "DEMO-LEGAL-INDUSTRIAL";
        if (db.LegalChecklistTemplates.Any(x => x.Code == code)) return;
        var author = db.Users.Where(x => x.IsActive && x.RoleEntity != null && x.RoleEntity.Code == "LEGAL_OFFICER")
            .OrderBy(x => x.Id).FirstOrDefault();
        if (author is null || !db.MasterDataOptions.Any(x => x.Category == "permit_type" && x.Code == "gpxd" && x.IsActive)) return;
        db.LegalChecklistTemplates.Add(new LegalChecklistTemplate
        {
            Code = code,
            CreatedByUserId = author.Id,
            UpdatedByUserId = author.Id,
            NameVi = "Mẫu minh họa hồ sơ xây dựng nhà máy — chưa phê duyệt",
            Name = "Factory construction dossier example — not approved",
            NameZh = "工厂建设资料示例 — 未审批",
            NameJa = "工場建設書類の例 — 未承認",
            ScopeVi = "Dữ liệu demo cho Pháp lý soạn và CM kiểm tra. Không phải danh mục NICON đã duyệt; không được áp dụng khi còn nháp.",
            Scope = "Demo for Legal authoring and CM review. This is not a NICON-approved catalogue and cannot be applied as a draft.",
            ScopeZh = "供法务起草及CM审查的演示数据。并非NICON批准目录，草稿不可应用。",
            ScopeJa = "法務作成・CM確認用デモ。NICON承認済み一覧ではなく、下書きのまま適用できません。",
            ItemsJson = JsonSerializer.Serialize(new[] { new LegalChecklistTemplateItem { PermitTypeCode = "gpxd", IsRequired = true } }),
        });
        db.SaveChanges();
    }
}
