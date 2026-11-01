using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations;

public partial class SyncDesignContractTranslations : Migration
{
    private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

    private static readonly Rename[] Renames =
    [
        new("nav.designProjects", "vi", "Dự án thiết kế", "Hợp Đồng Thiết Kế"),
        new("nav.designProjects", "en", "Design projects", "Design contracts"),
        new("nav.designProjects", "zh", "设计项目", "设计合同"),
        new("nav.designProjects", "ja", "設計プロジェクト", "設計契約"),
        new("designProjects.title", "vi", "Quản lý Dự án thiết kế", "Quản lý Hợp Đồng Thiết Kế"),
        new("designProjects.title", "en", "Design project management", "Design contract management"),
        new("designProjects.title", "zh", "设计项目管理", "设计合同管理"),
        new("designProjects.title", "ja", "設計プロジェクト管理", "設計契約管理"),
        new("designProjects.subtitle", "en", "Track design projects moving through Concept → Basic Design → Detail Design.", "Track design contracts moving through Concept → Basic Design → Detail Design."),
        new("designProjects.subtitle", "zh", "跟踪设计项目在概念设计 → 基本设计 → 详细设计三个阶段的进展。", "跟踪设计合同在概念设计 → 基本设计 → 详细设计三个阶段的进展。"),
        new("designProjects.new", "vi", "Tạo dự án thiết kế", "Tạo hợp đồng thiết kế"),
        new("designProjects.new", "en", "New design project", "New design contract"),
        new("designProjects.new", "zh", "新建设计项目", "新建设计合同"),
        new("designProjects.edit", "en", "Edit design project", "Edit design contract"),
        new("designProjects.edit", "zh", "编辑设计项目", "编辑设计合同"),
        new("designProjects.created", "vi", "Đã tạo dự án thiết kế", "Đã tạo hợp đồng thiết kế"),
        new("designProjects.created", "en", "Design project created", "Design contract created"),
        new("designProjects.created", "zh", "已创建设计项目", "已创建设计合同"),
        new("designProjects.created", "ja", "設計プロジェクトを作成しました", "設計契約を作成しました"),
        new("designProjects.updated", "en", "Design project updated", "Design contract updated"),
        new("designProjects.updated", "zh", "已更新设计项目", "已更新设计合同"),
        new("designProjects.updated", "ja", "設計プロジェクトを更新しました", "設計契約を更新しました"),
        new("designProjects.deleted", "en", "Design project deleted", "Design contract deleted"),
        new("designProjects.deleted", "zh", "已删除设计项目", "已删除设计合同"),
        new("designProjects.deleted", "ja", "設計プロジェクトを削除しました", "設計契約を削除しました"),
        new("designProjects.empty", "vi", "Chưa có dự án thiết kế nào.", "Chưa có hợp đồng thiết kế nào."),
        new("designProjects.empty", "en", "No design projects yet.", "No design contracts yet."),
        new("designProjects.empty", "zh", "暂无设计项目。", "暂无设计合同。"),
        new("designProjects.empty", "ja", "設計プロジェクトがまだありません。", "設計契約がまだありません。"),
        new("designProjects.emptyFiltered", "en", "No design projects match the current filters.", "No design contracts match the current filters."),
        new("designProjects.delete.confirmTitle", "vi", "Xoá dự án thiết kế?", "Xoá hợp đồng thiết kế?"),
        new("designProjects.delete.confirmTitle", "en", "Delete design project?", "Delete design contract?"),
        new("designProjects.delete.confirmTitle", "zh", "删除设计项目？", "删除设计合同？"),
        new("designProjects.delete.confirmTitle", "ja", "設計プロジェクトを削除しますか？", "設計契約を削除しますか？"),
        new("designProjects.detail.back", "vi", "Quay lại danh sách dự án thiết kế", "Quay lại danh sách hợp đồng thiết kế"),
        new("designProjects.detail.back", "en", "Back to design projects", "Back to design contracts"),
        new("designProjects.detail.back", "zh", "返回设计项目列表", "返回设计合同列表"),
        new("designProjects.detail.back", "ja", "設計プロジェクト一覧へ戻る", "設計契約一覧へ戻る"),
        new("designProjects.error.contractTaken", "vi", "Hợp đồng này đã có dự án thiết kế. Đang mở dự án hiện có.", "Hợp đồng này đã có hợp đồng thiết kế. Đang mở dự án hiện có."),
        new("designProjects.error.contractTaken", "en", "This contract already has a design project. Opening the existing one.", "This contract already has a design contract. Opening the existing one."),
        new("designProjects.error.contractTaken", "zh", "该合同已有设计项目，正在打开现有项目。", "该合同已有设计合同，正在打开现有项目。"),
        new("designProjects.error.contractTaken", "ja", "この契約には既に設計プロジェクトがあります。既存のものを開きます。", "この契約には既に設計契約があります。既存のものを開きます。"),
        new("designProjects.team.unlinkedDescription", "vi", "Liên kết Dự án thiết kế với Dự án vận hành để quản lý đội ngũ chính thức.", "Liên kết Hợp Đồng Thiết Kế với Dự án vận hành để quản lý đội ngũ chính thức."),
        new("designProjects.team.unlinkedDescription", "en", "Link this design project to an operational project to manage its authoritative team.", "Link this design contract to an operational project to manage its authoritative team."),
        new("designProjects.team.field.designRoles", "vi", "Vai trò trong dự án thiết kế này", "Vai trò trong hợp đồng thiết kế này"),
        new("designProjects.team.field.designRoles", "en", "Roles in this design project", "Roles in this design contract"),
        new("designProjects.team.field.designRoles", "zh", "此设计项目中的角色", "此设计合同中的角色"),
        new("designProjects.team.field.designRoles", "ja", "この設計プロジェクトの役割", "この設計契約の役割"),
        new("designProjects.team.dialog.member.design.add.description", "vi", "Chọn người dùng và phân vai cho dự án thiết kế này.", "Chọn người dùng và phân vai cho hợp đồng thiết kế này."),
        new("designProjects.team.dialog.member.design.add.description", "en", "Choose a user and assign roles for this design project.", "Choose a user and assign roles for this design contract."),
        new("designProjects.team.dialog.member.design.add.description", "zh", "选择用户并为此设计项目分配角色。", "选择用户并为此设计合同分配角色。"),
        new("designProjects.team.dialog.member.design.add.description", "ja", "ユーザーを選択し、この設計プロジェクトの役割を割り当てます。", "ユーザーを選択し、この設計契約の役割を割り当てます。"),
        new("designProjects.team.dialog.member.design.edit.description", "vi", "Cập nhật vai trò trong dự án thiết kế này.", "Cập nhật vai trò trong hợp đồng thiết kế này."),
        new("designProjects.team.dialog.member.design.edit.description", "en", "Update roles in this design project.", "Update roles in this design contract."),
        new("designProjects.team.dialog.member.design.edit.description", "zh", "更新此设计项目中的角色。", "更新此设计合同中的角色。"),
        new("designProjects.team.dialog.member.design.edit.description", "ja", "この設計プロジェクトの役割を更新します。", "この設計契約の役割を更新します。"),
        new("designProjects.schedule.unlinkedDescription", "vi", "Liên kết Dự án thiết kế với Dự án vận hành để lập và theo dõi tiến độ thiết kế chi tiết.", "Liên kết Hợp Đồng Thiết Kế với Dự án vận hành để lập và theo dõi tiến độ thiết kế chi tiết."),
        new("designProjects.schedule.unlinkedDescription", "en", "Link this design project to an operational project to plan and track its detail-design schedule.", "Link this design contract to an operational project to plan and track its detail-design schedule."),
        new("designProjects.schedule.emptyDescription", "vi", "Khởi tạo ba giai đoạn chuẩn với trọng số 34%, 33% và 33% theo khoảng ngày của Dự án thiết kế.", "Khởi tạo ba giai đoạn chuẩn với trọng số 34%, 33% và 33% theo khoảng ngày của Hợp Đồng Thiết Kế."),
        new("designProjects.schedule.emptyDescription", "zh", "按设计项目日期范围初始化三个标准阶段，权重为 34%、33% 和 33%。", "按设计合同日期范围初始化三个标准阶段，权重为 34%、33% 和 33%。"),
        new("designProjects.schedule.emptyDescription", "ja", "設計プロジェクトの期間に標準3段階を34%、33%、33%の重みで初期化します。", "設計契約の期間に標準3段階を34%、33%、33%の重みで初期化します。"),
        new("opportunities.action.createDesignProject", "vi", "Tạo dự án thiết kế", "Tạo hợp đồng thiết kế"),
        new("opportunities.action.createDesignProject", "en", "Create design project", "Create design contract"),
        new("opportunities.action.createDesignProject", "zh", "创建设计项目", "创建设计合同"),
        new("opportunities.action.createDesignProject", "ja", "設計プロジェクトを作成", "設計契約を作成"),
        new("opportunities.action.openDesignProject", "vi", "Mở dự án thiết kế", "Mở hợp đồng thiết kế"),
        new("opportunities.action.openDesignProject", "en", "Open design project", "Open design contract"),
        new("opportunities.action.openDesignProject", "zh", "打开设计项目", "打开设计合同"),
        new("opportunities.action.openDesignProject", "ja", "設計プロジェクトを開く", "設計契約を開く"),
        new("opportunities.action.designProjectCreated", "vi", "Đã tạo dự án thiết kế", "Đã tạo hợp đồng thiết kế"),
        new("opportunities.action.designProjectCreated", "en", "Design project created", "Design contract created"),
        new("opportunities.action.designProjectCreated", "zh", "设计项目已创建", "设计合同已创建"),
        new("opportunities.action.designProjectCreated", "ja", "設計プロジェクト作成済み", "設計契約作成済み"),
        new("opportunities.designProject.created", "vi", "Đã tạo dự án thiết kế ở giai đoạn Thiết kế ý tưởng.", "Đã tạo hợp đồng thiết kế ở giai đoạn Thiết kế ý tưởng."),
        new("opportunities.designProject.created", "en", "The design project was created at the Concept stage.", "The design contract was created at the Concept stage."),
        new("opportunities.designProject.created", "zh", "设计项目已在概念设计阶段创建。", "设计合同已在概念设计阶段创建。"),
        new("opportunities.designProject.created", "ja", "設計プロジェクトをコンセプト段階で作成しました。", "設計契約をコンセプト段階で作成しました。"),
        new("opportunities.designProject.alreadyExists", "vi", "Dự án thiết kế đã tồn tại; hệ thống không tạo bản ghi trùng.", "Hợp Đồng Thiết Kế đã tồn tại; hệ thống không tạo bản ghi trùng."),
        new("opportunities.designProject.alreadyExists", "en", "The design project already exists; no duplicate was created.", "The design contract already exists; no duplicate was created."),
        new("opportunities.designProject.alreadyExists", "zh", "设计项目已存在，未创建重复记录。", "设计合同已存在，未创建重复记录。"),
        new("opportunities.designProject.alreadyExists", "ja", "設計プロジェクトは既に存在するため、重複作成されませんでした。", "設計契約は既に存在するため、重複作成されませんでした。"),
        new("opportunities.designProject.nameRule", "vi", "Tên dự án thiết kế phải có 1–300 ký tự, ví dụ: Thiết kế nhà máy Alpha.", "Tên hợp đồng thiết kế phải có 1–300 ký tự, ví dụ: Thiết kế nhà máy Alpha."),
        new("opportunities.designProject.nameRule", "en", "Design project name must contain 1–300 characters, e.g. Alpha factory design.", "Design contract name must contain 1–300 characters, e.g. Alpha factory design."),
        new("opportunities.designProject.nameRule", "zh", "设计项目名称须为 1–300 个字符，例如：Alpha 工厂设计。", "设计合同名称须为 1–300 个字符，例如：Alpha 工厂设计。"),
        new("opportunities.designProject.nameRule", "ja", "設計プロジェクト名は1～300文字です。例：Alpha工場設計。", "設計契約名は1～300文字です。例：Alpha工場設計。"),
        new("opportunities.designProject.noteRule", "vi", "Ghi chú dự án thiết kế tối đa 4.000 ký tự.", "Ghi chú hợp đồng thiết kế tối đa 4.000 ký tự."),
        new("opportunities.designProject.noteRule", "en", "Design project note must be at most 4,000 characters.", "Design contract note must be at most 4,000 characters."),
        new("opportunities.designProject.noteRule", "zh", "设计项目备注最多 4,000 个字符。", "设计合同备注最多 4,000 个字符。"),
        new("opportunities.designProject.noteRule", "ja", "設計プロジェクトの備考は4,000文字以内です。", "設計契約の備考は4,000文字以内です。"),
        new("opportunities.designProject.lost", "vi", "Cơ hội đã đánh dấu Thua nên không thể tạo dự án thiết kế.", "Cơ hội đã đánh dấu Thua nên không thể tạo hợp đồng thiết kế."),
        new("opportunities.designProject.lost", "en", "A lost opportunity cannot start a design project.", "A lost opportunity cannot start a design contract."),
        new("opportunities.designProject.lost", "zh", "已标记为失败的商机不能创建设计项目。", "已标记为失败的商机不能创建设计合同。"),
        new("opportunities.designProject.lost", "ja", "失注した商談から設計プロジェクトは作成できません。", "失注した商談から設計契約は作成できません。"),
        new("contracts.timeline.action.designProjectEnsure", "vi", "Tạo hoặc liên kết Dự án thiết kế", "Tạo hoặc liên kết Hợp Đồng Thiết Kế"),
        new("contracts.timeline.action.designProjectEnsure", "en", "Design project created or linked", "Design contract created or linked"),
        new("contracts.timeline.action.designProjectEnsure", "zh", "已创建或关联设计项目", "已创建或关联设计合同"),
        new("contracts.timeline.action.designProjectEnsure", "ja", "設計プロジェクトを作成または連携", "設計契約を作成または連携"),
        new("contracts.designProject.title", "vi", "Dự án thiết kế", "Hợp Đồng Thiết Kế"),
        new("contracts.designProject.title", "en", "Design project", "Design contract"),
        new("contracts.designProject.title", "zh", "设计项目", "设计合同"),
        new("contracts.designProject.title", "ja", "設計プロジェクト", "設計契約"),
        new("contracts.designProject.pending", "vi", "Chuyển trạng thái hợp đồng không tự tạo dự án thiết kế. Kinh doanh có thể tạo dự án thiết kế từ Cơ hội khi chưa có hợp đồng.", "Chuyển trạng thái hợp đồng không tự tạo hợp đồng thiết kế. Kinh doanh có thể tạo hợp đồng thiết kế từ Cơ hội khi chưa có hợp đồng."),
        new("contracts.designProject.pending", "en", "Changing contract status does not create a design project. Sales can create one from the opportunity before a contract exists.", "Changing contract status does not create a design contract. Sales can create one from the opportunity before a contract exists."),
        new("contracts.designProject.pending", "zh", "变更合同状态不会自动创建设计项目。销售人员可在签约前从商机创建设计项目。", "变更合同状态不会自动创建设计合同。销售人员可在签约前从商机创建设计合同。"),
        new("contracts.designProject.pending", "ja", "契約のステータス変更では設計プロジェクトは自動作成されません。営業担当者は契約前に商談から作成できます。", "契約のステータス変更では設計契約は自動作成されません。営業担当者は契約前に商談から作成できます。"),
        new("contracts.designProject.missing", "vi", "Hợp đồng chưa có dự án thiết kế. Chỉ chọn Tạo dự án thiết kế nếu thực sự cần một dự án mới.", "Hợp đồng chưa có hợp đồng thiết kế. Chỉ chọn Tạo hợp đồng thiết kế nếu thực sự cần một dự án mới."),
        new("contracts.designProject.missing", "en", "This contract has no design project. Select Create design project only if a new project is needed.", "This contract has no design contract. Select Create design contract only if a new project is needed."),
        new("contracts.designProject.missing", "zh", "此合同尚无设计项目。仅在确实需要新项目时选择创建设计项目。", "此合同尚无设计合同。仅在确实需要新项目时选择创建设计合同。"),
        new("contracts.designProject.missing", "ja", "この契約には設計プロジェクトがありません。新規作成が必要な場合のみ作成を選択してください。", "この契約には設計契約がありません。新規作成が必要な場合のみ作成を選択してください。"),
        new("contracts.designProject.create", "vi", "Tạo dự án thiết kế", "Tạo hợp đồng thiết kế"),
        new("contracts.designProject.create", "en", "Create design project", "Create design contract"),
        new("contracts.designProject.create", "zh", "创建设计项目", "创建设计合同"),
        new("contracts.designProject.create", "ja", "設計プロジェクトを作成", "設計契約を作成"),
        new("contracts.designProject.created", "vi", "Đã tạo dự án thiết kế.", "Đã tạo hợp đồng thiết kế."),
        new("contracts.designProject.created", "en", "Design project created.", "Design contract created."),
        new("contracts.designProject.created", "zh", "设计项目已创建。", "设计合同已创建。"),
        new("contracts.designProject.created", "ja", "設計プロジェクトを作成しました。", "設計契約を作成しました。"),
        new("rbac.perm.design.projects.view.label", "vi", "Xem dự án thiết kế", "Xem hợp đồng thiết kế"),
        new("rbac.perm.design.projects.view.label", "en", "View design projects", "View design contracts"),
        new("rbac.perm.design.projects.view.label", "zh", "查看设计项目", "查看设计合同"),
        new("rbac.perm.design.projects.view.label", "ja", "設計プロジェクトを表示", "設計契約を表示"),
        new("rbac.perm.design.projects.view.description", "vi", "Cho phép xem dự án thiết kế và tiến độ 3 giai đoạn.", "Cho phép xem hợp đồng thiết kế và tiến độ 3 giai đoạn."),
        new("rbac.perm.design.projects.view.description", "en", "Allows viewing design projects and the 3-stage progress board.", "Allows viewing design contracts and the 3-stage progress board."),
        new("rbac.perm.design.projects.view.description", "zh", "允许查看设计项目及三阶段进度看板。", "允许查看设计合同及三阶段进度看板。"),
        new("rbac.perm.design.projects.view.description", "ja", "設計プロジェクトと3段階の進捗ボードの閲覧を許可します。", "設計契約と3段階の進捗ボードの閲覧を許可します。"),
        new("rbac.perm.design.projects.manage.label", "vi", "Quản lý dự án thiết kế", "Quản lý hợp đồng thiết kế"),
        new("rbac.perm.design.projects.manage.label", "en", "Manage design projects", "Manage design contracts"),
        new("rbac.perm.design.projects.manage.label", "zh", "管理设计项目", "管理设计合同"),
        new("rbac.perm.design.projects.manage.label", "ja", "設計プロジェクトを管理", "設計契約を管理"),
        new("rbac.perm.design.projects.manage.description", "vi", "Cho phép tạo/sửa dự án thiết kế và cấu hình đội ngũ bộ môn.", "Cho phép tạo/sửa hợp đồng thiết kế và cấu hình đội ngũ bộ môn."),
        new("rbac.perm.design.projects.manage.description", "en", "Allows creating/editing design projects and configuring discipline teams.", "Allows creating/editing design contracts and configuring discipline teams."),
        new("rbac.perm.design.projects.manage.description", "zh", "允许创建/编辑设计项目并配置专业团队。", "允许创建/编辑设计合同并配置专业团队。"),
        new("rbac.perm.design.projects.manage.description", "ja", "設計プロジェクトの作成・編集及び専門チームの設定を許可します。", "設計契約の作成・編集及び専門チームの設定を許可します。"),
        new("deletionImpact.item.customer.designProjects", "vi", "Dự án thiết kế phải được xử lý trước", "Hợp Đồng Thiết Kế phải được xử lý trước"),
        new("deletionImpact.item.customer.designProjects", "en", "Design projects that must be resolved first", "Design contracts that must be resolved first"),
        new("deletionImpact.item.customer.designProjects", "zh", "必须先处理的设计项目", "必须先处理的设计合同"),
        new("deletionImpact.item.customer.designProjects", "ja", "先に処理が必要な設計プロジェクト", "先に処理が必要な設計契約"),
        new("deletionImpact.item.contract.designProjects", "vi", "Dự án thiết kế sẽ được gỡ liên kết hợp đồng", "Hợp Đồng Thiết Kế sẽ được gỡ liên kết hợp đồng"),
        new("deletionImpact.item.contract.designProjects", "en", "Design projects to unlink from the contract", "Design contracts to unlink from the contract"),
        new("deletionImpact.item.contract.designProjects", "zh", "将与合同解除关联的设计项目", "将与合同解除关联的设计合同"),
        new("deletionImpact.item.contract.designProjects", "ja", "契約とのリンクを解除する設計プロジェクト", "契約とのリンクを解除する設計契約"),
        new("deletionImpact.item.operations.designProjects", "vi", "Dự án thiết kế", "Hợp Đồng Thiết Kế"),
        new("deletionImpact.item.operations.designProjects", "en", "Design projects", "Design contracts"),
        new("deletionImpact.item.operations.designProjects", "zh", "设计项目", "设计合同"),
        new("deletionImpact.item.operations.designProjects", "ja", "設計プロジェクト", "設計契約"),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var rename in Renames)
        {
            migrationBuilder.Sql($"""
                UPDATE translations
                SET Value = {Literal(rename.Value)}, UpdatedAt = SYSUTCDATETIME()
                WHERE [Key] = {Literal(rename.Key)}
                  AND LanguageCode = {Literal(rename.Language)}
                  AND Value = {Literal(rename.PreviousValue)};
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var rename in Renames)
        {
            migrationBuilder.Sql($"""
                UPDATE translations
                SET Value = {Literal(rename.PreviousValue)}, UpdatedAt = SYSUTCDATETIME()
                WHERE [Key] = {Literal(rename.Key)}
                  AND LanguageCode = {Literal(rename.Language)}
                  AND Value = {Literal(rename.Value)};
                """);
        }
    }

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
}
