using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902133000_NormalizeBoqCatalogReferenceSource")]
    public partial class NormalizeBoqCatalogReferenceSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
            UPDATE line
            SET [Quantity] = ROUND(line.[Quantity], 4),
              [UnitRate] = ROUND(line.[UnitRate], 2),
              [AmountPerSqm] = ROUND(ROUND(line.[Quantity], 4) * ROUND(line.[UnitRate], 2), 4)
            FROM [material_rate_lines] line
            INNER JOIN [material_rate_revisions] revision ON revision.[Id] = line.[RevisionId]
            INNER JOIN [material_rate_catalogs] catalog ON catalog.[Id] = revision.[CatalogId]
            WHERE catalog.[CatalogType] = N'Boq';

            UPDATE item
            SET [Amount] = ROUND(item.[Quantity] * item.[UnitPrice], 2)
            FROM [quote_items] item
            INNER JOIN [quotes] quote ON quote.[Id] = item.[QuoteId]
            WHERE quote.[Method] = N'Boq';

            WITH quote_totals AS
            (
              SELECT quote.[Id], COALESCE(SUM(item.[Amount]), 0) AS [Subtotal]
              FROM [quotes] quote
              LEFT JOIN [quote_items] item ON item.[QuoteId] = quote.[Id]
              WHERE quote.[Method] = N'Boq'
              GROUP BY quote.[Id]
            )
            UPDATE quote
            SET [Subtotal] = ROUND(totals.[Subtotal], 2),
              [GrandTotal] = ROUND(
                totals.[Subtotal]
                * (1 - quote.[DiscountPercent] / 100)
                * (1 + quote.[VatPercent] / 100),
                2)
            FROM [quotes] quote
            INNER JOIN quote_totals totals ON totals.[Id] = quote.[Id];

            WITH snapshot_totals AS
            (
              SELECT snapshot.[Id],
                N'[' + STRING_AGG(
                  CAST(JSON_MODIFY(
                    JSON_MODIFY(
                      JSON_MODIFY(item.[value], '$.Quantity', values_to_store.[Quantity]),
                      '$.UnitPrice', values_to_store.[UnitPrice]),
                    '$.Amount', values_to_store.[Amount]) AS nvarchar(max)),
                  N',') WITHIN GROUP (ORDER BY CONVERT(int, item.[key])) + N']' AS [ItemsJson]
              FROM [quote_version_snapshots] snapshot
              CROSS APPLY OPENJSON(
                CASE WHEN ISJSON(snapshot.[ItemsJson]) = 1 THEN snapshot.[ItemsJson] ELSE N'[]' END) item
              CROSS APPLY OPENJSON(item.[value])
                WITH ([Quantity] decimal(18,6) '$.Quantity', [UnitPrice] decimal(18,4) '$.UnitPrice') parsed
              CROSS APPLY
              (
                SELECT
                  CAST(ROUND(parsed.[Quantity], 4) AS decimal(18,4)) AS [Quantity],
                  CAST(ROUND(parsed.[UnitPrice], 2) AS decimal(18,2)) AS [UnitPrice],
                  CAST(ROUND(ROUND(parsed.[Quantity], 4) * ROUND(parsed.[UnitPrice], 2), 2) AS decimal(18,2)) AS [Amount]
              ) values_to_store
              WHERE snapshot.[Method] = N'Boq'
                AND ISJSON(snapshot.[ItemsJson]) = 1
              GROUP BY snapshot.[Id]
            )
            UPDATE snapshot
            SET [ItemsJson] = normalized.[ItemsJson]
            FROM [quote_version_snapshots] snapshot
            INNER JOIN snapshot_totals normalized ON normalized.[Id] = snapshot.[Id];

            WITH snapshot_totals AS
            (
              SELECT snapshot.[Id], COALESCE(SUM(item.[Amount]), 0) AS [Subtotal]
              FROM [quote_version_snapshots] snapshot
              OUTER APPLY OPENJSON(
                CASE WHEN ISJSON(snapshot.[ItemsJson]) = 1 THEN snapshot.[ItemsJson] ELSE N'[]' END)
                WITH ([Amount] decimal(18,2) '$.Amount') item
              WHERE snapshot.[Method] = N'Boq'
                AND ISJSON(snapshot.[ItemsJson]) = 1
              GROUP BY snapshot.[Id]
            )
            UPDATE snapshot
            SET [Subtotal] = ROUND(totals.[Subtotal], 2),
              [GrandTotal] = ROUND(
                totals.[Subtotal]
                * (1 - snapshot.[DiscountPercent] / 100)
                * (1 + snapshot.[VatPercent] / 100),
                2)
            FROM [quote_version_snapshots] snapshot
            INNER JOIN snapshot_totals totals ON totals.[Id] = snapshot.[Id];

                UPDATE [quotes]
                SET [RateSource] = N'CatalogReference'
                WHERE [Method] = N'Boq'
                  AND [MaterialRateRevisionId] IS NOT NULL
                  AND [RateSource] = N'Catalog';

                UPDATE [quote_version_snapshots]
                SET [RateSource] = N'CatalogReference'
                WHERE [Method] = N'Boq'
                  AND [MaterialRateRevisionId] IS NOT NULL
                  AND [RateSource] = N'Catalog';
              ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
            -- Precision normalization and total reconciliation are intentionally
            -- irreversible because the discarded extra decimals cannot be recovered.
                UPDATE [quotes]
                SET [RateSource] = N'Catalog'
                WHERE [Method] = N'Boq'
                  AND [MaterialRateRevisionId] IS NOT NULL
                  AND [RateSource] = N'CatalogReference';

                UPDATE [quote_version_snapshots]
                SET [RateSource] = N'Catalog'
                WHERE [Method] = N'Boq'
                  AND [MaterialRateRevisionId] IS NOT NULL
                  AND [RateSource] = N'CatalogReference';
              ");
        }
    }
}
