using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectModelLandAreaAndStartingPriceRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ProjectModel land area now expressed as a min/max range (sq.wa) plus
            // StandardLandArea, replacing the per-model Rai/Ngan/SquareWa breakdown.
            // Existing data wouldn't map cleanly (Rai is not Max, SquareWa is not Min)
            // so we drop the old columns rather than rename.
            migrationBuilder.DropColumn(
                name: "LandAreaRai",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropColumn(
                name: "LandAreaNgan",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropColumn(
                name: "LandAreaSquareWa",
                schema: "appraisal",
                table: "ProjectModels");

            // LB now uses Starting Price min/max (same as Condo). The single LB-only
            // StartingPrice column is removed; values can be re-entered as min/max.
            migrationBuilder.DropColumn(
                name: "StartingPrice",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaMin",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaMax",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandAreaMin",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropColumn(
                name: "LandAreaMax",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaRai",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaNgan",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaSquareWa",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StartingPrice",
                schema: "appraisal",
                table: "ProjectModels",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
