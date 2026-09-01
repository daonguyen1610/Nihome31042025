using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902123000_ValidateHistoricalSurveyProjectRouting")]
    public partial class ValidateHistoricalSurveyProjectRouting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM surveys survey
                    LEFT JOIN opportunities opportunity
                        ON opportunity.Id = survey.LinkedOpportunityId
                    WHERE opportunity.Id IS NULL
                       OR opportunity.OperationalProjectId IS NULL
                       OR survey.OperationalProjectId <> opportunity.OperationalProjectId
                )
                    THROW 51001,
                        'Không thể xác nhận dự án của phiếu khảo sát lịch sử từ cơ hội liên kết.',
                        1;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
