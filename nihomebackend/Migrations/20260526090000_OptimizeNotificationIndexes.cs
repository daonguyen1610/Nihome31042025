using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526090000_OptimizeNotificationIndexes")]
    public partial class OptimizeNotificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_IsRead",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAt_Id",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_IsRead",
                table: "notifications",
                columns: new[] { "UserId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_CreatedAt_Id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_IsRead",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_IsRead",
                table: "notifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");
        }
    }
}
