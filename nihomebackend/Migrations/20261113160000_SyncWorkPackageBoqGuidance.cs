using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <summary>
    /// 13/11/2026: Procurement uploads the project BOQ in the input-price work package instead of
    /// the project page. TranslationSeeder only inserts missing keys, so the two guidance texts
    /// that named the old place are updated here, only while they still hold the earlier seed
    /// value, leaving administrator edits alone.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261113160000_SyncWorkPackageBoqGuidance")]
    public partial class SyncWorkPackageBoqGuidance : Migration
    {
        private sealed record Rename(string Key, string Language, string PreviousValue, string Value);

        private static readonly Rename[] Renames =
        [
            new("boqWorkbook.projectSection.hint", "en", "Each project has one root BOQ: the tender BOQ, which becomes the contract BOQ once the contract is signed. QS uploads the filled form; permitted departments view it.", "Each project has one root BOQ: the tender BOQ, which becomes the contract BOQ once the contract is signed. Procurement uploads it in the input-price work package and the package lead approves it; permitted departments view it here."),
            new("boqWorkbook.projectSection.hint", "ja", "各プロジェクトには基準BOQが1つあります。入札BOQは契約締結後に契約BOQになります。QSがテンプレートでアップロードし、権限のある部門が閲覧します。", "各プロジェクトには基準BOQが1つあります。入札BOQは契約締結後に契約BOQになります。購買部が仕入見積のワークパッケージでアップロードし、担当者が承認します。権限のある部門はここで閲覧します。"),
            new("boqWorkbook.projectSection.hint", "vi", "Mỗi dự án có một BOQ gốc: BOQ dự thầu, khi ký hợp đồng thì thành BOQ hợp đồng. QS upload file theo mẫu; các phòng có quyền cùng xem.", "Mỗi dự án có một BOQ gốc: BOQ dự thầu, khi ký hợp đồng thì thành BOQ hợp đồng. Cung ứng upload BOQ trong nhóm công việc yêu cầu giá đầu vào và người phụ trách nhóm duyệt; các phòng có quyền cùng xem tại đây."),
            new("boqWorkbook.projectSection.hint", "zh", "每个项目有一份基准BOQ：投标BOQ，合同签订后成为合同BOQ。QS按模板上传文件，有权限的部门共同查看。", "每个项目有一份基准BOQ：投标BOQ，合同签订后成为合同BOQ。采购部在进货报价工作组中上传，由工作组负责人审批；有权限的部门在此查看。"),
            new("quotes.projectBoq.none", "en", "The project has no approved BOQ yet. QS uploads and submits the BOQ on the project page.", "The project has no approved BOQ yet. Procurement uploads it in the input-price work package for the package lead to approve."),
            new("quotes.projectBoq.none", "ja", "プロジェクトに承認済みBOQがありません。QSがプロジェクト画面でBOQをアップロードし申請します。", "プロジェクトに承認済みBOQがありません。購買部が仕入見積のワークパッケージでアップロードし、担当者が承認します。"),
            new("quotes.projectBoq.none", "vi", "Dự án chưa có BOQ đã duyệt. QS cần upload và trình duyệt BOQ tại trang Dự án.", "Dự án chưa có BOQ đã duyệt. Cung ứng upload BOQ trong nhóm công việc yêu cầu giá đầu vào để người phụ trách nhóm duyệt."),
            new("quotes.projectBoq.none", "zh", "项目尚无已批准的BOQ。QS需在项目页面上传并提交BOQ审批。", "项目尚无已批准的BOQ。采购部需在进货报价工作组中上传，由工作组负责人审批。"),
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
