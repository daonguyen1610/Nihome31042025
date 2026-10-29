using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class ScopeDesignFlowTeamsAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operational_project_members_OperationalProjectId_UserId",
                table: "operational_project_members");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_DesignProjectId",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Code",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_phases_OperationalProjectId_Code",
                table: "design_schedule_phases");

            migrationBuilder.AddColumn<int>(
                name: "DesignProjectId",
                table: "operational_project_members",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_DesignProjectId_UserId",
                table: "operational_project_members",
                columns: new[] { "DesignProjectId", "UserId" },
                unique: true,
                filter: "[EndedAt] IS NULL AND [DesignProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_OperationalProjectId_UserId",
                table: "operational_project_members",
                columns: new[] { "OperationalProjectId", "UserId" },
                unique: true,
                filter: "[EndedAt] IS NULL AND [DesignProjectId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_DesignProjectId_Code",
                table: "design_schedule_tasks",
                columns: new[] { "DesignProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Code",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_phases_OperationalProjectId_Code",
                table: "design_schedule_phases",
                columns: new[] { "OperationalProjectId", "Code" });

            migrationBuilder.AddForeignKey(
                name: "FK_operational_project_members_design_projects_DesignProjectId",
                table: "operational_project_members",
                column: "DesignProjectId",
                principalTable: "design_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The previous contract allowed one Design per operational project.
            // Keep the original membership/history and split Design participation
            // into that flow. Never guess a destination after multiple flows exist.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM design_projects
                    WHERE OperationalProjectId IS NOT NULL
                    GROUP BY OperationalProjectId HAVING COUNT(*) > 1)
                    THROW 51000, 'Scope Design teams before creating additional Design flows. Reconcile ambiguous operational projects first.', 1;

                DECLARE @members TABLE (OldId int PRIMARY KEY, NewId int, DesignProjectId int);
                INSERT INTO operational_project_members
                    (OperationalProjectId, DesignProjectId, UserId, Position,
                     ReportsToMemberId, StartedAt, EndedAt, Source, SourceReference,
                     CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
                SELECT m.OperationalProjectId, d.Id, m.UserId, m.Position,
                    NULL, m.StartedAt, m.EndedAt, 'LegacyBackfill',
                    CONCAT('DesignFlowSplit:', m.Id), m.CreatedAt, m.CreatedByUserId,
                    SYSUTCDATETIME(), m.UpdatedByUserId
                FROM operational_project_members m
                JOIN design_projects d ON d.OperationalProjectId = m.OperationalProjectId
                WHERE m.DesignProjectId IS NULL AND (
                    EXISTS (SELECT 1 FROM operational_project_member_roles r WHERE r.MemberId = m.Id
                        AND ((r.Scope = 'Module' AND r.ScopeValue = 'Design') OR r.Scope = 'Discipline'))
                    OR EXISTS (SELECT 1 FROM design_schedule_tasks t WHERE t.DesignProjectId = d.Id
                        AND (t.AssigneeMemberId = m.Id OR t.ApproverMemberId = m.Id))
                    OR EXISTS (SELECT 1 FROM design_schedule_task_cc_members c
                        JOIN design_schedule_tasks t ON t.Id = c.TaskId
                        WHERE t.DesignProjectId = d.Id AND c.MemberId = m.Id)
                    OR EXISTS (SELECT 1 FROM operational_project_assignments a
                        WHERE a.OperationalProjectId = m.OperationalProjectId AND a.Module = 'Design'
                        AND (a.AssigneeMemberId = m.Id OR a.ManagerMemberId = m.Id)));

                INSERT INTO @members (OldId, NewId, DesignProjectId)
                SELECT CAST(SUBSTRING(SourceReference, 17, 20) AS int), Id, DesignProjectId
                FROM operational_project_members WHERE Source = 'LegacyBackfill'
                    AND SourceReference LIKE 'DesignFlowSplit:%';

                INSERT INTO operational_project_member_roles
                    (MemberId, RoleCode, Scope, ScopeValue, Source, SourceReference, StartedAt, EndedAt)
                SELECT x.NewId, r.RoleCode, r.Scope, r.ScopeValue, r.Source,
                    r.SourceReference, r.StartedAt, r.EndedAt
                FROM operational_project_member_roles r JOIN @members x ON x.OldId = r.MemberId
                WHERE r.Scope = 'Project' OR r.Scope = 'Discipline'
                    OR (r.Scope = 'Module' AND r.ScopeValue = 'Design');

                UPDATE n SET ReportsToMemberId = parent.NewId
                FROM operational_project_members n JOIN @members x ON x.NewId = n.Id
                JOIN operational_project_members old ON old.Id = x.OldId
                JOIN @members parent ON parent.OldId = old.ReportsToMemberId
                    AND parent.DesignProjectId = x.DesignProjectId;

                UPDATE t SET AssigneeMemberId = x.NewId
                FROM design_schedule_tasks t JOIN @members x ON x.OldId = t.AssigneeMemberId
                    AND x.DesignProjectId = t.DesignProjectId;
                UPDATE t SET ApproverMemberId = x.NewId
                FROM design_schedule_tasks t JOIN @members x ON x.OldId = t.ApproverMemberId
                    AND x.DesignProjectId = t.DesignProjectId;
                UPDATE c SET MemberId = x.NewId
                FROM design_schedule_task_cc_members c JOIN design_schedule_tasks t ON t.Id = c.TaskId
                JOIN @members x ON x.OldId = c.MemberId AND x.DesignProjectId = t.DesignProjectId;
                UPDATE a SET AssigneeMemberId = x.NewId
                FROM operational_project_assignments a JOIN @members x ON x.OldId = a.AssigneeMemberId
                WHERE a.Module = 'Design';
                UPDATE a SET ManagerMemberId = x.NewId
                FROM operational_project_assignments a JOIN @members x ON x.OldId = a.ManagerMemberId
                WHERE a.Module = 'Design';

                UPDATE r SET EndedAt = SYSUTCDATETIME()
                FROM operational_project_member_roles r JOIN @members x ON x.OldId = r.MemberId
                WHERE r.EndedAt IS NULL AND (r.Scope = 'Discipline'
                    OR (r.Scope = 'Module' AND r.ScopeValue = 'Design'));
                UPDATE m SET EndedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
                FROM operational_project_members m JOIN @members x ON x.OldId = m.Id
                WHERE m.EndedAt IS NULL
                    AND NOT EXISTS (SELECT 1 FROM operational_project_member_roles r
                        WHERE r.MemberId = m.Id AND r.EndedAt IS NULL)
                    AND NOT EXISTS (SELECT 1 FROM operational_project_assignments a
                        WHERE a.AssigneeMemberId = m.Id OR a.ManagerMemberId = m.Id);

                INSERT INTO operational_project_team_history
                    (OperationalProjectId, EntityType, EntityId, Action, SnapshotJson, ChangedAt, ChangedByUserId)
                SELECT m.OperationalProjectId, 'Member', m.Id, 'Migrated',
                    CONCAT('{"LegacyMemberId":', x.OldId, ',"DesignProjectId":', x.DesignProjectId, '}'),
                    SYSUTCDATETIME(), m.UpdatedByUserId
                FROM @members x JOIN operational_project_members m ON m.Id = x.NewId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merging teams would discard scope and may change authorization.
            // Restore the rehearsed backup or reconcile explicitly before rollback.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM operational_project_members WHERE DesignProjectId IS NOT NULL)
                    THROW 51000, 'Cannot merge scoped Design memberships automatically. Restore the pre-migration backup or reconcile teams and schedule references first.', 1;
                IF EXISTS (SELECT 1 FROM design_schedule_tasks GROUP BY OperationalProjectId, Code HAVING COUNT(*) > 1)
                    OR EXISTS (SELECT 1 FROM design_schedule_phases GROUP BY OperationalProjectId, Code HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot restore shared schedule uniqueness while duplicate codes exist across Design flows.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_operational_project_members_design_projects_DesignProjectId",
                table: "operational_project_members");

            migrationBuilder.DropIndex(
                name: "IX_operational_project_members_DesignProjectId_UserId",
                table: "operational_project_members");

            migrationBuilder.DropIndex(
                name: "IX_operational_project_members_OperationalProjectId_UserId",
                table: "operational_project_members");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_DesignProjectId_Code",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Code",
                table: "design_schedule_tasks");

            migrationBuilder.DropIndex(
                name: "IX_design_schedule_phases_OperationalProjectId_Code",
                table: "design_schedule_phases");

            migrationBuilder.DropColumn(
                name: "DesignProjectId",
                table: "operational_project_members");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_OperationalProjectId_UserId",
                table: "operational_project_members",
                columns: new[] { "OperationalProjectId", "UserId" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_DesignProjectId",
                table: "design_schedule_tasks",
                column: "DesignProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_tasks_OperationalProjectId_Code",
                table: "design_schedule_tasks",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_design_schedule_phases_OperationalProjectId_Code",
                table: "design_schedule_phases",
                columns: new[] { "OperationalProjectId", "Code" },
                unique: true);
        }
    }
}
