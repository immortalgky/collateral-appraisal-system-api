using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketComparableGeoLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByCompanyId",
                schema: "appraisal",
                table: "MarketComparables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            // Persisted computed column — NULL-safe: only calls geography::Point() when both
            // Latitude and Longitude are non-NULL. PERSISTED requires the expression to be
            // deterministic; geography::Point with literal SRID 4326 satisfies that requirement.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.MarketComparables
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            // Spatial index on the computed column (geography type uses default tessellation).
            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_MarketComparables_GeoPoint
                    ON appraisal.MarketComparables(GeoPoint);
                """);

            // Best-effort backfill: stamp CreatedByCompanyId from auth.AspNetUsers.
            // CreatedBy stores the username (nvarchar) stamped by AuditableEntityInterceptor.
            // Rows where the user has no company (bank-internal users) stay NULL — that is correct.
            migrationBuilder.Sql("""
                UPDATE mc
                SET    mc.CreatedByCompanyId = u.CompanyId
                FROM   appraisal.MarketComparables mc
                JOIN   auth.AspNetUsers u ON u.UserName = mc.CreatedBy
                WHERE  mc.CreatedByCompanyId IS NULL
                  AND  u.CompanyId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop spatial index before dropping the computed column it is built on.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_MarketComparables_GeoPoint
                    ON appraisal.MarketComparables;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE appraisal.MarketComparables
                    DROP COLUMN GeoPoint;
                """);

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "CreatedByCompanyId",
                schema: "appraisal",
                table: "MarketComparables");
        }
    }
}
