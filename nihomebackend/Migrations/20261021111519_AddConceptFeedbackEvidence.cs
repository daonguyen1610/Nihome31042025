using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddConceptFeedbackEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceSource",
                table: "concept_feedback",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "concept_feedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM concept_feedback
                    WHERE EvidenceSource IS NOT NULL OR Outcome IS NOT NULL)
                    THROW 51000, 'Rollback blocked while Concept feedback evidence exists.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "EvidenceSource",
                table: "concept_feedback");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "concept_feedback");
        }
    }
}
