using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameInspectionFeeToConstructionInspectionFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InspectionFeeAmount",
                schema: "appraisal",
                table: "AppraisalFees",
                newName: "ConstructionInspectionFeeAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ConstructionInspectionFeeAmount",
                schema: "appraisal",
                table: "AppraisalFees",
                newName: "InspectionFeeAmount");
        }
    }
}
