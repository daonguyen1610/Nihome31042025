using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// TranslationSeeder only inserts missing keys, so later wording changes in
    /// the seed files never reached existing databases. The demo database still
    /// showed the old sales-pipeline stage names (Tìm hiểu / Đánh giá / Đề xuất /
    /// Đàm phán / Thắng) instead of the customer's terms from Nicon-QLVH.md.
    /// Each row below updates only while it still holds the exact earlier seed
    /// value it was found with (every one appears in the seed history), so
    /// labels an administrator edited in /admin/translations are left alone.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261003090000_SyncDriftedTranslationLabels")]
    public partial class SyncDriftedTranslationLabels : Migration
    {
        private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("auth.err.accountNotFound", "en", "No account found with this phone number.", "No account found with this phone number or email."),
            new("auth.err.accountNotFound", "ja", "この電話番号のアカウントが見つかりません。", "この電話番号またはメールアドレスのアカウントが見つかりません。"),
            new("auth.err.accountNotFound", "vi", "Không tìm thấy tài khoản với số điện thoại này.", "Không tìm thấy tài khoản với số điện thoại hoặc email này."),
            new("auth.err.accountNotFound", "zh", "未找到该手机号对应的账户。", "未找到该手机号或邮箱对应的账户。"),
            new("auth.err.invalidCredentials", "en", "Invalid phone number or password.", "Invalid phone number, email, or password."),
            new("auth.err.invalidCredentials", "ja", "電話番号またはパスワードが正しくありません。", "電話番号、メールアドレス、またはパスワードが正しくありません。"),
            new("auth.err.invalidCredentials", "vi", "Số điện thoại hoặc mật khẩu không đúng.", "Số điện thoại, email hoặc mật khẩu không đúng."),
            new("auth.err.invalidCredentials", "zh", "手机号或密码错误。", "手机号、邮箱或密码错误。"),
            new("contracts.detail.tab.timeline", "vi", "Timeline", "Lịch sử"),
            new("contracts.numberHint", "en", "Leave blank to auto-generate HD-YYYY-NNNN.", "A number is generated in the HD-YYYY-NNNN format; you can edit it."),
            new("contracts.numberHint", "ja", "空欄の場合、システムが HD-YYYY-NNNN 形式で自動採番します。", "HD-YYYY-NNNN 形式の番号が自動生成されます。必要に応じて編集できます。"),
            new("contracts.numberHint", "vi", "Để trống để hệ thống tự sinh theo mẫu HD-YYYY-NNNN.", "Hệ thống tạo sẵn theo mẫu HD-YYYY-NNNN; bạn có thể chỉnh sửa."),
            new("contracts.numberHint", "zh", "留空时系统自动按 HD-YYYY-NNNN 生成。", "系统已按 HD-YYYY-NNNN 格式生成编号；您可以修改。"),
            new("leads.convert.companyNotice", "en", "This lead has a company name, so a company customer will be created. Tax code, registered address and legal representative are required.", "This lead has a company name, so a company customer will be created. Registered address and legal representative are required; the tax code can be added later."),
            new("leads.convert.companyNotice", "ja", "このリードには会社名があるため法人顧客を作成します。税番号、登記住所、代表者が必要です。", "このリードには会社名があるため法人顧客を作成します。登記住所と代表者は必須です。税番号は後から追加できます。"),
            new("leads.convert.companyNotice", "vi", "Lead này có tên công ty nên sẽ tạo khách hàng doanh nghiệp. Cần bổ sung mã số thuế, địa chỉ đăng ký và người đại diện.", "Lead này có tên công ty nên sẽ tạo khách hàng doanh nghiệp. Địa chỉ đăng ký và người đại diện là bắt buộc; mã số thuế có thể bổ sung sau."),
            new("leads.convert.companyNotice", "zh", "该线索含公司名称，将创建企业客户。需填写税号、注册地址和法定代表人。", "该线索含公司名称，将创建企业客户。注册地址和法定代表人为必填项；税号可稍后补充。"),
            new("leads.unconvert.confirm", "en", "Undo this lead conversion? The customer and opportunity are deleted only if this conversion created them and nothing else references them.", "Undo this lead conversion? The customer is always preserved; the opportunity is deleted only if this conversion created it and nothing else references it."),
            new("leads.unconvert.confirm", "ja", "このリード変換を取り消しますか？顧客と商談は、本変換で作成され参照がない場合のみ削除されます。", "このリード変換を取り消しますか？顧客は常に保持され、商談は本変換で作成され参照がない場合のみ削除されます。"),
            new("leads.unconvert.confirm", "vi", "Hoàn tác chuyển đổi lead này? Khách hàng và cơ hội chỉ bị xoá nếu chúng do lần chuyển đổi này tạo ra và chưa phát sinh dữ liệu.", "Hoàn tác chuyển đổi lead này? Khách hàng luôn được giữ lại; cơ hội chỉ bị xoá nếu do lần chuyển đổi này tạo ra và chưa phát sinh dữ liệu."),
            new("leads.unconvert.confirm", "zh", "撤销此线索转换？仅当客户和商机由本次转换创建且无关联数据时才会删除。", "撤销此线索转换？客户将始终保留；仅当商机由本次转换创建且无关联数据时才会删除。"),
            new("leads.unconvert.done.DeletedBoth", "en", "Undone. The newly created customer and opportunity were removed.", "Legacy conversion data was undone. Review the related records."),
            new("leads.unconvert.done.DeletedBoth", "ja", "取り消しました。新規作成した顧客と商談を削除しました。", "旧形式の変換データを取り消しました。関連レコードを確認してください。"),
            new("leads.unconvert.done.DeletedBoth", "vi", "Đã hoàn tác. Khách hàng và cơ hội vừa tạo đã được xoá.", "Đã hoàn tác dữ liệu chuyển đổi cũ. Vui lòng kiểm tra các bản ghi liên quan."),
            new("leads.unconvert.done.DeletedBoth", "zh", "已撤销。新建的客户和商机已删除。", "旧版转换数据已撤销。请检查相关记录。"),
            new("leads.unconvert.done.DeletedOpportunity", "en", "Undone. The new opportunity was removed; the existing customer was kept.", "Undone. The new opportunity was removed; the customer was preserved."),
            new("leads.unconvert.done.DeletedOpportunity", "ja", "取り消しました。新規商談を削除し、既存顧客は保持しました。", "取り消しました。新規商談を削除し、顧客は保持しました。"),
            new("leads.unconvert.done.DeletedOpportunity", "vi", "Đã hoàn tác. Cơ hội vừa tạo đã xoá, khách hàng có sẵn được giữ nguyên.", "Đã hoàn tác. Cơ hội vừa tạo đã xoá; khách hàng được giữ nguyên."),
            new("leads.unconvert.done.DeletedOpportunity", "zh", "已撤销。新建商机已删除，原有客户保留。", "已撤销。新建商机已删除，客户已保留。"),
            new("masterData.opportunity_stage.negotiation.label", "vi", "Đàm phán", "Thương thảo"),
            new("masterData.opportunity_stage.proposal.label", "en", "Proposal", "Quotation / Tender"),
            new("masterData.opportunity_stage.proposal.label", "ja", "提案", "見積／入札"),
            new("masterData.opportunity_stage.proposal.label", "vi", "Đề xuất", "Báo giá / Đấu thầu"),
            new("masterData.opportunity_stage.proposal.label", "zh", "提案", "报价／投标"),
            new("masterData.opportunity_stage.prospecting.label", "en", "Prospecting", "Approach"),
            new("masterData.opportunity_stage.prospecting.label", "ja", "見込み調査", "アプローチ"),
            new("masterData.opportunity_stage.prospecting.label", "vi", "Tìm hiểu", "Tiếp cận"),
            new("masterData.opportunity_stage.prospecting.label", "zh", "开发中", "接洽"),
            new("masterData.opportunity_stage.qualification.label", "en", "Qualification", "Survey"),
            new("masterData.opportunity_stage.qualification.label", "ja", "適格性判定", "現地調査"),
            new("masterData.opportunity_stage.qualification.label", "vi", "Đánh giá", "Khảo sát"),
            new("masterData.opportunity_stage.qualification.label", "zh", "评估", "勘察"),
            new("masterData.opportunity_stage.won.label", "en", "Won", "Contract signed"),
            new("masterData.opportunity_stage.won.label", "ja", "受注", "契約締結"),
            new("masterData.opportunity_stage.won.label", "vi", "Thắng", "Ký hợp đồng"),
            new("masterData.opportunity_stage.won.label", "zh", "赢单", "合同已签署"),
            new("opportunities.action.markWon", "en", "Mark as Won", "Confirm contract signed"),
            new("opportunities.action.markWon", "ja", "受注に変更", "契約締結を確認"),
            new("opportunities.action.markWon", "vi", "Đánh dấu Thắng", "Xác nhận ký hợp đồng"),
            new("opportunities.action.markWon", "zh", "标记为成交", "确认合同已签署"),
            new("opportunities.stage.Negotiation", "ja", "交渉", "契約交渉"),
            new("opportunities.stage.Negotiation", "vi", "Đàm phán", "Thương thảo"),
            new("opportunities.stage.Negotiation", "zh", "谈判", "商务谈判"),
            new("opportunities.stage.Proposal", "en", "Proposal", "Quotation / Tender"),
            new("opportunities.stage.Proposal", "ja", "提案", "見積／入札"),
            new("opportunities.stage.Proposal", "vi", "Đề xuất", "Báo giá / Đấu thầu"),
            new("opportunities.stage.Proposal", "zh", "提案", "报价／投标"),
            new("opportunities.stage.Prospecting", "en", "Prospecting", "Approach"),
            new("opportunities.stage.Prospecting", "ja", "リサーチ", "アプローチ"),
            new("opportunities.stage.Prospecting", "vi", "Tìm hiểu", "Tiếp cận"),
            new("opportunities.stage.Prospecting", "zh", "调研", "接洽"),
            new("opportunities.stage.Qualification", "en", "Qualification", "Survey"),
            new("opportunities.stage.Qualification", "ja", "評価", "現地調査"),
            new("opportunities.stage.Qualification", "vi", "Đánh giá", "Khảo sát"),
            new("opportunities.stage.Qualification", "zh", "评估", "勘察"),
            new("opportunities.stage.Won", "en", "Won", "Contract signed"),
            new("opportunities.stage.Won", "ja", "受注", "契約締結"),
            new("opportunities.stage.Won", "vi", "Thắng", "Ký hợp đồng"),
            new("opportunities.stage.Won", "zh", "成交", "合同已签署"),
            new("opportunities.stage.won.description", "en", "Optionally pick a linked Quote or Tender (will become required once those modules ship).", "The opportunity can complete only after a linked contract has a valid signed date."),
            new("opportunities.stage.won.description", "ja", "関連する見積または入札を任意で選択できます（該当モジュールが実装され次第、必須になります）。", "有効な締結日を持つ関連契約がある場合のみ商談を完了できます。"),
            new("opportunities.stage.won.description", "vi", "Chọn Báo giá hoặc Gói thầu liên kết (tuỳ chọn — sẽ bắt buộc khi module Báo giá / Đấu thầu sẵn sàng).", "Cơ hội chỉ hoàn tất khi đã có hợp đồng liên kết với ngày ký hợp lệ."),
            new("opportunities.stage.won.description", "zh", "可选择关联报价或标段（相关模块上线后将变为必填）。", "仅当关联合同具有有效签署日期时，商机才能完成。"),
            new("opportunities.stage.won.title", "en", "Move to Won", "Confirm contract signed"),
            new("opportunities.stage.won.title", "ja", "受注に変更", "契約締結を確認"),
            new("opportunities.stage.won.title", "vi", "Chuyển sang Thắng", "Xác nhận ký hợp đồng"),
            new("opportunities.stage.won.title", "zh", "标记为成交", "确认合同已签署"),
            new("opportunities.subtitle", "en", "Lead → Opportunity → Quote → Contract pipeline — track win probability and close reasons.", "Approach → Survey → Quotation/Tender → Negotiation → Contract signed."),
            new("opportunities.subtitle", "ja", "リード → 商談 → 見積 → 契約 のパイプラインと勝敗理由を可視化します。", "アプローチ → 現地調査 → 見積／入札 → 交渉 → 契約締結。"),
            new("opportunities.subtitle", "vi", "Pipeline Lead → Cơ hội → Báo giá → Hợp đồng — theo dõi xác suất chốt và lý do thắng thua.", "Tiếp cận → Khảo sát → Báo giá/Đấu thầu → Thương thảo → Ký hợp đồng."),
            new("opportunities.subtitle", "zh", "线索 → 商机 → 报价 → 合同 管道，追踪成交率与胜败原因。", "接洽 → 勘察 → 报价／投标 → 商务谈判 → 合同签署。"),
            new("procurement.field.receivedQuantity", "en", "Received quantity", "Received"),
            new("procurement.field.receivedQuantity", "ja", "入庫数量", "入庫済み"),
            new("procurement.field.receivedQuantity", "vi", "Số lượng nhận", "Đã nhận"),
            new("procurement.field.receivedQuantity", "zh", "收货数量", "已收货"),
            new("profilePage.nav.about", "en", "About", "About us"),
            new("profilePage.nav.about", "ja", "概要", "会社概要"),
            new("profilePage.nav.about", "vi", "Giới thiệu", "Về chúng tôi"),
            new("profilePage.nav.about", "zh", "介绍", "关于我们"),
            new("profilePage.nav.certs", "en", "Certifications", "Certifications & awards"),
            new("profilePage.nav.certs", "ja", "認証", "認証・受賞"),
            new("profilePage.nav.certs", "vi", "Chứng nhận", "Chứng nhận & giải thưởng"),
            new("profilePage.nav.certs", "zh", "认证", "认证与奖项"),
            new("profilePage.nav.downloads", "en", "Downloads", "Catalogue"),
            new("profilePage.nav.downloads", "ja", "資料", "カタログ"),
            new("profilePage.nav.downloads", "vi", "Tài liệu", "Catalogue"),
            new("profilePage.nav.downloads", "zh", "资料下载", "目录"),
            new("profilePage.nav.org", "en", "Organization", "Organization chart"),
            new("profilePage.nav.org", "ja", "組織", "組織図"),
            new("profilePage.nav.org", "vi", "Tổ chức", "Sơ đồ tổ chức"),
            new("profilePage.nav.org", "zh", "组织", "组织架构"),
            new("profilePage.nav.timeline", "en", "History", "Company history"),
            new("profilePage.nav.timeline", "vi", "Lịch sử", "Lịch sử hoạt động"),
            new("profilePage.nav.timeline", "zh", "历史", "公司历史"),
            new("quotes.boq.name", "en", "Item", "Item name"),
            new("quotes.boq.name", "ja", "項目", "項目名"),
            new("quotes.boq.name", "zh", "项目", "项目名称"),
            new("rbac.perm.crm.contracts.view.all.description", "en", "Bypasses owner filtering — Sales Manager / Accountant / BOD / Admin can see contracts owned by any user.", "Bypasses owner filtering — Sales Manager / BOD / Admin can see contracts owned by any user."),
            new("rbac.perm.crm.contracts.view.all.description", "ja", "担当者フィルタを無視 — セールスマネージャー・経理・役員・管理者は全担当者の契約を閲覧可能。", "担当者フィルタを無視 — セールスマネージャー・役員・管理者は全担当者の契約を閲覧可能。"),
            new("rbac.perm.crm.contracts.view.all.description", "vi", "Bỏ qua bộ lọc chủ sở hữu — Sales Manager / Kế toán / BGĐ / Admin thấy hợp đồng của mọi Sales.", "Bỏ qua bộ lọc chủ sở hữu — Sales Manager / BGĐ / Admin thấy hợp đồng của mọi Sales."),
            new("rbac.perm.crm.contracts.view.all.description", "zh", "忽略负责人过滤 — 销售经理 / 会计 / 董事会 / 管理员可查看所有销售的合同。", "忽略负责人过滤 — 销售经理 / 董事会 / 管理员可查看所有销售的合同。"),
            new("rbac.perm.crm.surveys.manage.description", "en", "Allows creating/editing surveys and syncing media to Drive.", "Allows creating and editing surveys and syncing media within assigned or created/managed project scope."),
            new("rbac.perm.crm.surveys.manage.description", "ja", "現場調査の作成・編集及びファイルのDrive同期を許可します。", "割り当て済み、または作成・管理するプロジェクト範囲内で調査の作成・編集とメディア同期を許可します。"),
            new("rbac.perm.crm.surveys.manage.description", "vi", "Cho phép tạo/sửa phiếu khảo sát và đồng bộ file lên Drive.", "Cho phép tạo/sửa phiếu khảo sát và đồng bộ file trong phạm vi phiếu được giao hoặc dự án do người dùng tạo/phụ trách."),
            new("rbac.perm.crm.surveys.manage.description", "zh", "允许创建、编辑勘察并将文件同步至云盘。", "允许在已分配或用户创建/管理的项目范围内创建、编辑勘察并同步媒体。"),
            new("rbac.perm.crm.surveys.view.description", "en", "Allows viewing site surveys and attached media.", "Allows viewing surveys and media assigned to the user or within projects they created or manage."),
            new("rbac.perm.crm.surveys.view.description", "ja", "現場調査記録と添付メディアの閲覧を許可します。", "ユーザーに割り当てられた調査、または作成・管理するプロジェクト内の調査とメディアの閲覧を許可します。"),
            new("rbac.perm.crm.surveys.view.description", "vi", "Cho phép xem phiếu khảo sát hiện trường và media đính kèm.", "Cho phép xem phiếu khảo sát và media trong phạm vi phiếu được giao hoặc dự án do người dùng tạo/phụ trách."),
            new("rbac.perm.crm.surveys.view.description", "zh", "允许查看现场勘察记录和附加媒体。", "允许查看分配给用户或属于其创建或管理项目的勘察及媒体。"),
            new("surveys.media.deleteDescription", "en", "The file will be removed from private storage and Google Drive if synced. This cannot be undone.", "The file will be removed from private storage. If synced, the Google Drive copy will be moved to trash and can be restored by a Drive administrator."),
            new("surveys.media.deleteDescription", "ja", "ファイルは非公開ストレージから削除され、同期済みの場合は Google Drive からも削除されます。元に戻せません。", "ファイルは非公開ストレージから削除されます。同期済みの場合、Google Drive のコピーはゴミ箱へ移動され、Drive 管理者が復元できます。"),
            new("surveys.media.deleteDescription", "vi", "Tệp sẽ bị xoá khỏi vùng lưu trữ riêng và Google Drive nếu đã đồng bộ. Hành động này không thể hoàn tác.", "Tệp sẽ bị xoá khỏi vùng lưu trữ riêng. Nếu đã đồng bộ, bản sao Google Drive sẽ được chuyển vào thùng rác và quản trị viên Drive có thể khôi phục."),
            new("surveys.media.deleteDescription", "zh", "文件将从私有存储中删除；若已同步，也会从 Google Drive 删除。此操作无法撤销。", "文件将从私有存储中删除。若已同步，Google Drive 副本将移至回收站，并可由 Drive 管理员恢复。"),
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
}
