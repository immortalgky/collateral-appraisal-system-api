using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPMAFieldProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuildingInsurancePrice",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                schema: "appraisal",
                table: "AppraisalProperties",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingInsurancePrice",
                schema: "appraisal",
                table: "AppraisalProperties");

            migrationBuilder.DropColumn(
                name: "ForcedSalePrice",
                schema: "appraisal",
                table: "AppraisalProperties");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                schema: "appraisal",
                table: "AppraisalProperties");
        }
    }
}
