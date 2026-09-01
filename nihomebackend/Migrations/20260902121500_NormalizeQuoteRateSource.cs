using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902121500_NormalizeQuoteRateSource")]
    public partial class NormalizeQuoteRateSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE quotes
                SET RateSource = 'Override',
                    RateOverrideReason = COALESCE(
                        NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                        N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                    RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                    RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME())
                     WHERE RateSource IS NULL
                         OR NOT (
                              (RateSource COLLATE Latin1_General_100_BIN2 = N'Catalog'
                                AND DATALENGTH(RateSource) = DATALENGTH(N'Catalog'))
                              OR (RateSource COLLATE Latin1_General_100_BIN2 = N'Override'
                                    AND DATALENGTH(RateSource) = DATALENGTH(N'Override'))
                         );

                UPDATE quote_version_snapshots
                SET RateSource = 'Override',
                    RateOverrideReason = COALESCE(
                        NULLIF(LTRIM(RTRIM(RateOverrideReason)), ''),
                        N'Giá được chuyển đổi từ dữ liệu trước Module 1.'),
                    RateOverrideByUserId = COALESCE(RateOverrideByUserId, CreatedByUserId),
                    RateOverrideAt = COALESCE(RateOverrideAt, CreatedAt, SYSUTCDATETIME())
                     WHERE RateSource IS NULL
                         OR NOT (
                              (RateSource COLLATE Latin1_General_100_BIN2 = N'Catalog'
                                AND DATALENGTH(RateSource) = DATALENGTH(N'Catalog'))
                              OR (RateSource COLLATE Latin1_General_100_BIN2 = N'Override'
                                    AND DATALENGTH(RateSource) = DATALENGTH(N'Override'))
                         );

                DECLARE @quoteDefault sysname;
                SELECT @quoteDefault = defaultConstraint.name
                FROM sys.default_constraints defaultConstraint
                INNER JOIN sys.columns columnDefinition
                    ON columnDefinition.object_id = defaultConstraint.parent_object_id
                   AND columnDefinition.column_id = defaultConstraint.parent_column_id
                WHERE defaultConstraint.parent_object_id = OBJECT_ID('quotes')
                  AND columnDefinition.name = 'RateSource';
                IF @quoteDefault IS NOT NULL
                BEGIN
                    DECLARE @dropQuoteDefault nvarchar(max) =
                        N'ALTER TABLE quotes DROP CONSTRAINT ' + QUOTENAME(@quoteDefault);
                    EXEC sp_executesql @dropQuoteDefault;
                END;
                ALTER TABLE quotes ADD CONSTRAINT DF_quotes_RateSource
                    DEFAULT N'Override' FOR RateSource;

                DECLARE @snapshotDefault sysname;
                SELECT @snapshotDefault = defaultConstraint.name
                FROM sys.default_constraints defaultConstraint
                INNER JOIN sys.columns columnDefinition
                    ON columnDefinition.object_id = defaultConstraint.parent_object_id
                   AND columnDefinition.column_id = defaultConstraint.parent_column_id
                WHERE defaultConstraint.parent_object_id = OBJECT_ID('quote_version_snapshots')
                  AND columnDefinition.name = 'RateSource';
                IF @snapshotDefault IS NOT NULL
                BEGIN
                    DECLARE @dropSnapshotDefault nvarchar(max) =
                        N'ALTER TABLE quote_version_snapshots DROP CONSTRAINT ' + QUOTENAME(@snapshotDefault);
                    EXEC sp_executesql @dropSnapshotDefault;
                END;
                ALTER TABLE quote_version_snapshots ADD CONSTRAINT DF_quote_version_snapshots_RateSource
                    DEFAULT N'Override' FOR RateSource;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
