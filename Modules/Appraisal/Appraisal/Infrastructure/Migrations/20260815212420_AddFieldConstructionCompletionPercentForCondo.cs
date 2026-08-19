using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldConstructionCompletionPercentForCondo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConstructionCompletionPercent",
                schema: "appraisal",
                table: "CondoAppraisalDetails");
        }
    }
}
