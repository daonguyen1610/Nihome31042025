using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceControlWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClosingAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingByUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_periods_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_periods_users_ClosingByUserId",
                        column: x => x.ClosingByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "payment_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    ContractPaymentMilestoneId = table.Column<int>(type: "int", nullable: true),
                    SupplierInvoiceNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidatedByUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedAccountantUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_requests_contract_payment_milestones_ContractPaymentMilestoneId",
                        column: x => x.ContractPaymentMilestoneId,
                        principalTable: "contract_payment_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_requests_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_requests_procurement_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "procurement_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_requests_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_AssignedAccountantUserId",
                        column: x => x.AssignedAccountantUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payment_requests_users_ValidatedByUserId",
                        column: x => x.ValidatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "accounting_corrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    AccountingPeriodId = table.Column<int>(type: "int", nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEntityId = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OriginalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CorrectedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResponsibleAccountantUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReversalOfCorrectionId = table.Column<int>(type: "int", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_corrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_corrections_accounting_corrections_ReversalOfCorrectionId",
                        column: x => x.ReversalOfCorrectionId,
                        principalTable: "accounting_corrections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_accounting_periods_AccountingPeriodId",
                        column: x => x.AccountingPeriodId,
                        principalTable: "accounting_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_corrections_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_ResponsibleAccountantUserId",
                        column: x => x.ResponsibleAccountantUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_ReversedByUserId",
                        column: x => x.ReversedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_accounting_corrections_users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "payment_request_attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRequestId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_request_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_request_attachments_payment_requests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "payment_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_request_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRequestId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_request_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_request_events_payment_requests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "payment_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_request_events_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_AccountingPeriodId",
                table: "accounting_corrections",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_ApprovedByUserId",
                table: "accounting_corrections",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_Code",
                table: "accounting_corrections",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_OperationalProjectId",
                table: "accounting_corrections",
                column: "OperationalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_RecordedByUserId",
                table: "accounting_corrections",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_RejectedByUserId",
                table: "accounting_corrections",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_ResponsibleAccountantUserId_ApprovedAt_Status",
                table: "accounting_corrections",
                columns: new[] { "ResponsibleAccountantUserId", "ApprovedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_ReversalOfCorrectionId",
                table: "accounting_corrections",
                column: "ReversalOfCorrectionId",
                unique: true,
                filter: "[ReversalOfCorrectionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_ReversedByUserId",
                table: "accounting_corrections",
                column: "ReversedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_SourceEntityType_SourceEntityId",
                table: "accounting_corrections",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_corrections_SubmittedByUserId",
                table: "accounting_corrections",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_ClosedByUserId",
                table: "accounting_periods",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_ClosingByUserId",
                table: "accounting_periods",
                column: "ClosingByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_Year_Month",
                table: "accounting_periods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_request_attachments_PaymentRequestId_FilePath",
                table: "payment_request_attachments",
                columns: new[] { "PaymentRequestId", "FilePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_request_events_ChangedByUserId",
                table: "payment_request_events",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_request_events_PaymentRequestId_ChangedAt",
                table: "payment_request_events",
                columns: new[] { "PaymentRequestId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_ApprovedByUserId",
                table: "payment_requests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_AssignedAccountantUserId_PaidAt_Status",
                table: "payment_requests",
                columns: new[] { "AssignedAccountantUserId", "PaidAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_CancelledByUserId",
                table: "payment_requests",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_Code",
                table: "payment_requests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_ContractId",
                table: "payment_requests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_ContractPaymentMilestoneId",
                table: "payment_requests",
                column: "ContractPaymentMilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_CreatedByUserId",
                table: "payment_requests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_PaidByUserId",
                table: "payment_requests",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_RejectedByUserId",
                table: "payment_requests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_SubmittedByUserId",
                table: "payment_requests",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_ValidatedByUserId",
                table: "payment_requests",
                column: "ValidatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_VendorId_SupplierInvoiceNumber",
                table: "payment_requests",
                columns: new[] { "VendorId", "SupplierInvoiceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_corrections");

            migrationBuilder.DropTable(
                name: "payment_request_attachments");

            migrationBuilder.DropTable(
                name: "payment_request_events");

            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "payment_requests");
        }
    }
}
