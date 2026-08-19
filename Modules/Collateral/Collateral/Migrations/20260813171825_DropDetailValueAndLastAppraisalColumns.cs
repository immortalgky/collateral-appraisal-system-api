using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class DropDetailValueAndLastAppraisalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LandDetails_UnderConstruction",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "ProjectDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "ProjectDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "ProjectDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "MachineDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "LeaseholdDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "BuildingValue",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "IsUnderConstructionAtLastAppraisal",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "OverallConstructionProgressPercent",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "AppraisalValue",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "BuildingValue",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "collateral",
                table: "CondoDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "ProjectDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "ProjectDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "ProjectDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "MachineDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "MachineDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "MachineDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "MachineDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingValue",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderConstructionAtLastAppraisal",
                schema: "collateral",
                table: "LandDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "LandDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "LandDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallConstructionProgressPercent",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "collateral",
                table: "LandDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisalValue",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingValue",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAppraisalId",
                schema: "collateral",
                table: "CondoDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAppraisalNumber",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAppraisedDate",
                schema: "collateral",
                table: "CondoDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "collateral",
                table: "CondoDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandDetails_UnderConstruction",
                schema: "collateral",
                table: "LandDetails",
                column: "IsUnderConstructionAtLastAppraisal",
                filter: "[IsUnderConstructionAtLastAppraisal] = 1");
        }
    }
}
