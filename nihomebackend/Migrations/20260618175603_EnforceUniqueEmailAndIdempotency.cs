using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueEmailAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Backfill missing emails so the NOT NULL constraint below
            //    cannot fail on existing rows.
            migrationBuilder.Sql(@"
                UPDATE users
                SET Email = CONCAT('noemail.user', Id, '@nihome.local')
                WHERE Email IS NULL OR LTRIM(RTRIM(Email)) = '';
            ");

            // 2) Normalize all emails to lower-case + trimmed so the unique
            //    index treats 'A@x.com' and 'a@x.com' as the same identity.
            migrationBuilder.Sql(@"
                UPDATE users
                SET Email = LOWER(LTRIM(RTRIM(Email)));
            ");

            // 3) Resolve any existing duplicates: keep the oldest row (lowest
            //    Id) and rename the rest as 'local+dupN@domain' so the unique
            //    index can be created without conflict. Original collision is
            //    audited so admins can reach out and reconcile manually.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT
                        Id,
                        Email,
                        ROW_NUMBER() OVER (PARTITION BY Email ORDER BY Id) AS rn
                    FROM users
                )
                UPDATE u
                SET Email = CASE
                    WHEN CHARINDEX('@', r.Email) > 0 THEN
                        SUBSTRING(r.Email, 1, CHARINDEX('@', r.Email) - 1)
                        + '+dup' + CAST(r.rn - 1 AS NVARCHAR(10))
                        + SUBSTRING(r.Email, CHARINDEX('@', r.Email), 150)
                    ELSE r.Email + '+dup' + CAST(r.rn - 1 AS NVARCHAR(10))
                END
                FROM users u
                JOIN ranked r ON r.Id = u.Id
                WHERE r.rn > 1;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO audit_logs
                    (AuditId, CreatedAt, Action, ResourceType, ResourceId, Message,
                     ActorType, SourceSystem, Channel, Status)
                SELECT
                    LEFT(CONVERT(NVARCHAR(36), NEWID()), 36),
                    SYSUTCDATETIME(),
                    'user.email.dedup',
                    'User',
                    CAST(Id AS NVARCHAR(100)),
                    'Email auto-renamed during unique-index migration: ' + Email,
                    'System',
                    'migration',
                    'internal',
                    'Success'
                FROM users
                WHERE Email LIKE '%+dup%';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email_Unique",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ExpiresAt",
                table: "idempotency_records",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_Scope_Key",
                table: "idempotency_records",
                columns: new[] { "Scope", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropIndex(
                name: "IX_users_Email_Unique",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);
        }
    }
}
