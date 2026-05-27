using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalDetailGeoPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Persisted computed geography column — LandAppraisalDetails.
            // NULL-safe: only calls geography::Point() when both Latitude and Longitude are non-NULL.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.LandAppraisalDetails
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            // Spatial index on LandAppraisalDetails.GeoPoint.
            // SQL Server spatial indexes do NOT support a WHERE filter clause;
            // rows with NULL GeoPoint are excluded from the spatial grid automatically.
            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_LandAppraisalDetails_GeoPoint
                    ON appraisal.LandAppraisalDetails(GeoPoint);
                """);

            // Persisted computed geography column — CondoAppraisalDetails.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.CondoAppraisalDetails
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            // Spatial index on CondoAppraisalDetails.GeoPoint.
            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_CondoAppraisalDetails_GeoPoint
                    ON appraisal.CondoAppraisalDetails(GeoPoint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_LandAppraisalDetails_GeoPoint
                    ON appraisal.LandAppraisalDetails;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE appraisal.LandAppraisalDetails
                    DROP COLUMN GeoPoint;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_CondoAppraisalDetails_GeoPoint
                    ON appraisal.CondoAppraisalDetails;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE appraisal.CondoAppraisalDetails
                    DROP COLUMN GeoPoint;
                """);
        }
    }
}
