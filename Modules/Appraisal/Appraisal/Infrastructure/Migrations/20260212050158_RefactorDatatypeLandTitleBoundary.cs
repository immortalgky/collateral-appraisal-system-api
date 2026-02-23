using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDatatypeLandTitleBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.AlterColumn<string>(
                name: "HasBoundaryMarker",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                column: "ComparableNumber",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.AlterColumn<bool>(
                name: "HasBoundaryMarker",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_ComparableNumber",
                schema: "appraisal",
                table: "MarketComparables",
                column: "ComparableNumber",
                unique: true);
        }
    }
}
