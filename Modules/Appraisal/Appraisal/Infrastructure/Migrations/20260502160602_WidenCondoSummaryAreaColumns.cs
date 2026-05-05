using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenCondoSummaryAreaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_TotalBuildingArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_ProjectSalesArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_IndoorSalesArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_ConstructionAreaCityPlan",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_CommonArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_BuildingArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_AreaTitleDeed",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_AreaSqM",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(13,2)",
                precision: 13,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_TotalBuildingArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_ProjectSalesArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_IndoorSalesArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_ConstructionAreaCityPlan",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_CommonArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_BuildingArea",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_AreaTitleDeed",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CondominiumSummary_AreaSqM",
                schema: "appraisal",
                table: "HypothesisAnalyses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,2)",
                oldPrecision: 13,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
