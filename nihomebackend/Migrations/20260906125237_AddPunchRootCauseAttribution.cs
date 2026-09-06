using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchRootCauseAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResponsibleDesignUserId",
                table: "punch_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "punch_items",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unclassified");

            migrationBuilder.AddColumn<DateTime>(
                name: "RootCauseConfirmedAt",
                table: "punch_items",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RootCauseConfirmedByUserId",
                table: "punch_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootCauseNote",
                table: "punch_items",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_ResponsibleDesignUserId",
                table: "punch_items",
                column: "ResponsibleDesignUserId");

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_RootCause_ResponsibleDesignUserId_VerifiedAt",
                table: "punch_items",
                columns: new[] { "RootCause", "ResponsibleDesignUserId", "VerifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_punch_items_RootCauseConfirmedByUserId",
                table: "punch_items",
                column: "RootCauseConfirmedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_punch_items_users_ResponsibleDesignUserId",
                table: "punch_items",
                column: "ResponsibleDesignUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_punch_items_users_RootCauseConfirmedByUserId",
                table: "punch_items",
                column: "RootCauseConfirmedByUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_punch_items_users_ResponsibleDesignUserId",
                table: "punch_items");

            migrationBuilder.DropForeignKey(
                name: "FK_punch_items_users_RootCauseConfirmedByUserId",
                table: "punch_items");

            migrationBuilder.DropIndex(
                name: "IX_punch_items_ResponsibleDesignUserId",
                table: "punch_items");

            migrationBuilder.DropIndex(
                name: "IX_punch_items_RootCause_ResponsibleDesignUserId_VerifiedAt",
                table: "punch_items");

            migrationBuilder.DropIndex(
                name: "IX_punch_items_RootCauseConfirmedByUserId",
                table: "punch_items");

            migrationBuilder.DropColumn(
                name: "ResponsibleDesignUserId",
                table: "punch_items");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "punch_items");

            migrationBuilder.DropColumn(
                name: "RootCauseConfirmedAt",
                table: "punch_items");

            migrationBuilder.DropColumn(
                name: "RootCauseConfirmedByUserId",
                table: "punch_items");

            migrationBuilder.DropColumn(
                name: "RootCauseNote",
                table: "punch_items");
        }
    }
}
