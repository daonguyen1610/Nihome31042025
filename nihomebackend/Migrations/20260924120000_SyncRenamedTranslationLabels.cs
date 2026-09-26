using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// TranslationSeeder only inserts missing keys, so label renames made in
    /// the seed files never reach existing databases. This pushes the NIH-459
    /// Detail Design terminology (plus the deletion-impact item it missed) and
    /// the shorter "Dự án" sidebar label to rows that still hold a previous
    /// seed value; admin edits are untouched.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924120000_SyncRenamedTranslationLabels")]
    public partial class SyncRenamedTranslationLabels : Migration
    {
        private sealed record Rename(string Key, string Language, string[] PreviousValues, string Value);

        private static readonly Rename[] Renames =
        [
            new("basicDesign.action.backToDraft", "vi", ["Về Đang thiết kế"], "Chuyển về đang thiết kế"),
            new("basicDesign.empty", "vi", ["Chưa có hồ sơ Basic Design nào cho dự án này."], "Chưa có hồ sơ thiết kế cơ sở nào cho dự án này."),
            new("basicDesign.fileUploaded", "vi", ["Đã tải file lên"], "Đã tải tệp lên"),
            new("basicDesign.form.hint", "vi", ["Điền tên bản vẽ + chọn bộ môn. Mã bản vẽ (VD KT-BD-001) sinh tự động theo bộ môn."], "Điền tên bản vẽ và chọn bộ môn. Mã bản vẽ (ví dụ: KT-BD-001) được tạo tự động theo bộ môn."),
            new("basicDesign.locked", "vi", ["Dự án chưa ở giai đoạn Basic Design. Hãy chốt phương án Concept trước."], "Dự án chưa ở giai đoạn Thiết kế cơ sở. Hãy chốt phương án thiết kế ý tưởng trước."),
            new("basicDesign.lockedShop", "en", ["Project has moved to Shop Drawing — Basic Design documents are read-only."], "Project has moved to Detail Design — Basic Design documents are read-only."),
            new("basicDesign.lockedShop", "ja", ["案件は施工図段階に移行しました — 基本設計ドキュメントは読み取り専用です。"], "案件は詳細設計段階に移行しました — 基本設計ドキュメントは読み取り専用です。"),
            new("basicDesign.lockedShop", "vi", ["Dự án đã chuyển sang Shop Drawing — hồ sơ Basic Design chỉ đọc.", "Dự án đã chuyển sang Thiết kế chi tiết — hồ sơ Basic Design chỉ đọc."], "Dự án đã chuyển sang Thiết kế chi tiết — hồ sơ thiết kế cơ sở chỉ đọc."),
            new("basicDesign.lockedShop", "zh", ["项目已进入施工图阶段——基本设计文档只读。"], "项目已进入详细设计阶段——基本设计文档只读。"),
            new("basicDesign.readiness.hint", "vi", ["Cần ≥1 hồ sơ Đã duyệt nội bộ cho mỗi bộ môn: Kiến trúc · Kết cấu · MEP."], "Cần ≥1 hồ sơ được duyệt nội bộ cho mỗi bộ môn: Kiến trúc · Kết cấu · MEP."),
            new("basicDesign.readiness.title", "en", ["Ready for Shop Drawing"], "Ready for Detail Design"),
            new("basicDesign.readiness.title", "ja", ["施工図移行の準備"], "詳細設計移行の準備"),
            new("basicDesign.readiness.title", "vi", ["Sẵn sàng chuyển Shop Drawing"], "Sẵn sàng chuyển Thiết kế chi tiết"),
            new("basicDesign.readiness.title", "zh", ["准备转施工图"], "准备转详细设计"),
            new("basicDesign.readiness.unlock", "en", ["Unlock Shop Drawing"], "Unlock Detail Design"),
            new("basicDesign.readiness.unlock", "ja", ["施工図を解放"], "詳細設計を解放"),
            new("basicDesign.readiness.unlock", "vi", ["Mở khoá Shop Drawing"], "Mở khoá Thiết kế chi tiết"),
            new("basicDesign.readiness.unlock", "zh", ["解锁施工图"], "解锁详细设计"),
            new("basicDesign.readiness.unlocked", "en", ["Shop Drawing unlocked. Project stage advanced."], "Detail Design unlocked. Project stage advanced."),
            new("basicDesign.readiness.unlocked", "ja", ["施工図が解放され、案件段階が進みました。"], "詳細設計が解放され、案件段階が進みました。"),
            new("basicDesign.readiness.unlocked", "vi", ["Đã mở khoá Shop Drawing. Dự án đã chuyển giai đoạn."], "Đã mở khoá Thiết kế chi tiết. Dự án đã chuyển giai đoạn."),
            new("basicDesign.readiness.unlocked", "zh", ["已解锁施工图，项目阶段已更新。"], "已解锁详细设计，项目阶段已更新。"),
            new("basicDesign.replaceFile", "vi", ["Thay file"], "Thay tệp"),
            new("basicDesign.status.SubmittedForPermit", "vi", ["Đã submit xin phép"], "Đã nộp xin phép"),
            new("basicDesign.status.SubmittedForReview", "vi", ["Đã submit review"], "Đã gửi duyệt"),
            new("basicDesign.title", "vi", ["Hồ sơ Basic Design"], "Hồ sơ thiết kế cơ sở"),
            new("basicDesign.uploadFile", "vi", ["Tải file lên"], "Tải tệp lên"),
            new("concepts.delete.confirmTitle", "vi", ["Xoá phương án Concept?"], "Xoá phương án thiết kế ý tưởng?"),
            new("concepts.empty", "vi", ["Chưa có phương án Concept nào cho dự án này."], "Chưa có phương án thiết kế ý tưởng nào cho dự án này."),
            new("concepts.finalize.confirmBody", "vi", ["Chốt \"{name}\" sẽ loại bỏ các phương án còn lại và mở khoá giai đoạn Basic Design. Không thể hoàn tác."], "Chốt \"{name}\" sẽ loại bỏ các phương án còn lại và mở khoá giai đoạn Thiết kế cơ sở. Không thể hoàn tác."),
            new("concepts.finalize.confirmTitle", "vi", ["Chốt phương án Concept?"], "Chốt phương án thiết kế ý tưởng?"),
            new("concepts.finalized", "vi", ["Đã chốt phương án. Dự án chuyển sang giai đoạn Basic Design."], "Đã chốt phương án. Dự án chuyển sang giai đoạn Thiết kế cơ sở."),
            new("concepts.locked", "vi", ["Dự án đã qua giai đoạn Concept — chỉ hiển thị lịch sử."], "Dự án đã qua giai đoạn Thiết kế ý tưởng — chỉ hiển thị lịch sử."),
            new("concepts.title", "vi", ["Phương án Concept"], "Phương án thiết kế ý tưởng"),
            new("designProjects.delete.notConceptTitle", "vi", ["Không thể xoá dự án ngoài giai đoạn Concept"], "Không thể xoá dự án sau giai đoạn Thiết kế ý tưởng"),
            new("designProjects.detail.tab.basic", "vi", ["Basic Design"], "Thiết kế cơ sở"),
            new("designProjects.detail.tab.concept", "vi", ["Concept"], "Thiết kế ý tưởng"),
            new("deletionImpact.item.design.shopDrawings", "vi", ["Shop Drawing"], "Tài liệu thiết kế chi tiết"),
            new("deletionImpact.item.design.shopDrawings", "en", ["Shop drawings"], "Detail design documents"),
            new("deletionImpact.item.design.shopDrawings", "zh", ["施工图"], "详细设计文档"),
            new("deletionImpact.item.design.shopDrawings", "ja", ["施工図"], "詳細設計文書"),
            new("designProjects.detail.tab.shop", "en", ["Shop drawing"], "Detail Design"),
            new("designProjects.detail.tab.shop", "ja", ["施工図"], "詳細設計"),
            new("designProjects.detail.tab.shop", "vi", ["Shop Drawing"], "Thiết kế chi tiết"),
            new("designProjects.detail.tab.shop", "zh", ["施工图"], "详细设计"),
            new("designProjects.detail.tab.team", "vi", ["Team"], "Đội ngũ"),
            new("designProjects.documents.description", "vi", ["Tổng hợp chỉ đọc; cập nhật hồ sơ tại tab giai đoạn tương ứng."], "Tổng hợp chỉ đọc; cập nhật hồ sơ tại mục giai đoạn tương ứng."),
            new("designProjects.documents.downloadFile", "vi", ["Tải file thiết kế"], "Tải tệp thiết kế"),
            new("designProjects.documents.group.basic", "vi", ["Hồ sơ Basic Design"], "Hồ sơ thiết kế cơ sở"),
            new("designProjects.documents.group.concepts", "vi", ["Phương án Concept"], "Phương án thiết kế ý tưởng"),
            new("designProjects.documents.group.revisions", "vi", ["Revision"], "Lịch sử phiên bản"),
            new("designProjects.documents.group.shop", "en", ["Shop drawings"], "Detail Design documents"),
            new("designProjects.documents.group.shop", "ja", ["施工図"], "詳細設計ドキュメント"),
            new("designProjects.documents.group.shop", "vi", ["Bản vẽ Shop Drawing"], "Hồ sơ Thiết kế chi tiết"),
            new("designProjects.documents.group.shop", "zh", ["施工图"], "详细设计文件"),
            new("designProjects.documents.openFile", "vi", ["Mở file thiết kế"], "Mở tệp thiết kế"),
            new("designProjects.field.deadline", "vi", ["Deadline"], "Hạn hoàn thành"),
            new("designProjects.field.designLead", "vi", ["Design Lead"], "Chủ trì thiết kế"),
            new("designProjects.field.pm", "vi", ["PM phụ trách"], "Quản lý dự án"),
            new("designProjects.filter.allLeads", "vi", ["Tất cả Design Lead"], "Tất cả chủ trì thiết kế"),
            new("designProjects.filter.allPms", "vi", ["Tất cả PM"], "Tất cả quản lý dự án"),
            new("designProjects.form.createHint", "vi", ["Chọn khách hàng, hợp đồng liên kết (tuỳ chọn), gán PM/Design Lead rồi bấm Lưu. Mã dự án sinh tự động theo định dạng DP-YYYY-NNNN."], "Chọn khách hàng, hợp đồng liên kết (tuỳ chọn), quản lý dự án và chủ trì thiết kế rồi bấm Lưu. Mã dự án được tạo tự động theo định dạng DP-YYYY-NNNN."),
            new("designProjects.form.deadlineBeforeStart", "vi", ["Deadline phải sau ngày bắt đầu."], "Hạn hoàn thành phải từ ngày bắt đầu trở đi."),
            new("designProjects.form.leadNone", "vi", ["Chưa gán Design Lead"], "Chưa phân công chủ trì thiết kế"),
            new("designProjects.form.pmNone", "vi", ["Chưa gán PM"], "Chưa phân công quản lý dự án"),
            new("designProjects.form.stageHint", "vi", ["Cập nhật giai đoạn / trạng thái để đồng bộ tiến độ và mở khoá các tab tiếp theo."], "Cập nhật giai đoạn / trạng thái để đồng bộ tiến độ và mở khoá các mục tiếp theo."),
            new("designProjects.stage.BasicDesign", "vi", ["Basic Design"], "Thiết kế cơ sở"),
            new("designProjects.stage.Concept", "vi", ["Concept"], "Thiết kế ý tưởng"),
            new("designProjects.stage.ShopDrawing", "en", ["Shop drawing"], "Detail Design"),
            new("designProjects.stage.ShopDrawing", "ja", ["施工図"], "詳細設計"),
            new("designProjects.stage.ShopDrawing", "vi", ["Shop Drawing"], "Thiết kế chi tiết"),
            new("designProjects.stage.ShopDrawing", "zh", ["施工图"], "详细设计"),
            new("designProjects.subtitle", "en", ["Track design projects moving through Concept → Basic Design → Shop Drawing."], "Track design projects moving through Concept → Basic Design → Detail Design."),
            new("designProjects.subtitle", "ja", ["コンセプト → 基本設計 → 施工図の3段階を追跡します。"], "コンセプト → 基本設計 → 詳細設計の3段階を追跡します。"),
            new("designProjects.subtitle", "vi", ["Theo dõi các dự án thiết kế đang chạy qua ba giai đoạn Concept → Basic Design → Shop Drawing.", "Theo dõi các dự án thiết kế đang chạy qua ba giai đoạn Concept → Basic Design → Thiết kế chi tiết."], "Theo dõi dự án qua ba giai đoạn: Thiết kế ý tưởng → Thiết kế cơ sở → Thiết kế chi tiết."),
            new("designProjects.subtitle", "zh", ["跟踪设计项目在 Concept → Basic Design → Shop Drawing 三个阶段的进展。"], "跟踪设计项目在概念设计 → 基本设计 → 详细设计三个阶段的进展。"),
            new("designProjects.team.role.basic", "vi", ["Phụ trách Basic Design"], "Phụ trách thiết kế cơ sở"),
            new("designProjects.team.role.concept", "vi", ["Phụ trách Concept"], "Phụ trách thiết kế ý tưởng"),
            new("designProjects.team.role.shop", "en", ["Shop drawing owner"], "Detail Design owner"),
            new("designProjects.team.role.shop", "ja", ["施工図担当"], "詳細設計担当"),
            new("designProjects.team.role.shop", "vi", ["Phụ trách Shop Drawing"], "Phụ trách Thiết kế chi tiết"),
            new("designProjects.team.role.shop", "zh", ["施工图负责人"], "详细设计负责人"),
            new("drawingRevision.button", "vi", ["Xem revision"], "Xem phiên bản"),
            new("drawingRevision.created", "vi", ["Đã tạo revision"], "Đã tạo phiên bản"),
            new("drawingRevision.empty", "vi", ["Bản vẽ này chưa có revision nào — vẫn ở phiên bản gốc R0."], "Bản vẽ này chưa có phiên bản cập nhật nào — hiện vẫn là bản gốc R0."),
            new("drawingRevision.form.hint", "vi", ["Revision mới sẽ tự động lên số kế tiếp và các revision cũ sẽ chuyển sang trạng thái Đã thu hồi."], "Phiên bản mới sẽ tự động được đánh số tiếp theo và các phiên bản cũ sẽ chuyển sang trạng thái Đã thu hồi."),
            new("drawingRevision.form.notePlaceholder", "vi", ["Mô tả chi tiết thay đổi so với revision trước…"], "Mô tả chi tiết thay đổi so với phiên bản trước…"),
            new("drawingRevision.new", "vi", ["Tạo revision mới"], "Tạo phiên bản mới"),
            new("drawingRevision.title", "vi", ["Lịch sử revision"], "Lịch sử phiên bản"),
            new("drawingRevision.warn.supersededOpen", "vi", ["Bạn đang xem revision đã bị thu hồi. Hãy dùng revision hiện tại thay thế."], "Bạn đang xem phiên bản đã bị thu hồi. Hãy dùng phiên bản hiện tại thay thế."),
            new("ifcRelease.lockedBefore", "en", ["Project is not at the Shop Drawing stage — IFC releases are not available yet."], "Project is not at the Detail Design stage — IFC releases are not available yet."),
            new("ifcRelease.lockedBefore", "ja", ["案件は施工図段階に達していません — IFC発行はまだ利用できません。"], "案件は詳細設計段階に達していません — IFC発行はまだ利用できません。"),
            new("ifcRelease.lockedBefore", "vi", ["Dự án chưa ở giai đoạn Shop Drawing — chưa thể tạo phiếu IFC."], "Dự án chưa ở giai đoạn Thiết kế chi tiết — chưa thể tạo phiếu IFC."),
            new("ifcRelease.lockedBefore", "zh", ["项目尚未进入施工图阶段——暂无法创建IFC发布单。"], "项目尚未进入详细设计阶段——暂无法创建IFC发布单。"),
            new("ifcRelease.recipient.namePlaceholder", "vi", ["VD: Công ty CP xây dựng ABC"], "Ví dụ: Công ty CP xây dựng ABC"),
            new("masterData.category.drawing_revision_reason.title", "vi", ["Lý do revision bản vẽ"], "Lý do cập nhật phiên bản bản vẽ"),
            new("notification.design.revision.created.body", "vi", ["Bản vẽ {{drawingCode}} vừa có revision {{revision}} ({{reason}}). Bản cũ đã bị thu hồi, vui lòng dùng phiên bản mới."], "Bản vẽ {{drawingCode}} vừa có phiên bản {{revision}} ({{reason}}). Bản cũ đã bị thu hồi, vui lòng dùng phiên bản mới."),
            new("notification.design.revision.created.title", "vi", ["Revision mới cho bản vẽ {{drawingCode}}"], "Phiên bản mới cho bản vẽ {{drawingCode}}"),
            new("rbac.perm.design.basic.approve.description", "en", ["Internally approves Basic Design docs (gate to Shop Drawing)."], "Internally approves Basic Design documents (gate to Detail Design)."),
            new("rbac.perm.design.basic.approve.description", "ja", ["基本設計の社内承認(施工図移行の要件)を行います。"], "基本設計の社内承認（詳細設計移行の要件）を行います。"),
            new("rbac.perm.design.basic.approve.description", "vi", ["Duyệt nội bộ hồ sơ Basic Design (điều kiện chuyển Shop Drawing).", "Duyệt nội bộ hồ sơ Basic Design (điều kiện chuyển Thiết kế chi tiết)."], "Duyệt nội bộ hồ sơ thiết kế cơ sở (điều kiện chuyển sang Thiết kế chi tiết)."),
            new("rbac.perm.design.basic.approve.description", "zh", ["对基础设计文件进行内部审批(转施工图的门槛)。"], "对基本设计文件进行内部审批（进入详细设计的门槛）。"),
            new("rbac.perm.design.basic.approve.label", "vi", ["Duyệt hồ sơ Basic Design"], "Duyệt hồ sơ thiết kế cơ sở"),
            new("rbac.perm.design.basic.manage.description", "vi", ["Cho phép upload/thay thế bản vẽ + thuyết minh Basic Design."], "Cho phép tải lên/thay thế bản vẽ và thuyết minh thiết kế cơ sở."),
            new("rbac.perm.design.basic.manage.label", "vi", ["Quản lý hồ sơ Basic Design"], "Quản lý hồ sơ thiết kế cơ sở"),
            new("rbac.perm.design.basic.view.label", "vi", ["Xem hồ sơ Basic Design"], "Xem hồ sơ thiết kế cơ sở"),
            new("rbac.perm.design.concepts.finalize.description", "vi", ["Chốt 1 phương án và tự động khoá các phương án còn lại; mở khoá Basic Design."], "Chốt một phương án, tự động khoá các phương án còn lại và mở khoá Thiết kế cơ sở."),
            new("rbac.perm.design.concepts.finalize.label", "vi", ["Chốt phương án Concept"], "Chốt phương án thiết kế ý tưởng"),
            new("rbac.perm.design.concepts.manage.description", "vi", ["Cho phép tạo/sửa phương án Concept, upload 3D/mặt bằng."], "Cho phép tạo/sửa phương án thiết kế ý tưởng, tải lên mô hình 3D và mặt bằng."),
            new("rbac.perm.design.concepts.manage.label", "vi", ["Quản lý hồ sơ Concept"], "Quản lý hồ sơ thiết kế ý tưởng"),
            new("rbac.perm.design.concepts.view.description", "vi", ["Cho phép xem các phương án Concept và feedback khách hàng."], "Cho phép xem các phương án thiết kế ý tưởng và phản hồi của khách hàng."),
            new("rbac.perm.design.concepts.view.label", "vi", ["Xem hồ sơ Concept"], "Xem hồ sơ thiết kế ý tưởng"),
            new("rbac.perm.design.projects.manage.description", "vi", ["Cho phép tạo/sửa dự án thiết kế và cấu hình team bộ môn."], "Cho phép tạo/sửa dự án thiết kế và cấu hình đội ngũ bộ môn."),
            new("rbac.perm.design.revisions.manage.description", "vi", ["Cho phép tạo revision mới với lý do thay đổi + tự thu hồi bản cũ."], "Cho phép tạo phiên bản mới kèm lý do thay đổi và tự động thu hồi bản cũ."),
            new("rbac.perm.design.revisions.view.description", "vi", ["Cho phép xem lịch sử revision và bản vẽ đã bị thu hồi."], "Cho phép xem lịch sử phiên bản và các bản vẽ đã bị thu hồi."),
            new("rbac.perm.design.shop.approve.description", "en", ["Final Design Lead approval of shop drawings before an IFC release."], "Final Design Lead approval of Detail Design documents before an IFC release."),
            new("rbac.perm.design.shop.approve.description", "ja", ["IFC発行前に設計リーダーが施工図を最終承認します。"], "IFC発行前に設計リーダーが詳細設計ドキュメントを最終承認します。"),
            new("rbac.perm.design.shop.approve.description", "vi", ["Duyệt cuối Shop Drawing bởi Design Lead trước khi đưa vào phiếu IFC.", "Design Lead duyệt cuối Thiết kế chi tiết trước khi đưa vào phiếu IFC."], "Chủ trì thiết kế duyệt cuối hồ sơ Thiết kế chi tiết trước khi đưa vào phiếu IFC."),
            new("rbac.perm.design.shop.approve.description", "zh", ["施工图在纳入IFC发布前由设计主管终审。"], "详细设计文件在纳入IFC发布前由设计主管终审。"),
            new("rbac.perm.design.shop.approve.label", "en", ["Approve shop drawings"], "Approve Detail Design"),
            new("rbac.perm.design.shop.approve.label", "ja", ["施工図を承認"], "詳細設計を承認"),
            new("rbac.perm.design.shop.approve.label", "vi", ["Duyệt Shop Drawing"], "Duyệt Thiết kế chi tiết"),
            new("rbac.perm.design.shop.approve.label", "zh", ["审批施工图"], "审批详细设计"),
            new("rbac.perm.design.shop.manage.description", "en", ["Allows creating/editing/uploading shop drawings and assigning cross-discipline reviewers."], "Allows creating, editing, and uploading Detail Design documents and assigning cross-discipline reviewers."),
            new("rbac.perm.design.shop.manage.description", "ja", ["施工図の作成・編集・アップロード及び他専門レビュアーの割当てを許可します。"], "詳細設計ドキュメントの作成・編集・アップロード及び他専門レビュアーの割当てを許可します。"),
            new("rbac.perm.design.shop.manage.description", "vi", ["Cho phép tạo/sửa/upload Shop Drawing và gán reviewer chéo bộ môn.", "Cho phép tạo/sửa/upload hồ sơ Thiết kế chi tiết và gán reviewer chéo bộ môn."], "Cho phép tạo/sửa/tải lên hồ sơ Thiết kế chi tiết và phân công người duyệt chéo bộ môn."),
            new("rbac.perm.design.shop.manage.description", "zh", ["允许创建/编辑/上传施工图并分配跨专业审查人。"], "允许创建、编辑和上传详细设计文件，并分配跨专业审查人。"),
            new("rbac.perm.design.shop.manage.label", "en", ["Manage shop drawings"], "Manage Detail Design"),
            new("rbac.perm.design.shop.manage.label", "ja", ["施工図を管理"], "詳細設計を管理"),
            new("rbac.perm.design.shop.manage.label", "vi", ["Quản lý Shop Drawing"], "Quản lý Thiết kế chi tiết"),
            new("rbac.perm.design.shop.manage.label", "zh", ["管理施工图"], "管理详细设计"),
            new("rbac.perm.design.shop.view.description", "en", ["Allows viewing shop drawings by discipline and work package."], "Allows viewing Detail Design documents by discipline and work package."),
            new("rbac.perm.design.shop.view.description", "ja", ["専門と分類別に施工図の閲覧を許可します。"], "専門と分類別に詳細設計ドキュメントの閲覧を許可します。"),
            new("rbac.perm.design.shop.view.description", "vi", ["Cho phép xem bản vẽ thi công chi tiết theo bộ môn và hạng mục."], "Cho phép xem hồ sơ Thiết kế chi tiết theo bộ môn và hạng mục."),
            new("rbac.perm.design.shop.view.description", "zh", ["允许按专业和分项查看施工图。"], "允许按专业和分项查看详细设计文件。"),
            new("rbac.perm.design.shop.view.label", "en", ["View shop drawings"], "View Detail Design"),
            new("rbac.perm.design.shop.view.label", "ja", ["施工図を表示"], "詳細設計を表示"),
            new("rbac.perm.design.shop.view.label", "vi", ["Xem Shop Drawing"], "Xem Thiết kế chi tiết"),
            new("rbac.perm.design.shop.view.label", "zh", ["查看施工图"], "查看详细设计"),
            new("rbac.role.ARCHITECT.description", "en", ["Owns architectural design documents across Concept, Basic and Shop Drawing stages."], "Owns architectural design documents across Concept, Basic Design, and Detail Design stages."),
            new("rbac.role.ARCHITECT.description", "ja", ["コンセプト・基本・施工図の各段階における建築設計を担当します。"], "コンセプト・基本設計・詳細設計の各段階における建築設計を担当します。"),
            new("rbac.role.ARCHITECT.description", "vi", ["Chịu trách nhiệm hồ sơ thiết kế bộ môn Kiến trúc (Concept · Basic · Shop Drawing).", "Chịu trách nhiệm hồ sơ thiết kế bộ môn Kiến trúc (Concept · Basic Design · Thiết kế chi tiết)."], "Chịu trách nhiệm hồ sơ bộ môn Kiến trúc (Thiết kế ý tưởng · Thiết kế cơ sở · Thiết kế chi tiết)."),
            new("rbac.role.ARCHITECT.description", "zh", ["负责建筑专业的方案、基础及施工图设计文件。"], "负责建筑专业的概念设计、基本设计及详细设计文件。"),
            new("rbac.role.DESIGN_LEAD.description", "en", ["Finalises concepts, approves Basic/Shop drawings and releases IFC packages."], "Finalises concepts, approves Basic Design and Detail Design, and releases IFC packages."),
            new("rbac.role.DESIGN_LEAD.description", "ja", ["コンセプトを確定し、基本/施工図を承認し、IFC図面を発行します。"], "コンセプトを確定し、基本設計と詳細設計を承認し、IFC図面を発行します。"),
            new("rbac.role.DESIGN_LEAD.description", "vi", ["Chốt phương án Concept, duyệt Basic/Shop Drawing và phát hành hồ sơ IFC.", "Chốt phương án Concept, duyệt Basic Design/Thiết kế chi tiết và phát hành hồ sơ IFC."], "Chốt phương án thiết kế ý tưởng, duyệt Thiết kế cơ sở/Thiết kế chi tiết và phát hành hồ sơ IFC."),
            new("rbac.role.DESIGN_LEAD.description", "zh", ["确定方案概念,审批基础/施工图并发布IFC文件。"], "确定概念方案，审批基本设计和详细设计并发布IFC文件。"),
            new("rbac.role.STRUCT_ENGINEER.description", "en", ["Owns structural engineering deliverables in Basic Design and Shop Drawing."], "Owns structural engineering deliverables in Basic Design and Detail Design."),
            new("rbac.role.STRUCT_ENGINEER.description", "ja", ["基本設計と施工図における構造関連成果物を担当します。"], "基本設計と詳細設計における構造関連成果物を担当します。"),
            new("rbac.role.STRUCT_ENGINEER.description", "vi", ["Phụ trách hồ sơ Kết cấu trong Basic Design và Shop Drawing.", "Phụ trách hồ sơ Kết cấu trong Basic Design và Thiết kế chi tiết."], "Phụ trách hồ sơ Kết cấu trong giai đoạn Thiết kế cơ sở và Thiết kế chi tiết."),
            new("rbac.role.STRUCT_ENGINEER.description", "zh", ["负责基础设计与施工图中的结构专业文件。"], "负责基本设计与详细设计中的结构专业文件。"),
            new("shopDrawing.action.backToDraft", "vi", ["Về Đang vẽ"], "Chuyển về đang vẽ"),
            new("shopDrawing.action.queueIfc", "vi", ["Xếp hàng IFC"], "Đưa vào danh sách phát hành IFC"),
            new("shopDrawing.action.sendReview", "vi", ["Gửi review"], "Gửi duyệt"),
            new("shopDrawing.action.unqueueIfc", "vi", ["Rút khỏi hàng IFC"], "Rút khỏi danh sách phát hành IFC"),
            new("shopDrawing.empty", "en", ["No shop drawings yet."], "No Detail Design documents yet."),
            new("shopDrawing.empty", "ja", ["施工図はまだありません。"], "詳細設計ドキュメントはまだありません。"),
            new("shopDrawing.empty", "vi", ["Chưa có bản vẽ Shop Drawing nào cho dự án này."], "Chưa có hồ sơ Thiết kế chi tiết nào cho dự án này."),
            new("shopDrawing.empty", "zh", ["该项目尚无施工图。"], "该项目尚无详细设计文件。"),
            new("shopDrawing.fileUploaded", "vi", ["Đã tải file lên"], "Đã tải tệp lên"),
            new("shopDrawing.form.constructionItemPlaceholder", "vi", ["VD: Móng cọc, Trần thạch cao tầng 3"], "Ví dụ: Móng cọc, trần thạch cao tầng 3"),
            new("shopDrawing.form.hint", "vi", ["Điền tên bản vẽ + chọn bộ môn + hạng mục thi công. Mã bản vẽ (VD KT-SD-001) sinh tự động theo bộ môn."], "Điền tên bản vẽ, chọn bộ môn và hạng mục thi công. Mã bản vẽ (ví dụ: KT-SD-001) được tạo tự động theo bộ môn."),
            new("shopDrawing.lockedAfter", "en", ["Project is completed — shop drawings are read-only."], "Project is completed — Detail Design documents are read-only."),
            new("shopDrawing.lockedAfter", "ja", ["案件は完了しました — 施工図は読み取り専用です。"], "案件は完了しました — 詳細設計ドキュメントは読み取り専用です。"),
            new("shopDrawing.lockedAfter", "vi", ["Dự án đã hoàn thành — bản vẽ Shop Drawing chỉ đọc."], "Dự án đã hoàn thành — hồ sơ Thiết kế chi tiết chỉ đọc."),
            new("shopDrawing.lockedAfter", "zh", ["项目已完成——施工图为只读。"], "项目已完成——详细设计文件为只读。"),
            new("shopDrawing.lockedBefore", "en", ["Project is not at the Shop Drawing stage yet. Unlock it from the Basic Design tab first."], "Project is not at the Detail Design stage yet. Unlock it from the Basic Design tab first."),
            new("shopDrawing.lockedBefore", "ja", ["案件は施工図段階に達していません。まず基本設計タブから解放してください。"], "案件は詳細設計段階に達していません。まず基本設計タブから解放してください。"),
            new("shopDrawing.lockedBefore", "vi", ["Dự án chưa ở giai đoạn Shop Drawing. Hãy mở khoá từ tab Basic Design trước.", "Dự án chưa ở giai đoạn Thiết kế chi tiết. Hãy mở khoá từ tab Basic Design trước."], "Dự án chưa ở giai đoạn Thiết kế chi tiết. Hãy mở khoá từ mục Thiết kế cơ sở trước."),
            new("shopDrawing.lockedBefore", "zh", ["项目尚未进入施工图阶段。请先在基本设计选项卡中解锁。"], "项目尚未进入详细设计阶段。请先在基本设计选项卡中解锁。"),
            new("shopDrawing.replaceFile", "vi", ["Thay file"], "Thay tệp"),
            new("shopDrawing.stats.inReview", "vi", ["Đang review"], "Chờ duyệt"),
            new("shopDrawing.stats.pendingIfc", "vi", ["Chờ IFC"], "Chờ phát hành IFC"),
            new("shopDrawing.stats.released", "vi", ["Đã phát hành"], "Đã phát hành IFC"),
            new("shopDrawing.status.InReview", "vi", ["Đang review"], "Chờ duyệt"),
            new("shopDrawing.title", "en", ["Shop Drawings"], "Detail Design"),
            new("shopDrawing.title", "ja", ["施工図"], "詳細設計"),
            new("shopDrawing.title", "vi", ["Bản vẽ Shop Drawing"], "Thiết kế chi tiết"),
            new("shopDrawing.title", "zh", ["施工图"], "详细设计"),
            new("shopDrawing.uploadFile", "vi", ["Tải file lên"], "Tải tệp lên"),
            new("nav.operationalProjects", "vi", ["Dự án vận hành"], "Dự án"),
            new("nav.operationalProjects", "en", ["Operational projects"], "Projects"),
            new("nav.operationalProjects", "zh", ["运营项目"], "项目"),
            new("nav.operationalProjects", "ja", ["運用プロジェクト"], "プロジェクト"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                var previous = string.Join(", ", rename.PreviousValues.Select(Literal));
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(rename.Value)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.Language)}
                      AND Value IN ({previous});
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var rename in Renames)
            {
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(rename.PreviousValues[0])}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.Language)}
                      AND Value = {Literal(rename.Value)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
