using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementIfcReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These new receipt capabilities do not grant Design access. Existing
            // custom role grants remain unchanged; only default Procurement roles
            // receive the new capability on upgrade.
            migrationBuilder.Sql("""
                INSERT INTO permissions (Module, Action, DescriptionKey, IsActive, CreatedAt)
                SELECT N'proc.ifc', a.Action, N'rbac.perm.proc.ifc.' + a.Action, 1, SYSUTCDATETIME()
                FROM (VALUES (N'view'), (N'acknowledge')) a(Action)
                WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.Module=N'proc.ifc' AND p.Action=a.Action);
                INSERT INTO role_permissions (RoleId, PermissionId, CreatedAt)
                SELECT r.Id, p.Id, SYSUTCDATETIME() FROM roles r CROSS JOIN permissions p
                WHERE r.Code IN (N'PROCUREMENT', N'PROCUREMENT_MANAGER')
                  AND p.Module=N'proc.ifc' AND p.Action IN (N'view', N'acknowledge')
                  AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
                """);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ifc_releases",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "AcknowledgedByUserId",
                table: "ifc_release_recipients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                table: "ifc_release_recipients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ifc_release_recipients",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ReleasedFileSha256",
                table: "ifc_release_items",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_recipients_AcknowledgedByUserId",
                table: "ifc_release_recipients",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_recipients_IfcReleaseId_RecipientUserId",
                table: "ifc_release_recipients",
                columns: new[] { "IfcReleaseId", "RecipientUserId" },
                unique: true,
                filter: "[RecipientUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ifc_release_recipients_RecipientUserId",
                table: "ifc_release_recipients",
                column: "RecipientUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ifc_release_recipients_users_AcknowledgedByUserId",
                table: "ifc_release_recipients",
                column: "AcknowledgedByUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ifc_release_recipients_users_RecipientUserId",
                table: "ifc_release_recipients",
                column: "RecipientUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ifc_release_recipients_users_AcknowledgedByUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropForeignKey(
                name: "FK_ifc_release_recipients_users_RecipientUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropIndex(
                name: "IX_ifc_release_recipients_AcknowledgedByUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropIndex(
                name: "IX_ifc_release_recipients_IfcReleaseId_RecipientUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropIndex(
                name: "IX_ifc_release_recipients_RecipientUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ifc_releases");

            migrationBuilder.DropColumn(
                name: "AcknowledgedByUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "ifc_release_recipients");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ifc_release_recipients");

            migrationBuilder.DropColumn(
                name: "ReleasedFileSha256",
                table: "ifc_release_items");
        }
    }
}
