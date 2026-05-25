using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddCollateralMasterGeoPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Persisted computed geography column for spatial queries.
            // LandDetails.Latitude/Longitude are the flat owned-entity columns.
            // NULL-safe: only calls geography::Point() when both coords are non-NULL.
            migrationBuilder.Sql("""
                ALTER TABLE collateral.LandDetails
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            // Spatial index on GeoPoint.
            //
            // NOTE: SQL Server spatial indexes do NOT support a WHERE filter clause —
            // unlike regular non-clustered indexes, you cannot write
            // `CREATE SPATIAL INDEX ... WHERE Latitude IS NOT NULL`. The same
            // "land-type only" effect is achieved automatically: rows with NULL
            // Latitude/Longitude produce NULL GeoPoint (via the CASE above) and
            // SQL Server excludes them from the spatial grid pages.
            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_LandDetails_GeoPoint
                    ON collateral.LandDetails(GeoPoint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_LandDetails_GeoPoint
                    ON collateral.LandDetails;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE collateral.LandDetails
                    DROP COLUMN GeoPoint;
                """);
        }
    }
}
