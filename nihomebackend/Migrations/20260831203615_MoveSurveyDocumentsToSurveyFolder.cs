using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class MoveSurveyDocumentsToSurveyFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [project_documents]
                SET [Category] = N'Survey',
                    [DesiredOperation] = N'Upsert',
                    [SyncStatus] = N'Pending',
                    [SyncAttemptCount] = 0,
                    [SyncError] = NULL,
                    [NextSyncAttemptAt] = SYSUTCDATETIME(),
                    [ClaimToken] = NULL,
                    [ClaimExpiresAt] = NULL,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Category] = N'CrmPreDesign'
                    AND [SourceModule] = N'Survey'
                    AND [SourceEntityType] = N'SurveyMedia'
                    AND [DesiredOperation] <> N'Delete'
                    AND [SyncStatus] <> N'Deleted';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [project_documents]
                SET [Category] = N'CrmPreDesign',
                    [DesiredOperation] = N'Upsert',
                    [SyncStatus] = N'Pending',
                    [SyncAttemptCount] = 0,
                    [SyncError] = NULL,
                    [NextSyncAttemptAt] = SYSUTCDATETIME(),
                    [ClaimToken] = NULL,
                    [ClaimExpiresAt] = NULL,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Category] = N'Survey'
                    AND [SourceModule] = N'Survey'
                    AND [SourceEntityType] = N'SurveyMedia'
                    AND [DesiredOperation] <> N'Delete'
                    AND [SyncStatus] <> N'Deleted';
                """);
        }
    }
}
