using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppraisalMachineVesselVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnginePower",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NumberOfEngines",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VesselTypeOther",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorOther",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDate",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleTypeOther",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineTypeOther",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnginePower",
                schema: "appraisal",
                table: "VesselAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "NumberOfEngines",
                schema: "appraisal",
                table: "VesselAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "VesselTypeOther",
                schema: "appraisal",
                table: "VesselAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "ColorOther",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "RegistrationDate",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "VehicleTypeOther",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MachineTypeOther",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");
        }
    }
}
