using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameFinalValueAdjustedToFinalValueOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinalValueAdjusted",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "FinalValueOverride");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinalValueOverride",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "FinalValueAdjusted");
        }
    }
}
