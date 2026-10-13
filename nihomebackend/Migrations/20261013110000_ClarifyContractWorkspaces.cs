using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// Renames the canonical Contract registry and the two Finance projections
    /// without overwriting administrator-customized translations.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013110000_ClarifyContractWorkspaces")]
    public partial class ClarifyContractWorkspaces : Migration
    {
        private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("nav.contracts", "vi", "Hợp đồng", "Sổ hợp đồng"),
            new("nav.contracts", "en", "Contracts", "Contract registry"),
            new("nav.contracts", "zh", "合同", "合同台账"),
            new("nav.contracts", "ja", "契約", "契約台帳"),
            new("nav.primaryContracts", "vi", "Hợp đồng chính", "Theo dõi phải thu"),
            new("nav.primaryContracts", "en", "Primary contracts", "Receivables"),
            new("nav.primaryContracts", "zh", "主合同", "应收跟踪"),
            new("nav.primaryContracts", "ja", "主要契約", "売掛金管理"),
            new("nav.inputContracts", "vi", "Hợp đồng đầu vào", "Theo dõi phải trả"),
            new("nav.inputContracts", "en", "Input contracts", "Payables"),
            new("nav.inputContracts", "zh", "下游合同", "应付跟踪"),
            new("nav.inputContracts", "ja", "下流契約", "買掛金管理"),
            new("contracts.title", "vi", "Hợp đồng", "Sổ hợp đồng"),
            new("contracts.title", "en", "Contracts", "Contract registry"),
            new("contracts.title", "zh", "销售合同", "合同台账"),
            new("contracts.title", "ja", "販売契約", "契約台帳"),
            new("contracts.subtitle", "vi", "Danh sách hợp đồng bán ra theo Sales, khách hàng và tiến độ thực hiện.", "Nguồn dữ liệu hợp đồng dùng chung cho CRM, Tài chính và các module vận hành."),
            new("contracts.subtitle", "en", "Sales contracts filtered by owner, customer and execution timeline.", "The shared contract source of truth for CRM, Finance and operational modules."),
            new("contracts.subtitle", "zh", "按负责人、客户和执行时间线过滤的销售合同列表。", "CRM、财务和运营模块共用的合同唯一数据源。"),
            new("contracts.subtitle", "ja", "担当者・顧客・進行状況で絞り込む販売契約一覧。", "CRM、財務、運用モジュールで共有する契約の唯一の管理台帳。"),
            new("contracts.primary.title", "vi", "Hợp đồng chính", "Theo dõi phải thu"),
            new("contracts.primary.title", "en", "Primary contracts", "Receivables monitoring"),
            new("contracts.primary.title", "zh", "主合同", "应收跟踪"),
            new("contracts.primary.title", "ja", "主要契約", "売掛金管理"),
            new("contracts.primary.subtitle", "vi", "Hợp đồng đầu ra với chủ đầu tư, theo dự án vận hành và phạm vi quyền được cấp.", "Theo dõi giá trị, mốc thu và công nợ của hợp đồng đầu ra theo dự án và phạm vi được cấp."),
            new("contracts.primary.subtitle", "en", "Upstream client contracts by operational project and authorized scope.", "Monitor values, collection milestones and receivables for upstream contracts within the authorized scope."),
            new("contracts.primary.subtitle", "zh", "按运营项目和授权范围管理与业主签订的上游合同。", "按授权范围跟踪上游合同的金额、收款节点和应收款。"),
            new("contracts.primary.subtitle", "ja", "運用プロジェクトと権限範囲別の顧客向け上流契約。", "権限範囲内の上流契約について、金額・回収マイルストーン・売掛金を追跡します。"),
            new("contracts.input.title", "vi", "Hợp đồng đầu vào", "Theo dõi phải trả"),
            new("contracts.input.title", "en", "Input contracts", "Payables monitoring"),
            new("contracts.input.title", "zh", "下游合同", "应付跟踪"),
            new("contracts.input.title", "ja", "下流契約", "買掛金管理"),
            new("contracts.input.subtitle", "vi", "Hợp đồng nhà cung cấp và thầu phụ, nghĩa vụ thanh toán theo dự án vận hành và phạm vi quyền được cấp.", "Theo dõi giá trị, mốc chi và công nợ của hợp đồng nhà cung cấp, thầu phụ theo dự án và phạm vi được cấp."),
            new("contracts.input.subtitle", "en", "Supplier and subcontractor contracts and payment obligations by operational project and authorized scope.", "Monitor values, payment milestones and payables for supplier and subcontractor contracts within the authorized scope."),
            new("contracts.input.subtitle", "zh", "按运营项目和授权范围管理供应商、分包合同及付款义务。", "按授权范围跟踪供应商和分包合同的金额、付款节点和应付款。"),
            new("contracts.input.subtitle", "ja", "運用プロジェクトと権限範囲別の仕入先・下請契約および支払義務。", "権限範囲内の仕入先・下請契約について、金額・支払マイルストーン・買掛金を追跡します。"),
            new("contracts.primary.directionLockedHint", "vi", "Màn hình Finance chỉ tạo hợp đồng chính với chủ đầu tư.", "Không gian Tài chính chỉ theo dõi hợp đồng đầu ra; dữ liệu gốc được quản trị tại Sổ hợp đồng."),
            new("contracts.primary.directionLockedHint", "en", "Finance creates upstream client contracts on this screen only.", "Finance monitors upstream contracts here; master data is managed in the Contract registry."),
            new("contracts.primary.directionLockedHint", "zh", "财务在此页面仅创建上游业主合同。", "财务在此跟踪上游合同；主数据在合同台账中管理。"),
            new("contracts.primary.directionLockedHint", "ja", "Financeはこの画面で上流顧客契約のみ作成します。", "財務では上流契約を追跡し、マスターデータは契約台帳で管理します。"),
            new("contracts.input.directionLockedHint", "vi", "Màn hình này chỉ tạo hợp đồng đầu vào với nhà cung cấp hoặc thầu phụ.", "Không gian Tài chính chỉ theo dõi hợp đồng đầu vào; dữ liệu gốc được quản trị tại Sổ hợp đồng."),
            new("contracts.input.directionLockedHint", "en", "This screen creates downstream supplier or subcontractor contracts only.", "Finance monitors downstream contracts here; master data is managed in the Contract registry."),
            new("contracts.input.directionLockedHint", "zh", "此页面仅创建下游供应商或分包合同。", "财务在此跟踪下游合同；主数据在合同台账中管理。"),
            new("contracts.input.directionLockedHint", "ja", "この画面では仕入先または下請の下流契約のみ作成します。", "財務では下流契約を追跡し、マスターデータは契約台帳で管理します。"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, reverse: false);

        protected override void Down(MigrationBuilder migrationBuilder) => Apply(migrationBuilder, reverse: true);

        private static void Apply(MigrationBuilder migrationBuilder, bool reverse)
        {
            foreach (var rename in Renames)
            {
                var from = reverse ? rename.Value : rename.PreviousValue;
                var to = reverse ? rename.PreviousValue : rename.Value;
                migrationBuilder.Sql($"""
                    UPDATE translations
                    SET Value = {Literal(to)}, UpdatedAt = SYSUTCDATETIME()
                    WHERE [Key] = {Literal(rename.Key)}
                      AND LanguageCode = {Literal(rename.Language)}
                      AND Value = {Literal(from)};
                    """);
            }
        }

        private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
