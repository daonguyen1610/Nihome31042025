using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace NihomeBackend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105120000_AddProjectMasterSchedule")]
public sealed class AddProjectMasterSchedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_schedule_nodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_schedule_nodes", x => x.Id);
                table.ForeignKey("FK_project_schedule_nodes_operational_projects_OperationalProjectId",
                    x => x.OperationalProjectId, "operational_projects", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "project_schedule_revisions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ChangeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ApproverUserId = table.Column<int>(type: "int", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_schedule_revisions", x => x.Id);
                table.ForeignKey("FK_project_schedule_revisions_operational_projects_OperationalProjectId",
                    x => x.OperationalProjectId, "operational_projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_project_schedule_revisions_users_ApproverUserId",
                    x => x.ApproverUserId, "users", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_project_schedule_revisions_users_CreatedByUserId",
                    x => x.CreatedByUserId, "users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_project_schedule_revisions_users_ReviewedByUserId",
                    x => x.ReviewedByUserId, "users", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_project_schedule_revisions_users_SubmittedByUserId",
                    x => x.SubmittedByUserId, "users", "Id", onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateTable(
            name: "project_schedule_items",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                RevisionId = table.Column<int>(type: "int", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ParentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WbsCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                PlannedStart = table.Column<DateOnly>(type: "date", nullable: false),
                PlannedEnd = table.Column<DateOnly>(type: "date", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_schedule_items", x => x.Id);
                table.CheckConstraint("CK_project_schedule_items_planned_dates", "[PlannedEnd] >= [PlannedStart]");
                table.ForeignKey("FK_project_schedule_items_project_schedule_nodes_NodeId",
                    x => x.NodeId, "project_schedule_nodes", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_project_schedule_items_project_schedule_nodes_ParentNodeId",
                    x => x.ParentNodeId, "project_schedule_nodes", "Id", onDelete: ReferentialAction.NoAction);
                table.ForeignKey("FK_project_schedule_items_project_schedule_revisions_RevisionId",
                    x => x.RevisionId, "project_schedule_revisions", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<Guid>(name: "ProjectScheduleNodeId", table: "department_work_tasks",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ProjectScheduleNodeId", table: "design_schedule_tasks",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ProjectScheduleNodeId", table: "construction_tasks",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProjectScheduleEmailSubjectTemplate", table: "site_settings",
            type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProjectScheduleEmailBodyTemplate", table: "site_settings",
            type: "nvarchar(max)", nullable: true);

        migrationBuilder.CreateIndex("IX_project_schedule_nodes_OperationalProjectId_Id", "project_schedule_nodes",
            new[] { "OperationalProjectId", "Id" }, unique: true);
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_OperationalProjectId_Version", "project_schedule_revisions",
            new[] { "OperationalProjectId", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_OperationalProjectId_Status", "project_schedule_revisions",
            new[] { "OperationalProjectId", "Status" });
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_ApproverUserId", "project_schedule_revisions", "ApproverUserId");
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_CreatedByUserId", "project_schedule_revisions", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_ReviewedByUserId", "project_schedule_revisions", "ReviewedByUserId");
        migrationBuilder.CreateIndex("IX_project_schedule_revisions_SubmittedByUserId", "project_schedule_revisions", "SubmittedByUserId");
        migrationBuilder.CreateIndex("IX_project_schedule_items_RevisionId_NodeId", "project_schedule_items",
            new[] { "RevisionId", "NodeId" }, unique: true);
        migrationBuilder.CreateIndex("IX_project_schedule_items_RevisionId_WbsCode", "project_schedule_items",
            new[] { "RevisionId", "WbsCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_project_schedule_items_NodeId", "project_schedule_items", "NodeId");
        migrationBuilder.CreateIndex("IX_project_schedule_items_ParentNodeId", "project_schedule_items", "ParentNodeId");
        migrationBuilder.CreateIndex("IX_department_work_tasks_ProjectScheduleNodeId", "department_work_tasks", "ProjectScheduleNodeId");
        migrationBuilder.CreateIndex("IX_design_schedule_tasks_ProjectScheduleNodeId", "design_schedule_tasks", "ProjectScheduleNodeId");
        migrationBuilder.CreateIndex("IX_construction_tasks_ProjectScheduleNodeId", "construction_tasks", "ProjectScheduleNodeId");
        migrationBuilder.AddForeignKey("FK_department_work_tasks_project_schedule_nodes_ProjectScheduleNodeId",
            "department_work_tasks", "ProjectScheduleNodeId", "project_schedule_nodes", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_design_schedule_tasks_project_schedule_nodes_ProjectScheduleNodeId",
            "design_schedule_tasks", "ProjectScheduleNodeId", "project_schedule_nodes", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_construction_tasks_project_schedule_nodes_ProjectScheduleNodeId",
            "construction_tasks", "ProjectScheduleNodeId", "project_schedule_nodes", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_department_work_tasks_project_schedule_nodes_ProjectScheduleNodeId", "department_work_tasks");
        migrationBuilder.DropForeignKey("FK_design_schedule_tasks_project_schedule_nodes_ProjectScheduleNodeId", "design_schedule_tasks");
        migrationBuilder.DropForeignKey("FK_construction_tasks_project_schedule_nodes_ProjectScheduleNodeId", "construction_tasks");
        migrationBuilder.DropIndex("IX_department_work_tasks_ProjectScheduleNodeId", "department_work_tasks");
        migrationBuilder.DropIndex("IX_design_schedule_tasks_ProjectScheduleNodeId", "design_schedule_tasks");
        migrationBuilder.DropIndex("IX_construction_tasks_ProjectScheduleNodeId", "construction_tasks");
        migrationBuilder.DropColumn("ProjectScheduleNodeId", "department_work_tasks");
        migrationBuilder.DropColumn("ProjectScheduleNodeId", "design_schedule_tasks");
        migrationBuilder.DropColumn("ProjectScheduleNodeId", "construction_tasks");
        migrationBuilder.DropColumn("ProjectScheduleEmailSubjectTemplate", "site_settings");
        migrationBuilder.DropColumn("ProjectScheduleEmailBodyTemplate", "site_settings");
        migrationBuilder.DropTable("project_schedule_items");
        migrationBuilder.DropTable("project_schedule_revisions");
        migrationBuilder.DropTable("project_schedule_nodes");
    }
}
