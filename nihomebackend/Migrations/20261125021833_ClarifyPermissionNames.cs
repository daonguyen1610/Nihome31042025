using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// NICON could not tell what some permissions do (feedback row 19): website and project
    /// rights shared a name, "nhật ký kiểm toán" read as a financial audit, and QA/QC, QS, HSE,
    /// IFC, RFQ went unexplained. The seeder only inserts keys, so existing rows are renamed
    /// here, and only where they still hold the seeded wording: wording an administrator
    /// changed in /admin/translations is kept.
    /// </summary>
    public partial class ClarifyPermissionNames : Migration
    {
        private static readonly (string Key, string Language, string Old, string New)[] Labels =
        [
            ("rbac.perm.proc.ifc.view.label", "vi", @"Xem hồ sơ IFC được gửi", @"Xem bản vẽ thi công (IFC) được gửi"),
            ("rbac.perm.proc.ifc.acknowledge.label", "vi", @"Xác nhận nhận IFC", @"Xác nhận đã nhận bản vẽ thi công (IFC)"),
            ("rbac.perm.construction.resource-request.cm-review.label", "vi", @"CM duyệt đề xuất nguồn lực", @"Phòng CM duyệt đề xuất nguồn lực"),
            ("rbac.perm.construction.qa-qc.view.label", "vi", @"Xem hồ sơ QA/QC", @"Xem hồ sơ chất lượng (QA/QC)"),
            ("rbac.perm.construction.qa-qc.create.label", "vi", @"Lập hồ sơ QA/QC", @"Lập hồ sơ chất lượng (QA/QC)"),
            ("rbac.perm.construction.qa-qc.cm-review.label", "vi", @"CM duyệt hồ sơ QA/QC", @"Phòng CM duyệt hồ sơ chất lượng (QA/QC)"),
            ("rbac.perm.construction.qa-qc.template-manage.label", "vi", @"Quản lý mẫu QA/QC", @"Quản lý mẫu hồ sơ chất lượng (QA/QC)"),
            ("rbac.perm.construction.qs.view.label", "vi", @"Xem hồ sơ QS", @"Xem hồ sơ khối lượng (QS)"),
            ("rbac.perm.construction.qs.create.label", "vi", @"Lập hồ sơ QS", @"Lập hồ sơ khối lượng (QS)"),
            ("rbac.perm.construction.qs.cm-review.label", "vi", @"CM duyệt hồ sơ QS", @"Phòng CM duyệt hồ sơ khối lượng (QS)"),
            ("rbac.perm.dashboard.view.label", "vi", @"Xem Dashboard", @"Xem trang tổng quan"),
            ("rbac.perm.system.audit.view.label", "vi", @"Xem nhật ký kiểm toán", @"Xem nhật ký hoạt động hệ thống"),
            ("rbac.perm.system.audit.view.label", "en", @"View audit log", @"View system activity log"),
            ("rbac.perm.system.audit.view.label", "zh", @"查看审计日志", @"查看系统操作日志"),
            ("rbac.perm.system.audit.view.label", "ja", @"監査ログを表示", @"システム操作ログを表示"),
            ("rbac.perm.system.audit.manage.label", "vi", @"Quản lý nhật ký kiểm toán", @"Quản lý nhật ký hoạt động hệ thống"),
            ("rbac.perm.system.audit.manage.label", "en", @"Manage audit log", @"Manage system activity log"),
            ("rbac.perm.system.audit.manage.label", "zh", @"管理审计日志", @"管理系统操作日志"),
            ("rbac.perm.system.audit.manage.label", "ja", @"監査ログを管理", @"システム操作ログを管理"),
            ("rbac.perm.content.activities.view.label", "vi", @"Xem hoạt động", @"Xem bài hoạt động trên website"),
            ("rbac.perm.content.activities.view.label", "en", @"View activities", @"View website activity posts"),
            ("rbac.perm.content.activities.view.label", "zh", @"查看活动", @"查看网站活动文章"),
            ("rbac.perm.content.activities.view.label", "ja", @"アクティビティを表示", @"ウェブサイトの活動記事を表示"),
            ("rbac.perm.content.activities.manage.label", "vi", @"Quản lý hoạt động", @"Quản lý bài hoạt động trên website"),
            ("rbac.perm.content.activities.manage.label", "en", @"Manage activities", @"Manage website activity posts"),
            ("rbac.perm.content.activities.manage.label", "zh", @"管理活动", @"管理网站活动文章"),
            ("rbac.perm.content.activities.manage.label", "ja", @"アクティビティを管理", @"ウェブサイトの活動記事を管理"),
            ("rbac.perm.content.activity-categories.view.label", "vi", @"Xem danh mục hoạt động", @"Xem danh mục bài hoạt động trên website"),
            ("rbac.perm.content.activity-categories.view.label", "en", @"View activity categories", @"View website activity categories"),
            ("rbac.perm.content.activity-categories.view.label", "zh", @"查看活动分类", @"查看网站活动分类"),
            ("rbac.perm.content.activity-categories.view.label", "ja", @"アクティビティのカテゴリを表示", @"ウェブサイトの活動カテゴリを表示"),
            ("rbac.perm.content.activity-categories.manage.label", "vi", @"Quản lý danh mục hoạt động", @"Quản lý danh mục bài hoạt động trên website"),
            ("rbac.perm.content.activity-categories.manage.label", "en", @"Manage activity categories", @"Manage website activity categories"),
            ("rbac.perm.content.activity-categories.manage.label", "zh", @"管理活动分类", @"管理网站活动分类"),
            ("rbac.perm.content.activity-categories.manage.label", "ja", @"アクティビティのカテゴリを管理", @"ウェブサイトの活動カテゴリを管理"),
            ("rbac.perm.content.projects.view.label", "vi", @"Xem dự án", @"Xem dự án trên website"),
            ("rbac.perm.content.projects.view.label", "en", @"View projects", @"View website projects"),
            ("rbac.perm.content.projects.view.label", "zh", @"查看项目", @"查看网站项目"),
            ("rbac.perm.content.projects.view.label", "ja", @"プロジェクトを表示", @"ウェブサイトのプロジェクトを表示"),
            ("rbac.perm.content.projects.manage.label", "vi", @"Quản lý dự án", @"Quản lý dự án trên website"),
            ("rbac.perm.content.projects.manage.label", "en", @"Manage projects", @"Manage website projects"),
            ("rbac.perm.content.projects.manage.label", "zh", @"管理项目", @"管理网站项目"),
            ("rbac.perm.content.projects.manage.label", "ja", @"プロジェクトを管理", @"ウェブサイトのプロジェクトを管理"),
            ("rbac.perm.content.project-categories.view.label", "vi", @"Xem danh mục dự án", @"Xem danh mục dự án trên website"),
            ("rbac.perm.content.project-categories.view.label", "en", @"View project categories", @"View website project categories"),
            ("rbac.perm.content.project-categories.view.label", "zh", @"查看项目分类", @"查看网站项目分类"),
            ("rbac.perm.content.project-categories.view.label", "ja", @"プロジェクトのカテゴリを表示", @"ウェブサイトのプロジェクトカテゴリを表示"),
            ("rbac.perm.content.project-categories.manage.label", "vi", @"Quản lý danh mục dự án", @"Quản lý danh mục dự án trên website"),
            ("rbac.perm.content.project-categories.manage.label", "en", @"Manage project categories", @"Manage website project categories"),
            ("rbac.perm.content.project-categories.manage.label", "zh", @"管理项目分类", @"管理网站项目分类"),
            ("rbac.perm.content.project-categories.manage.label", "ja", @"プロジェクトのカテゴリを管理", @"ウェブサイトのプロジェクトカテゴリを管理"),
            ("rbac.perm.content.services.view.label", "vi", @"Xem dịch vụ", @"Xem dịch vụ trên website"),
            ("rbac.perm.content.services.view.label", "en", @"View services", @"View website services"),
            ("rbac.perm.content.services.view.label", "zh", @"查看服务", @"查看网站服务"),
            ("rbac.perm.content.services.view.label", "ja", @"サービスを表示", @"ウェブサイトのサービスを表示"),
            ("rbac.perm.content.services.manage.label", "vi", @"Quản lý dịch vụ", @"Quản lý dịch vụ trên website"),
            ("rbac.perm.content.services.manage.label", "en", @"Manage services", @"Manage website services"),
            ("rbac.perm.content.services.manage.label", "zh", @"管理服务", @"管理网站服务"),
            ("rbac.perm.content.services.manage.label", "ja", @"サービスを管理", @"ウェブサイトのサービスを管理"),
            ("rbac.perm.content.slideshow.view.label", "vi", @"Xem trình chiếu", @"Xem ảnh trình chiếu trang chủ"),
            ("rbac.perm.content.slideshow.view.label", "en", @"View slideshow", @"View home page slideshow"),
            ("rbac.perm.content.slideshow.view.label", "zh", @"查看幻灯片", @"查看首页幻灯片"),
            ("rbac.perm.content.slideshow.view.label", "ja", @"スライドショーを表示", @"トップページのスライドショーを表示"),
            ("rbac.perm.content.slideshow.manage.label", "vi", @"Quản lý trình chiếu", @"Quản lý ảnh trình chiếu trang chủ"),
            ("rbac.perm.content.slideshow.manage.label", "en", @"Manage slideshow", @"Manage home page slideshow"),
            ("rbac.perm.content.slideshow.manage.label", "zh", @"管理幻灯片", @"管理首页幻灯片"),
            ("rbac.perm.content.slideshow.manage.label", "ja", @"スライドショーを管理", @"トップページのスライドショーを管理"),
            ("rbac.perm.master-data.view.label", "vi", @"Xem dữ liệu chuẩn", @"Xem danh mục dùng chung"),
            ("rbac.perm.master-data.view.label", "en", @"View master data", @"View shared lists"),
            ("rbac.perm.master-data.view.label", "zh", @"查看主数据", @"查看通用目录"),
            ("rbac.perm.master-data.view.label", "ja", @"マスターデータを表示", @"共通リストを表示"),
            ("rbac.perm.master-data.manage.label", "vi", @"Quản lý dữ liệu chuẩn", @"Quản lý danh mục dùng chung"),
            ("rbac.perm.master-data.manage.label", "en", @"Manage master data", @"Manage shared lists"),
            ("rbac.perm.master-data.manage.label", "zh", @"管理主数据", @"管理通用目录"),
            ("rbac.perm.master-data.manage.label", "ja", @"マスターデータを管理", @"共通リストを管理"),
            ("rbac.perm.crm.contracts.view.upstream.all.label", "vi", @"Xem toàn bộ hợp đồng chính", @"Xem toàn bộ hợp đồng với khách hàng"),
            ("rbac.perm.crm.contracts.view.upstream.all.label", "en", @"View all primary contracts", @"View all customer contracts"),
            ("rbac.perm.crm.contracts.view.upstream.all.label", "zh", @"查看全部主合同", @"查看全部客户合同"),
            ("rbac.perm.crm.contracts.view.upstream.all.label", "ja", @"すべての主要契約を表示", @"すべての顧客契約を表示"),
            ("rbac.perm.crm.contracts.view.downstream.all.label", "vi", @"Xem toàn bộ hợp đồng đầu vào", @"Xem toàn bộ hợp đồng với nhà cung cấp, thầu phụ"),
            ("rbac.perm.crm.contracts.view.downstream.all.label", "en", @"View all input contracts", @"View all supplier and subcontractor contracts"),
            ("rbac.perm.crm.contracts.view.downstream.all.label", "zh", @"查看全部下游合同", @"查看全部供应商及分包合同"),
            ("rbac.perm.crm.contracts.view.downstream.all.label", "ja", @"すべての下流契約を表示", @"すべての仕入先・下請契約を表示"),
            ("rbac.perm.crm.contracts.activate.label", "vi", @"Kích hoạt hợp đồng", @"Chuyển hợp đồng sang Đang thực hiện"),
            ("rbac.perm.crm.contracts.activate.label", "en", @"Activate contract", @"Start contract execution"),
            ("rbac.perm.crm.contracts.activate.label", "zh", @"激活合同", @"将合同转为执行中"),
            ("rbac.perm.crm.contracts.activate.label", "ja", @"契約を有効化", @"契約を履行中にする"),
            ("rbac.perm.design.ifc.view.label", "vi", @"Xem phiếu phát hành IFC", @"Xem phiếu phát hành bản vẽ thi công (IFC)"),
            ("rbac.perm.design.ifc.manage.label", "vi", @"Chuẩn bị phiếu IFC", @"Chuẩn bị phiếu phát hành bản vẽ thi công (IFC)"),
            ("rbac.perm.design.ifc.release.label", "vi", @"Phát hành IFC", @"Phát hành bản vẽ thi công (IFC)"),
            ("rbac.perm.construction.punch.view.label", "vi", @"Xem danh mục lỗi", @"Xem danh sách lỗi cần khắc phục"),
            ("rbac.perm.construction.punch.manage.label", "vi", @"Quản lý danh mục lỗi", @"Quản lý danh sách lỗi cần khắc phục"),
            ("rbac.perm.construction.punch.manage.description", "vi", @"Tạo/chỉnh sửa/xoá lỗi, gán người xử lý, chuyển sang In Progress / Fixed / Cancelled hoặc mở lại lỗi.", @"Tạo, sửa, xoá lỗi, giao người xử lý, chuyển sang Đang xử lý / Đã sửa / Đã huỷ hoặc mở lại lỗi."),
            ("rbac.perm.construction.punch.verify.label", "vi", @"Xác nhận đóng lỗi", @"Xác nhận lỗi đã khắc phục"),
            ("rbac.perm.proc.contract-lines.view.label", "vi", @"Xem dòng hợp đồng mua sắm", @"Xem hạng mục hợp đồng với nhà cung cấp"),
            ("rbac.perm.proc.contract-lines.view.label", "en", @"View procurement contract lines", @"View supplier contract lines"),
            ("rbac.perm.proc.contract-lines.view.label", "zh", @"查看采购合同明细", @"查看供应商合同明细"),
            ("rbac.perm.proc.contract-lines.view.label", "ja", @"調達契約明細を表示", @"仕入先契約明細を表示"),
            ("rbac.perm.proc.contract-lines.manage.label", "vi", @"Quản lý dòng hợp đồng mua sắm", @"Quản lý hạng mục hợp đồng với nhà cung cấp"),
            ("rbac.perm.proc.contract-lines.manage.label", "en", @"Manage procurement contract lines", @"Manage supplier contract lines"),
            ("rbac.perm.proc.contract-lines.manage.label", "zh", @"管理采购合同明细", @"管理供应商合同明细"),
            ("rbac.perm.proc.contract-lines.manage.label", "ja", @"調達契約明細を管理", @"仕入先契約明細を管理"),
            ("rbac.perm.proc.rfqs.view.label", "vi", @"Xem RFQ dự án", @"Xem yêu cầu báo giá nhà cung cấp (RFQ)"),
            ("rbac.perm.proc.rfqs.manage.label", "vi", @"Quản lý RFQ dự án", @"Quản lý yêu cầu báo giá nhà cung cấp (RFQ)"),
            ("rbac.perm.proc.rfqs.export.label", "vi", @"Xuất RFQ dự án", @"Xuất yêu cầu báo giá nhà cung cấp (RFQ)"),
            ("rbac.perm.proc.rfqs.award.label", "vi", @"Duyệt chọn nhà cung cấp RFQ", @"Duyệt chọn nhà cung cấp từ yêu cầu báo giá (RFQ)"),
            ("rbac.perm.proc.resource-requests.manage.label", "vi", @"Liên kết hợp đồng và xác nhận cung cấp", @"Gắn hợp đồng nhà cung cấp và xác nhận đã cung cấp nguồn lực"),
            ("rbac.perm.proc.resource-requests.manage.label", "en", @"Link contracts and confirm supply", @"Link supplier contracts and confirm resources supplied"),
            ("rbac.perm.proc.resource-requests.manage.label", "zh", @"关联合同并确认供应", @"关联供应商合同并确认资源已供应"),
            ("rbac.perm.proc.resource-requests.manage.label", "ja", @"契約連携と供給確認", @"仕入先契約の連携と資源供給の確認"),
            ("rbac.perm.construction.hse.view.label", "vi", @"Xem vi phạm HSE", @"Xem vi phạm an toàn (HSE)"),
            ("rbac.perm.construction.hse.manage.label", "vi", @"Quản lý vi phạm HSE", @"Quản lý vi phạm an toàn (HSE)"),
            ("rbac.perm.construction.hse.confirm.label", "vi", @"Xác nhận vi phạm HSE", @"Xác nhận vi phạm an toàn (HSE)"),
            ("rbac.perm.construction.hse.close.label", "vi", @"Đóng vi phạm HSE", @"Đóng vi phạm an toàn (HSE)"),
            ("rbac.perm.construction.hse.delete.label", "vi", @"Xóa vi phạm HSE", @"Xoá vi phạm an toàn (HSE)"),
        ];

        private static void Apply(MigrationBuilder migrationBuilder, bool forward)
        {
            foreach (var (key, language, oldValue, newValue) in Labels)
            {
                var from = (forward ? oldValue : newValue).Replace("'", "''");
                var to = (forward ? newValue : oldValue).Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE translations SET Value = N'{to}', UpdatedAt = SYSUTCDATETIME() " +
                    $"WHERE [Key] = N'{key}' AND LanguageCode = N'{language}' AND Value = N'{from}';");
            }
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, forward: false);
    }
}
