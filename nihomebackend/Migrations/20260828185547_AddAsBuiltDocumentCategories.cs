using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAsBuiltDocumentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create the categories table first
            migrationBuilder.CreateTable(
                name: "as_built_document_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameVi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameZh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameJa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_as_built_document_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_as_built_document_categories_Code",
                table: "as_built_document_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_as_built_document_categories_IsActive",
                table: "as_built_document_categories",
                column: "IsActive");

            // Step 2: Seed default categories
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            migrationBuilder.Sql($@"
                INSERT INTO as_built_document_categories (Code, Name, NameVi, NameEn, NameZh, NameJa, IsRequired, IsActive, SortOrder, CreatedAt, UpdatedAt)
                VALUES
                    ('Drawing', N'Bản vẽ hoàn công', N'Bản vẽ hoàn công', 'As-built drawings', N'竣工图纸', N'竣工図面', 1, 1, 1, '{now}', '{now}'),
                    ('AcceptanceMinute', N'Biên bản nghiệm thu', N'Biên bản nghiệm thu', 'Acceptance minutes', N'验收记录', N'検収議事録', 1, 1, 2, '{now}', '{now}'),
                    ('TestReport', N'Báo cáo thí nghiệm', N'Báo cáo thí nghiệm', 'Test reports', N'测试报告', N'試験報告書', 1, 1, 3, '{now}', '{now}'),
                    ('WarrantyCertificate', N'Chứng chỉ bảo hành', N'Chứng chỉ bảo hành', 'Warranty certificates', N'保修证书', N'保証書', 1, 1, 4, '{now}', '{now}'),
                    ('Other', N'Tài liệu khác', N'Tài liệu khác', 'Other supporting documents', N'其他支持文件', N'その他の書類', 0, 1, 5, '{now}', '{now}')
            ");

            // Step 3: Add CategoryId column (nullable first for data migration)
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "as_built_documents",
                type: "int",
                nullable: true);

            // Step 4: Migrate existing data - map old Category enum string to new CategoryId
            migrationBuilder.Sql(@"
                UPDATE as_built_documents
                SET CategoryId = (SELECT Id FROM as_built_document_categories WHERE Code = as_built_documents.Category)
                WHERE Category IS NOT NULL
            ");

            // Step 5: Set default CategoryId for any remaining nulls (fallback to 'Other')
            migrationBuilder.Sql(@"
                UPDATE as_built_documents
                SET CategoryId = (SELECT Id FROM as_built_document_categories WHERE Code = 'Other')
                WHERE CategoryId IS NULL
            ");

            // Step 6: Make CategoryId non-nullable now that all rows have values
            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "as_built_documents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Step 7: Drop old Category column and index
            migrationBuilder.DropIndex(
                name: "IX_as_built_documents_Category",
                table: "as_built_documents");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "as_built_documents");

            // Step 8: Add FK and index
            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_CategoryId",
                table: "as_built_documents",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_as_built_documents_as_built_document_categories_CategoryId",
                table: "as_built_documents",
                column: "CategoryId",
                principalTable: "as_built_document_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_as_built_documents_as_built_document_categories_CategoryId",
                table: "as_built_documents");

            migrationBuilder.DropTable(
                name: "as_built_document_categories");

            migrationBuilder.DropIndex(
                name: "IX_as_built_documents_CategoryId",
                table: "as_built_documents");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "as_built_documents");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "as_built_documents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_as_built_documents_Category",
                table: "as_built_documents",
                column: "Category");
        }
    }
}
