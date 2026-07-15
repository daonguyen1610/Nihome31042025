using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627120000_AddCategoryI18nFields")]
    public partial class AddCategoryI18nFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "activity_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameJa",
                table: "activity_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "activity_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameZh",
                table: "activity_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE activity_categories SET NameVi = Name, NameEn = '', NameZh = '', NameJa = ''");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "news_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameJa",
                table: "news_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "news_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameZh",
                table: "news_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE news_categories SET NameVi = Name, NameEn = '', NameZh = '', NameJa = ''");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "project_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameJa",
                table: "project_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "project_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameZh",
                table: "project_categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE project_categories SET NameVi = Name, NameEn = '', NameZh = '', NameJa = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NameEn", table: "activity_categories");
            migrationBuilder.DropColumn(name: "NameJa", table: "activity_categories");
            migrationBuilder.DropColumn(name: "NameVi", table: "activity_categories");
            migrationBuilder.DropColumn(name: "NameZh", table: "activity_categories");

            migrationBuilder.DropColumn(name: "NameEn", table: "news_categories");
            migrationBuilder.DropColumn(name: "NameJa", table: "news_categories");
            migrationBuilder.DropColumn(name: "NameVi", table: "news_categories");
            migrationBuilder.DropColumn(name: "NameZh", table: "news_categories");

            migrationBuilder.DropColumn(name: "NameEn", table: "project_categories");
            migrationBuilder.DropColumn(name: "NameJa", table: "project_categories");
            migrationBuilder.DropColumn(name: "NameVi", table: "project_categories");
            migrationBuilder.DropColumn(name: "NameZh", table: "project_categories");
        }
    }
}
