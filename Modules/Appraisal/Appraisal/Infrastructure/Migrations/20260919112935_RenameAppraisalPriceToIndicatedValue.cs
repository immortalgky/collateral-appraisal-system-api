using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppraisalPriceToIndicatedValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AppraisalPrice",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "IndicatedValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IndicatedValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "AppraisalPrice");
        }
    }
}
