using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsCategoriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_categories", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "NewsCategoryId",
                table: "news_articles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_NewsCategoryId",
                table: "news_articles",
                column: "NewsCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_news_categories_Name",
                table: "news_categories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_news_articles_news_categories_NewsCategoryId",
                table: "news_articles",
                column: "NewsCategoryId",
                principalTable: "news_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_news_articles_news_categories_NewsCategoryId",
                table: "news_articles");

            migrationBuilder.DropIndex(
                name: "IX_news_articles_NewsCategoryId",
                table: "news_articles");

            migrationBuilder.DropIndex(
                name: "IX_news_categories_Name",
                table: "news_categories");

            migrationBuilder.DropColumn(
                name: "NewsCategoryId",
                table: "news_articles");

            migrationBuilder.DropTable(name: "news_categories");
        }
    }
}
