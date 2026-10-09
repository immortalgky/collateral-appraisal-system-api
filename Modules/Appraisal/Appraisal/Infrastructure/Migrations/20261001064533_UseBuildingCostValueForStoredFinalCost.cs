using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseBuildingCostValueForStoredFinalCost : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Rename only, no data change. reporting.vw_MisCasReport names this column, so the DbUp view scripts
        /// (which run after EF) must run before that view is read again.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinalCostValueOverride",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingCostValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildingCostValue",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "FinalCostValueOverride");
        }
    }
}
