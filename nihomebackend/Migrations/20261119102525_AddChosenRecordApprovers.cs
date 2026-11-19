using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddChosenRecordApprovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalApproverUserId",
                table: "project_boq_revisions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalApproverUserId",
                table: "procurement_input_price_offers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalApproverUserId",
                table: "department_finance_reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boq_revisions_ApprovalApproverUserId",
                table: "project_boq_revisions",
                column: "ApprovalApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_procurement_input_price_offers_ApprovalApproverUserId",
                table: "procurement_input_price_offers",
                column: "ApprovalApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_department_finance_reviews_ApprovalApproverUserId",
                table: "department_finance_reviews",
                column: "ApprovalApproverUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_department_finance_reviews_users_ApprovalApproverUserId",
                table: "department_finance_reviews",
                column: "ApprovalApproverUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_procurement_input_price_offers_users_ApprovalApproverUserId",
                table: "procurement_input_price_offers",
                column: "ApprovalApproverUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_project_boq_revisions_users_ApprovalApproverUserId",
                table: "project_boq_revisions",
                column: "ApprovalApproverUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_department_finance_reviews_users_ApprovalApproverUserId",
                table: "department_finance_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_procurement_input_price_offers_users_ApprovalApproverUserId",
                table: "procurement_input_price_offers");

            migrationBuilder.DropForeignKey(
                name: "FK_project_boq_revisions_users_ApprovalApproverUserId",
                table: "project_boq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_project_boq_revisions_ApprovalApproverUserId",
                table: "project_boq_revisions");

            migrationBuilder.DropIndex(
                name: "IX_procurement_input_price_offers_ApprovalApproverUserId",
                table: "procurement_input_price_offers");

            migrationBuilder.DropIndex(
                name: "IX_department_finance_reviews_ApprovalApproverUserId",
                table: "department_finance_reviews");

            migrationBuilder.DropColumn(
                name: "ApprovalApproverUserId",
                table: "project_boq_revisions");

            migrationBuilder.DropColumn(
                name: "ApprovalApproverUserId",
                table: "procurement_input_price_offers");

            migrationBuilder.DropColumn(
                name: "ApprovalApproverUserId",
                table: "department_finance_reviews");
        }
    }
}
