using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalProjectTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operational_project_members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReportsToMemberId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_project_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_project_members_operational_project_members_ReportsToMemberId",
                        column: x => x.ReportsToMemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_operational_project_members_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_project_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_project_team_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_project_team_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_project_team_history_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_project_team_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_project_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationalProjectId = table.Column<int>(type: "int", nullable: false),
                    WorkKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Discipline = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ParallelGroup = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AssigneeMemberId = table.Column<int>(type: "int", nullable: false),
                    ManagerMemberId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_project_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_project_assignments_operational_project_members_AssigneeMemberId",
                        column: x => x.AssigneeMemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operational_project_assignments_operational_project_members_ManagerMemberId",
                        column: x => x.ManagerMemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_operational_project_assignments_operational_projects_OperationalProjectId",
                        column: x => x.OperationalProjectId,
                        principalTable: "operational_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_project_member_roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeValue = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_project_member_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operational_project_member_roles_operational_project_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "operational_project_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_AssigneeMemberId",
                table: "operational_project_assignments",
                column: "AssigneeMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_ManagerMemberId",
                table: "operational_project_assignments",
                column: "ManagerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_OperationalProjectId_WorkKey_AssigneeMemberId",
                table: "operational_project_assignments",
                columns: new[] { "OperationalProjectId", "WorkKey", "AssigneeMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_assignments_ParallelGroup",
                table: "operational_project_assignments",
                column: "ParallelGroup");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_member_roles_MemberId_RoleCode_Scope_ScopeValue",
                table: "operational_project_member_roles",
                columns: new[] { "MemberId", "RoleCode", "Scope", "ScopeValue" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_OperationalProjectId_UserId",
                table: "operational_project_members",
                columns: new[] { "OperationalProjectId", "UserId" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_ReportsToMemberId",
                table: "operational_project_members",
                column: "ReportsToMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_members_UserId",
                table: "operational_project_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_team_history_ChangedByUserId",
                table: "operational_project_team_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_operational_project_team_history_OperationalProjectId_ChangedAt",
                table: "operational_project_team_history",
                columns: new[] { "OperationalProjectId", "ChangedAt" });

            migrationBuilder.Sql("""
                ;WITH LegacyCandidates AS (
                    SELECT p.Id AS OperationalProjectId, p.ProjectManagerUserId AS UserId,
                        CAST('Project Manager' AS nvarchar(150)) AS Position, 1 AS Priority,
                        CAST('OperationalProject.ProjectManagerUserId' AS nvarchar(200)) AS SourceReference
                    FROM operational_projects p
                    WHERE p.ProjectManagerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, d.DesignLeadUserId, 'Design Lead', 2,
                        'DesignProject.DesignLeadUserId'
                    FROM design_projects d
                    WHERE d.OperationalProjectId IS NOT NULL AND d.DesignLeadUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, d.ProjectManagerUserId, 'Design Project Manager', 3,
                        'DesignProject.ProjectManagerUserId'
                    FROM design_projects d
                    WHERE d.OperationalProjectId IS NOT NULL AND d.ProjectManagerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, c.OwnerUserId, 'Design Contributor', 4,
                        'ConceptOption.OwnerUserId'
                    FROM concept_options c
                    INNER JOIN design_projects d ON d.Id = c.DesignProjectId
                    WHERE d.OperationalProjectId IS NOT NULL AND c.OwnerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, b.OwnerUserId, 'Design Contributor', 4,
                        'BasicDesignDoc.OwnerUserId'
                    FROM basic_design_docs b
                    INNER JOIN design_projects d ON d.Id = b.DesignProjectId
                    WHERE d.OperationalProjectId IS NOT NULL AND b.OwnerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, s.OwnerUserId, 'Design Contributor', 4,
                        'ShopDrawing.OwnerUserId'
                    FROM shop_drawings s
                    INNER JOIN design_projects d ON d.Id = s.DesignProjectId
                    WHERE d.OperationalProjectId IS NOT NULL AND s.OwnerUserId IS NOT NULL
                ), RankedCandidates AS (
                    SELECT candidate.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY candidate.OperationalProjectId, candidate.UserId
                            ORDER BY candidate.Priority, candidate.SourceReference) AS CandidateRank
                    FROM LegacyCandidates candidate
                    INNER JOIN users u ON u.Id = candidate.UserId AND u.IsActive = 1
                )
                INSERT INTO operational_project_members
                    (OperationalProjectId, UserId, Position, ReportsToMemberId, StartedAt, EndedAt,
                     Source, SourceReference, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
                SELECT candidate.OperationalProjectId, candidate.UserId, candidate.Position, NULL,
                    SYSUTCDATETIME(), NULL, 'LegacyBackfill', candidate.SourceReference,
                    SYSUTCDATETIME(), candidate.UserId, SYSUTCDATETIME(), candidate.UserId
                FROM RankedCandidates candidate
                WHERE candidate.CandidateRank = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM operational_project_members existing
                      WHERE existing.OperationalProjectId = candidate.OperationalProjectId
                        AND existing.UserId = candidate.UserId AND existing.EndedAt IS NULL);

                ;WITH LegacyRoles AS (
                    SELECT p.Id AS OperationalProjectId, p.ProjectManagerUserId AS UserId,
                        'ProjectManager' AS RoleCode, 'Project' AS Scope, CAST(NULL AS nvarchar(80)) AS ScopeValue,
                        CAST('OperationalProject.ProjectManagerUserId' AS nvarchar(200)) AS SourceReference
                    FROM operational_projects p
                    WHERE p.ProjectManagerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, d.DesignLeadUserId, 'DesignLead', 'Module', 'Design',
                        'DesignProject.DesignLeadUserId'
                    FROM design_projects d
                    WHERE d.OperationalProjectId IS NOT NULL AND d.DesignLeadUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, d.ProjectManagerUserId, 'ProjectManager', 'Module', 'Design',
                        'DesignProject.ProjectManagerUserId'
                    FROM design_projects d
                    WHERE d.OperationalProjectId IS NOT NULL AND d.ProjectManagerUserId IS NOT NULL
                    UNION ALL
                    SELECT d.OperationalProjectId, owners.UserId, 'Observer', 'Module', 'Design',
                        'DesignProject.RecordOwnerUserId'
                    FROM design_projects d
                    CROSS APPLY (
                        SELECT c.OwnerUserId AS UserId FROM concept_options c
                        WHERE c.DesignProjectId = d.Id AND c.OwnerUserId IS NOT NULL
                        UNION
                        SELECT b.OwnerUserId FROM basic_design_docs b
                        WHERE b.DesignProjectId = d.Id AND b.OwnerUserId IS NOT NULL
                        UNION
                        SELECT s.OwnerUserId FROM shop_drawings s
                        WHERE s.DesignProjectId = d.Id AND s.OwnerUserId IS NOT NULL
                    ) owners
                    WHERE d.OperationalProjectId IS NOT NULL
                )
                INSERT INTO operational_project_member_roles
                    (MemberId, RoleCode, Scope, ScopeValue, Source, SourceReference, StartedAt, EndedAt)
                SELECT DISTINCT member.Id, role.RoleCode, role.Scope, role.ScopeValue,
                    'LegacyBackfill', role.SourceReference, SYSUTCDATETIME(), NULL
                FROM LegacyRoles role
                INNER JOIN operational_project_members member
                    ON member.OperationalProjectId = role.OperationalProjectId
                    AND member.UserId = role.UserId AND member.EndedAt IS NULL
                WHERE NOT EXISTS (
                    SELECT 1 FROM operational_project_member_roles existing
                    WHERE existing.MemberId = member.Id AND existing.RoleCode = role.RoleCode
                      AND existing.Scope = role.Scope
                      AND ISNULL(existing.ScopeValue, '') = ISNULL(role.ScopeValue, '')
                      AND existing.EndedAt IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_project_assignments");

            migrationBuilder.DropTable(
                name: "operational_project_member_roles");

            migrationBuilder.DropTable(
                name: "operational_project_team_history");

            migrationBuilder.DropTable(
                name: "operational_project_members");
        }
    }
}
