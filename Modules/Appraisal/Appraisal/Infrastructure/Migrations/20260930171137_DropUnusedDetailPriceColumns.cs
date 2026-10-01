using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedDetailPriceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LandAppraisalDetails.EncroachmentArea is unmapped from the model but deliberately NOT dropped here:
            // the one-time script 20260917130000_Backfill_LandAreaDeductionFromEncroachment reads it, and DbUp
            // scripts run after EF migrations, so a drop in this release could run before that copy on a
            // database that has not had it yet. Drop the column in a later release.

            migrationBuilder.DropColumn(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
