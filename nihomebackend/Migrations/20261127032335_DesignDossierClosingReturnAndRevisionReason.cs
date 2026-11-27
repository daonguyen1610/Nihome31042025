using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class DesignDossierClosingReturnAndRevisionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalReturnReason",
                table: "design_reviews",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalReturnedAt",
                table: "design_reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InternalReturnedByUserId",
                table: "design_reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "design_projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByUserId",
                table: "design_projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "design_projects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "concept_revisions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalReturnReason",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "InternalReturnedAt",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "InternalReturnedByUserId",
                table: "design_reviews");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "design_projects");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "design_projects");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "design_projects");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "concept_revisions");
        }
    }
}
