using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReappraisalCandidateGeoPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Persisted computed geography column on ReappraisalCandidates.
            // NULL-safe: geography::Point() is called only when both coords are non-NULL.
            // Rows with NULL GeoPoint are excluded from the spatial grid automatically
            // (SQL Server spatial indexes do NOT support a WHERE filter clause).
            migrationBuilder.Sql("""
                ALTER TABLE request.ReappraisalCandidates
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_ReappraisalCandidates_GeoPoint
                    ON request.ReappraisalCandidates(GeoPoint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_ReappraisalCandidates_GeoPoint
                    ON request.ReappraisalCandidates;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE request.ReappraisalCandidates
                    DROP COLUMN GeoPoint;
                """);
        }
    }
}
