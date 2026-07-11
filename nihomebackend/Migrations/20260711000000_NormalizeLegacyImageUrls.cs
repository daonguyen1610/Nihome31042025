using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NihomeBackend.Data;

#nullable disable

namespace nihomebackend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711000000_NormalizeLegacyImageUrls")]
    public partial class NormalizeLegacyImageUrls : Migration
    {
        // Strips leftover `http://localhost:5043` (and https/127.0.0.1 variants)
        // from every column that stores an image/media URL or a JSON blob that
        // may embed one. Fresh installs seeded from ContentSeeder.cs already ship
        // clean paths; this migration only affects environments whose data
        // predates that fix (see review of branch seed/refector-wwwroot/images).
        private static readonly (string Table, string Column)[] TargetColumns =
        {
            ("projects",                "ImageUrl"),
            ("projects",                "GalleryJson"),
            ("activities",              "ImageUrl"),
            ("activities",              "GalleryJson"),
            ("activities",              "ContentJson"),
            ("news_articles",           "ImageUrl"),
            ("news_articles",           "GalleryJson"),
            ("news_articles",           "ContentJson"),
            ("service_items",           "SectionsJson"),
            ("service_items",           "IntroBlocksJson"),
            ("client_logos",            "ImageUrl"),
            ("slideshow_items",         "ImageUrl"),
            ("slideshow_items",         "LinkUrl"),
            ("about_section_contents",  "ImageUrl"),
            ("about_section_contents",  "ItemsJson"),
            ("entity_translations",     "Value"),
        };

        private static readonly string[] LegacyPrefixes =
        {
            "http://localhost:5043",
            "https://localhost:5043",
            "http://localhost",
            "https://localhost",
            "http://127.0.0.1:5043",
            "https://127.0.0.1:5043",
            "http://127.0.0.1",
            "https://127.0.0.1",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in TargetColumns)
            {
                foreach (var prefix in LegacyPrefixes)
                {
                    // Escape single quotes for SQL literal safety. None of the
                    // hardcoded prefixes contain apostrophes, but keep the
                    // pattern in case the list grows.
                    var safePrefix = prefix.Replace("'", "''");
                    migrationBuilder.Sql(
                        $"UPDATE [{table}] SET [{column}] = REPLACE([{column}], '{safePrefix}', '') " +
                        $"WHERE [{column}] LIKE '%{safePrefix}%';");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: data cleanup is not reversible. Re-introducing
            // absolute dev-host prefixes would break every deployed environment.
        }
    }
}
