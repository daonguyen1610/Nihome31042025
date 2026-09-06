using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class HardenVendorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT UPPER(LTRIM(RTRIM(CompanyName)))
                    FROM procurement_vendors
                    GROUP BY UPPER(LTRIM(RTRIM(CompanyName)))
                    HAVING COUNT(*) > 1
                )
                    THROW 51030,
                        'Vendor migration blocked: duplicate normalized company names require business review.',
                        1;

                UPDATE procurement_vendors
                SET CompanyName = LTRIM(RTRIM(CompanyName));
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('procurement_vendors', 'RowVersion') IS NULL
                    ALTER TABLE procurement_vendors ADD RowVersion rowversion NOT NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'IX_procurement_vendors_CompanyName'
                )
                    CREATE UNIQUE INDEX IX_procurement_vendors_CompanyName
                    ON procurement_vendors (CompanyName);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'IX_procurement_vendors_UpdatedByUserId'
                )
                    CREATE INDEX IX_procurement_vendors_UpdatedByUserId
                    ON procurement_vendors (UpdatedByUserId);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'FK_procurement_vendors_users_UpdatedByUserId'
                )
                    ALTER TABLE procurement_vendors ADD CONSTRAINT
                    FK_procurement_vendors_users_UpdatedByUserId
                    FOREIGN KEY (UpdatedByUserId) REFERENCES users (Id) ON DELETE SET NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'FK_procurement_vendors_users_UpdatedByUserId'
                )
                    ALTER TABLE procurement_vendors DROP CONSTRAINT
                    FK_procurement_vendors_users_UpdatedByUserId;

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'IX_procurement_vendors_CompanyName'
                )
                    DROP INDEX IX_procurement_vendors_CompanyName ON procurement_vendors;

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('procurement_vendors')
                      AND name = 'IX_procurement_vendors_UpdatedByUserId'
                )
                    DROP INDEX IX_procurement_vendors_UpdatedByUserId ON procurement_vendors;

                IF COL_LENGTH('procurement_vendors', 'RowVersion') IS NOT NULL
                    ALTER TABLE procurement_vendors DROP COLUMN RowVersion;
                """);
        }
    }
}
