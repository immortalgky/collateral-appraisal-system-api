using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class ReKeyCollateralResultLogsByCollateral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CollateralResultLogs_Appraisal",
                schema: "collateral",
                table: "CollateralResultLogs");

            migrationBuilder.CreateIndex(
                name: "UX_CollateralResultLogs_Appraisal_Collateral",
                schema: "collateral",
                table: "CollateralResultLogs",
                columns: new[] { "AppraisalId", "CollateralId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CollateralResultLogs_Appraisal_Collateral",
                schema: "collateral",
                table: "CollateralResultLogs");

            migrationBuilder.CreateIndex(
                name: "UX_CollateralResultLogs_Appraisal",
                schema: "collateral",
                table: "CollateralResultLogs",
                column: "AppraisalId",
                unique: true);
        }
    }
}
