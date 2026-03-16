using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryAppraisalSummaryAndDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsePurpose",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "UsagePurpose");

            migrationBuilder.RenameColumn(
                name: "MachinePart",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "MachineParts");

            migrationBuilder.RenameColumn(
                name: "CountryOfManufacture",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "Manufacturer");

            migrationBuilder.RenameColumn(
                name: "CanUse",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "IsOperational");

            migrationBuilder.AddColumn<decimal>(
                name: "ConditionValue",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineDimensions",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementValue",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Series",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachineryAppraisalSummaries",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InIndustrial = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SurveyedNumber = table.Column<int>(type: "int", nullable: true),
                    AppraisalNumber = table.Column<int>(type: "int", nullable: true),
                    InstalledAndUseCount = table.Column<int>(type: "int", nullable: true),
                    AppraisalScrapCount = table.Column<int>(type: "int", nullable: true),
                    AppraisedByDocumentCount = table.Column<int>(type: "int", nullable: true),
                    NotInstalledCount = table.Column<int>(type: "int", nullable: true),
                    Maintenance = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Exterior = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Performance = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MarketDemandAvailable = table.Column<bool>(type: "bit", nullable: true),
                    MarketDemand = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Proprietor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MachineAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    Obligation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Other = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryAppraisalSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineryAppraisalSummaries_AppraisalId",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries",
                column: "AppraisalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineryAppraisalSummaries",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "ConditionValue",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MachineDimensions",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "ReplacementValue",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "Series",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.RenameColumn(
                name: "UsagePurpose",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "UsePurpose");

            migrationBuilder.RenameColumn(
                name: "Manufacturer",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "CountryOfManufacture");

            migrationBuilder.RenameColumn(
                name: "MachineParts",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "MachinePart");

            migrationBuilder.RenameColumn(
                name: "IsOperational",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "CanUse");
        }
    }
}
