using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class HardenConstructionReadinessAndQsLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentKind",
                table: "construction_qs_batches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Original");

            migrationBuilder.AddColumn<bool>(
                name: "IsActiveLineage",
                table: "construction_qs_batches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecordState",
                table: "construction_qs_batches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<long>(
                name: "BchOrganizationDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BudgetBaselineDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HsePlanDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MethodStatementDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResourcePlanDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SCurveBaselineDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ScheduleBaselineDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SiteHandoverDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WbsBaselineDocumentId",
                table: "construction_preparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [construction_qs_batches]
                SET [AdjustmentKind] = CASE
                    WHEN [SupersedesBatchId] IS NOT NULL THEN 'SupersedingCorrection'
                    WHEN [ReversesBatchId] IS NOT NULL THEN 'Reversal'
                    ELSE 'Original'
                END,
                [IsActiveLineage] = CASE
                    WHEN [Status] = 'CmApproved'
                         AND ([SupersedesBatchId] IS NOT NULL OR [ReversesBatchId] IS NOT NULL)
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END;

                IF EXISTS (
                    SELECT [PredecessorId]
                    FROM (
                        SELECT COALESCE([SupersedesBatchId], [ReversesBatchId]) AS [PredecessorId]
                        FROM [construction_qs_batches]
                        WHERE [Status] = 'CmApproved'
                          AND ([SupersedesBatchId] IS NOT NULL OR [ReversesBatchId] IS NOT NULL)
                    ) AS [ApprovedLineage]
                    GROUP BY [PredecessorId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Cannot migrate QS lineage because an approved batch has multiple approved successors.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [construction_qs_batches] AS [successor]
                    LEFT JOIN [construction_qs_batches] AS [predecessor]
                        ON [predecessor].[Id] = COALESCE([successor].[SupersedesBatchId], [successor].[ReversesBatchId])
                    WHERE ([successor].[SupersedesBatchId] IS NOT NULL OR [successor].[ReversesBatchId] IS NOT NULL)
                      AND [predecessor].[Id] IS NULL
                )
                    THROW 51001, 'Cannot migrate QS lineage because a correction references a missing predecessor.', 1;

                UPDATE [predecessor]
                SET [RecordState] = CASE
                    WHEN [successor].[ReversesBatchId] IS NOT NULL THEN 'Reversed'
                    ELSE 'Superseded'
                END
                FROM [construction_qs_batches] AS [predecessor]
                INNER JOIN [construction_qs_batches] AS [successor]
                    ON [predecessor].[Id] = COALESCE([successor].[SupersedesBatchId], [successor].[ReversesBatchId])
                WHERE [successor].[Status] = 'CmApproved';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ReversesBatchId",
                table: "construction_qs_batches",
                column: "ReversesBatchId",
                unique: true,
                filter: "[ReversesBatchId] IS NOT NULL AND [IsActiveLineage] = CAST(1 AS bit)");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_SupersedesBatchId",
                table: "construction_qs_batches",
                column: "SupersedesBatchId",
                unique: true,
                filter: "[SupersedesBatchId] IS NOT NULL AND [IsActiveLineage] = CAST(1 AS bit)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_construction_qs_batches_adjustment_link",
                table: "construction_qs_batches",
                sql: "([AdjustmentKind] = 'Original' AND [SupersedesBatchId] IS NULL AND [ReversesBatchId] IS NULL) OR ([AdjustmentKind] = 'SupersedingCorrection' AND [SupersedesBatchId] IS NOT NULL AND [ReversesBatchId] IS NULL) OR ([AdjustmentKind] = 'Reversal' AND [SupersedesBatchId] IS NULL AND [ReversesBatchId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_BchOrganizationDocumentId",
                table: "construction_preparations",
                column: "BchOrganizationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_BudgetBaselineDocumentId",
                table: "construction_preparations",
                column: "BudgetBaselineDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_HsePlanDocumentId",
                table: "construction_preparations",
                column: "HsePlanDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_MethodStatementDocumentId",
                table: "construction_preparations",
                column: "MethodStatementDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_ResourcePlanDocumentId",
                table: "construction_preparations",
                column: "ResourcePlanDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_ScheduleBaselineDocumentId",
                table: "construction_preparations",
                column: "ScheduleBaselineDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_SCurveBaselineDocumentId",
                table: "construction_preparations",
                column: "SCurveBaselineDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_SiteHandoverDocumentId",
                table: "construction_preparations",
                column: "SiteHandoverDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_preparations_WbsBaselineDocumentId",
                table: "construction_preparations",
                column: "WbsBaselineDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_BchOrganizationDocumentId",
                table: "construction_preparations",
                column: "BchOrganizationDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_BudgetBaselineDocumentId",
                table: "construction_preparations",
                column: "BudgetBaselineDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_HsePlanDocumentId",
                table: "construction_preparations",
                column: "HsePlanDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_MethodStatementDocumentId",
                table: "construction_preparations",
                column: "MethodStatementDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_ResourcePlanDocumentId",
                table: "construction_preparations",
                column: "ResourcePlanDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_SCurveBaselineDocumentId",
                table: "construction_preparations",
                column: "SCurveBaselineDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_ScheduleBaselineDocumentId",
                table: "construction_preparations",
                column: "ScheduleBaselineDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_SiteHandoverDocumentId",
                table: "construction_preparations",
                column: "SiteHandoverDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_preparations_project_documents_WbsBaselineDocumentId",
                table: "construction_preparations",
                column: "WbsBaselineDocumentId",
                principalTable: "project_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_qs_batches_construction_qs_batches_ReversesBatchId",
                table: "construction_qs_batches",
                column: "ReversesBatchId",
                principalTable: "construction_qs_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_qs_batches_construction_qs_batches_SupersedesBatchId",
                table: "construction_qs_batches",
                column: "SupersedesBatchId",
                principalTable: "construction_qs_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_BchOrganizationDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_BudgetBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_HsePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_MethodStatementDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_ResourcePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_SCurveBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_ScheduleBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_SiteHandoverDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_preparations_project_documents_WbsBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_qs_batches_construction_qs_batches_ReversesBatchId",
                table: "construction_qs_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_qs_batches_construction_qs_batches_SupersedesBatchId",
                table: "construction_qs_batches");

            migrationBuilder.DropIndex(
                name: "IX_construction_qs_batches_ReversesBatchId",
                table: "construction_qs_batches");

            migrationBuilder.DropIndex(
                name: "IX_construction_qs_batches_SupersedesBatchId",
                table: "construction_qs_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_construction_qs_batches_adjustment_link",
                table: "construction_qs_batches");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_BchOrganizationDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_BudgetBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_HsePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_MethodStatementDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_ResourcePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_ScheduleBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_SCurveBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_SiteHandoverDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropIndex(
                name: "IX_construction_preparations_WbsBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "AdjustmentKind",
                table: "construction_qs_batches");

            migrationBuilder.DropColumn(
                name: "IsActiveLineage",
                table: "construction_qs_batches");

            migrationBuilder.DropColumn(
                name: "RecordState",
                table: "construction_qs_batches");

            migrationBuilder.DropColumn(
                name: "BchOrganizationDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "BudgetBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "HsePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "MethodStatementDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "ResourcePlanDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "SCurveBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "ScheduleBaselineDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "SiteHandoverDocumentId",
                table: "construction_preparations");

            migrationBuilder.DropColumn(
                name: "WbsBaselineDocumentId",
                table: "construction_preparations");
        }
    }
}
