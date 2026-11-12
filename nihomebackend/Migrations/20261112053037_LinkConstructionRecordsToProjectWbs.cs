using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class LinkConstructionRecordsToProjectWbs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectScheduleNodeId",
                table: "qa_qc_dossiers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectScheduleNodeId",
                table: "construction_resource_requests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectScheduleNodeId",
                table: "construction_qs_batches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_qc_dossiers_ProjectScheduleNodeId",
                table: "qa_qc_dossiers",
                column: "ProjectScheduleNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_resource_requests_ProjectScheduleNodeId",
                table: "construction_resource_requests",
                column: "ProjectScheduleNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_construction_qs_batches_ProjectScheduleNodeId",
                table: "construction_qs_batches",
                column: "ProjectScheduleNodeId");

            // Backfill only when the code identifies the same stable node in every
            // published revision. Ambiguous or changed legacy codes remain unlinked.
            migrationBuilder.Sql("""
                ;WITH Matches AS (
                    SELECT record.Id, COUNT(DISTINCT item.NodeId) AS NodeCount
                    FROM construction_resource_requests record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                    GROUP BY record.Id
                ), UniqueMatches AS (
                    SELECT DISTINCT record.Id, item.NodeId
                    FROM construction_resource_requests record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    INNER JOIN Matches match ON match.Id = record.Id AND match.NodeCount = 1
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                )
                UPDATE record SET ProjectScheduleNodeId = matched.NodeId
                FROM construction_resource_requests record
                INNER JOIN UniqueMatches matched ON matched.Id = record.Id;

                ;WITH Matches AS (
                    SELECT record.Id, COUNT(DISTINCT item.NodeId) AS NodeCount
                    FROM qa_qc_dossiers record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                    GROUP BY record.Id
                ), UniqueMatches AS (
                    SELECT DISTINCT record.Id, item.NodeId
                    FROM qa_qc_dossiers record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    INNER JOIN Matches match ON match.Id = record.Id AND match.NodeCount = 1
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                )
                UPDATE record SET ProjectScheduleNodeId = matched.NodeId
                FROM qa_qc_dossiers record
                INNER JOIN UniqueMatches matched ON matched.Id = record.Id;

                ;WITH Matches AS (
                    SELECT record.Id, COUNT(DISTINCT item.NodeId) AS NodeCount
                    FROM construction_qs_batches record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                    GROUP BY record.Id
                ), UniqueMatches AS (
                    SELECT DISTINCT record.Id, item.NodeId
                    FROM construction_qs_batches record
                    INNER JOIN project_schedule_revisions revision
                        ON revision.OperationalProjectId = record.OperationalProjectId
                       AND revision.Status = N'Published'
                    INNER JOIN project_schedule_items item ON item.RevisionId = revision.Id
                    INNER JOIN Matches match ON match.Id = record.Id AND match.NodeCount = 1
                    WHERE UPPER(LTRIM(RTRIM(item.WbsCode))) = UPPER(LTRIM(RTRIM(record.WbsCode)))
                )
                UPDATE record SET ProjectScheduleNodeId = matched.NodeId
                FROM construction_qs_batches record
                INNER JOIN UniqueMatches matched ON matched.Id = record.Id;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_qs_batches_project_schedule_nodes_ProjectScheduleNodeId",
                table: "construction_qs_batches",
                column: "ProjectScheduleNodeId",
                principalTable: "project_schedule_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_construction_resource_requests_project_schedule_nodes_ProjectScheduleNodeId",
                table: "construction_resource_requests",
                column: "ProjectScheduleNodeId",
                principalTable: "project_schedule_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_qa_qc_dossiers_project_schedule_nodes_ProjectScheduleNodeId",
                table: "qa_qc_dossiers",
                column: "ProjectScheduleNodeId",
                principalTable: "project_schedule_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_construction_qs_batches_project_schedule_nodes_ProjectScheduleNodeId",
                table: "construction_qs_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_construction_resource_requests_project_schedule_nodes_ProjectScheduleNodeId",
                table: "construction_resource_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_qa_qc_dossiers_project_schedule_nodes_ProjectScheduleNodeId",
                table: "qa_qc_dossiers");

            migrationBuilder.DropIndex(
                name: "IX_qa_qc_dossiers_ProjectScheduleNodeId",
                table: "qa_qc_dossiers");

            migrationBuilder.DropIndex(
                name: "IX_construction_resource_requests_ProjectScheduleNodeId",
                table: "construction_resource_requests");

            migrationBuilder.DropIndex(
                name: "IX_construction_qs_batches_ProjectScheduleNodeId",
                table: "construction_qs_batches");

            migrationBuilder.DropColumn(
                name: "ProjectScheduleNodeId",
                table: "qa_qc_dossiers");

            migrationBuilder.DropColumn(
                name: "ProjectScheduleNodeId",
                table: "construction_resource_requests");

            migrationBuilder.DropColumn(
                name: "ProjectScheduleNodeId",
                table: "construction_qs_batches");
        }
    }
}
