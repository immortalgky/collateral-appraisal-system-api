using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddCondoGeoPointAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            // Persisted computed geography column + spatial index — same pattern as
            // AddCollateralMasterGeoPoint. NULL-safe via the CASE; SQL Server spatial
            // indexes do not support WHERE filter clauses, but NULL GeoPoint rows are
            // excluded from the spatial grid automatically.
            migrationBuilder.Sql("""
                ALTER TABLE collateral.CondoDetails
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_CondoDetails_GeoPoint
                    ON collateral.CondoDetails(GeoPoint);
                """);

            migrationBuilder.CreateTable(
                name: "CollateralDocuments",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollateralDocuments_CollateralMasters",
                        column: x => x.CollateralMasterId,
                        principalSchema: "collateral",
                        principalTable: "CollateralMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralDocuments_CollateralMasterId",
                schema: "collateral",
                table: "CollateralDocuments",
                column: "CollateralMasterId",
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralDocuments_DocumentType",
                schema: "collateral",
                table: "CollateralDocuments",
                columns: new[] { "CollateralMasterId", "DocumentType" },
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralDocuments",
                schema: "collateral");

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_CondoDetails_GeoPoint
                    ON collateral.CondoDetails;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE collateral.CondoDetails
                    DROP COLUMN GeoPoint;
                """);

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "collateral",
                table: "CondoDetails");
        }
    }
}
