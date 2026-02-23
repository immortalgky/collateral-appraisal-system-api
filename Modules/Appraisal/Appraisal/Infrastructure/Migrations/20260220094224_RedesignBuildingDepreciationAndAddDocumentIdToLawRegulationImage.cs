using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignBuildingDepreciationAndAddDocumentIdToLawRegulationImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionNotes",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "DepreciationMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "EffectiveAge",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "ExternalObsolescenceAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "ExternalObsolescencePct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "FunctionalObsolescenceAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "FunctionalObsolescencePct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "MaintenanceLevel",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "PhysicalDepreciationPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "RemainingLifeYears",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "SalvageValuePercent",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "StructuralCondition",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "UsefulLifeYears",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.RenameColumn(
                name: "TotalDepreciationAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PricePerSqMAfterDepreciation");

            migrationBuilder.RenameColumn(
                name: "ReplacementCostNew",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PricePerSqM");

            migrationBuilder.RenameColumn(
                name: "PhysicalDepreciationAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PriceDepreciation");

            migrationBuilder.RenameColumn(
                name: "DepreciatedValue",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PriceBeforeDepreciation");

            migrationBuilder.RenameColumn(
                name: "AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "BuildingAppraisalDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_BuildingDepreciationDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "IX_BuildingDepreciationDetails_BuildingAppraisalDetailId");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDepreciationPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "AppraisalMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Area",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AreaDescription",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciationYearPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsBuilding",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceAfterDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<short>(
                name: "Year",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "BuildingDepreciationPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    BuildingDepreciationDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtYear = table.Column<int>(type: "int", nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    DepreciationPerYear = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    PriceDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingDepreciationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingDepreciationPeriods_BuildingDepreciationDetails_BuildingDepreciationDetailId",
                        column: x => x.BuildingDepreciationDetailId,
                        principalSchema: "appraisal",
                        principalTable: "BuildingDepreciationDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingDepreciationPeriods_BuildingDepreciationDetailId",
                schema: "appraisal",
                table: "BuildingDepreciationPeriods",
                column: "BuildingDepreciationDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingDepreciationDetails_BuildingAppraisalDetails_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                column: "BuildingAppraisalDetailId",
                principalSchema: "appraisal",
                principalTable: "BuildingAppraisalDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingDepreciationDetails_BuildingAppraisalDetails_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropTable(
                name: "BuildingDepreciationPeriods",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "LawAndRegulationImages");

            migrationBuilder.DropColumn(
                name: "AppraisalMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "AreaDescription",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "DepreciationYearPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "IsBuilding",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "PriceAfterDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "Year",
                schema: "appraisal",
                table: "BuildingDepreciationDetails");

            migrationBuilder.RenameColumn(
                name: "PricePerSqMAfterDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "TotalDepreciationAmt");

            migrationBuilder.RenameColumn(
                name: "PricePerSqM",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "ReplacementCostNew");

            migrationBuilder.RenameColumn(
                name: "PriceDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PhysicalDepreciationAmt");

            migrationBuilder.RenameColumn(
                name: "PriceBeforeDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "DepreciatedValue");

            migrationBuilder.RenameColumn(
                name: "BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "AppraisalPropertyId");

            migrationBuilder.RenameIndex(
                name: "IX_BuildingDepreciationDetails_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "IX_BuildingDepreciationDetails_AppraisalPropertyId");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDepreciationPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,4)",
                oldPrecision: 7,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "ConditionNotes",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepreciationMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EffectiveAge",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalObsolescenceAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalObsolescencePct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FunctionalObsolescenceAmt",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FunctionalObsolescencePct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceLevel",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhysicalDepreciationPct",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RemainingLifeYears",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SalvageValuePercent",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StructuralCondition",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeYears",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
