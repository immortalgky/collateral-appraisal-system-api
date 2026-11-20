using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppraisalBookV5PropertyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RightOfWay",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "NumberOfLanes");

            migrationBuilder.RenameColumn(
                name: "RightOfWay",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "NumberOfLanes");

            migrationBuilder.RenameColumn(
                name: "RightOfWay",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "NumberOfLanes");

            migrationBuilder.RenameColumn(
                name: "AccessRoadWidth",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "CarriagewayWidth");

            migrationBuilder.RenameColumn(
                name: "AccessRoadWidth",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "CarriagewayWidth");

            migrationBuilder.RenameColumn(
                name: "AccessRoadWidth",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CarriagewayWidth");

            migrationBuilder.RenameColumn(
                name: "HasObligation",
                schema: "appraisal",
                table: "ProjectTowers",
                newName: "ObligationType");

            migrationBuilder.RenameColumn(
                name: "HasObligation",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "ObligationType");

            migrationBuilder.RenameColumn(
                name: "HasObligation",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "ObligationType");

            migrationBuilder.RenameColumn(
                name: "HasObligation",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "ObligationType");

            migrationBuilder.RenameColumn(
                name: "HasObligation",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "ObligationType");

            migrationBuilder.RenameColumn(
                name: "NoHouseNumber",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "HouseNumberStatus");

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "ProjectLands",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SanitaryTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                schema: "appraisal",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandCheckMethodType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevelopmentSuitabilityType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvictionRemark",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandLocationRemark",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeaseRegistered",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeaseholdPledgeable",
                schema: "appraisal",
                table: "LeaseAgreementDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "appraisal",
                table: "LandAreaDeductions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "LandCheckMethodType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevelopmentSuitabilityType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvictionRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloodProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasFloodProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOtherEnvironmentProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPollutionProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandLocationRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherEnvironmentProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloodProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasFloodProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOtherEnvironmentProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPollutionProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRentedOut",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherEnvironmentProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherLegalLimitations",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PollutionTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoadPassInFrontOfLand",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportationAccessType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportationAccessTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingLength",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingSystemType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingSystemTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuildingWidth",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConstructionLicenseNumber",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoorType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoorTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotAppraisableReason",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SanitaryType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpaceDivision",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DevelopmentSuitabilityType",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "EnvironmentType",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "EvictionRemark",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "LandLocationRemark",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "IsLeaseRegistered",
                schema: "appraisal",
                table: "LeaseAgreementDetails");

            migrationBuilder.DropColumn(
                name: "IsLeaseholdPledgeable",
                schema: "appraisal",
                table: "LeaseAgreementDetails");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "appraisal",
                table: "LandAreaDeductions");

            migrationBuilder.DropColumn(
                name: "DevelopmentSuitabilityType",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "EnvironmentType",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "EvictionRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "FloodProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasFloodProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasOtherEnvironmentProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasPollutionProblem",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "LandLocationRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "OtherEnvironmentProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionProblemRemark",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionType",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "FloodProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasFloodProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasOtherEnvironmentProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "HasPollutionProblem",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "IsRentedOut",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "LocationRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "OtherEnvironmentProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "OtherLegalLimitations",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionProblemRemark",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionType",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PollutionTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "RoadPassInFrontOfLand",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "TransportationAccessType",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "TransportationAccessTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColor",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingLength",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingSystemType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingSystemTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingWidth",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "ConstructionLicenseNumber",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DoorType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DoorTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MaintenanceType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MaintenanceTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MortgageDetails",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "NotAppraisableReason",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "SanitaryType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "SpaceDivision",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "WindowType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ObligationType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectTowers",
                newName: "HasObligation");

            migrationBuilder.RenameColumn(
                name: "ObligationType",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "HasObligation");

            migrationBuilder.RenameColumn(
                name: "ObligationType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "HasObligation");

            migrationBuilder.RenameColumn(
                name: "ObligationType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "HasObligation");

            migrationBuilder.RenameColumn(
                name: "ObligationType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "HasObligation");

            migrationBuilder.RenameColumn(
                name: "CarriagewayWidth",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "AccessRoadWidth");

            migrationBuilder.RenameColumn(
                name: "CarriagewayWidth",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "AccessRoadWidth");

            migrationBuilder.RenameColumn(
                name: "CarriagewayWidth",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "AccessRoadWidth");

            migrationBuilder.RenameColumn(
                name: "HouseNumberStatus",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "NoHouseNumber");

            migrationBuilder.DropColumn(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "RightOfWayWidth",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "ProjectLands");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "UrbanPlanningColorOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "SanitaryTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "WindowTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.RenameColumn(
                name: "NumberOfLanes",
                schema: "appraisal",
                table: "ProjectLands",
                newName: "RightOfWay");

            migrationBuilder.RenameColumn(
                name: "NumberOfLanes",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                newName: "RightOfWay");

            migrationBuilder.RenameColumn(
                name: "NumberOfLanes",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RightOfWay");

            migrationBuilder.AlterColumn<string>(
                name: "LandCheckMethodType",
                schema: "appraisal",
                table: "ProjectLands",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandCheckMethodType",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);
        }
    }
}
