using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAppraisalPropertyConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandAndBuildingAppraisalDetails",
                schema: "appraisal");

            migrationBuilder.DropIndex(
                name: "IX_LandTitles_AppraisalPropertyId_SequenceNumber",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "AerialPhotoName",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "BoundaryMarker",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "DocumentValidation",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "IsMissedOutSurvey",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "PricePerSquareWa",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "Remarks",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "IsEncroached",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.RenameColumn(
                name: "TotalAreaInSquareWa",
                schema: "appraisal",
                table: "LandTitles",
                newName: "GovernmentPricePerSqWa");

            migrationBuilder.RenameColumn(
                name: "SheetNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "MapSheetNumber");

            migrationBuilder.RenameColumn(
                name: "LandNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "LandParcelNumber");

            migrationBuilder.RenameColumn(
                name: "DocumentType",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TitleDeedType");

            migrationBuilder.RenameColumn(
                name: "BoundaryMarkerOther",
                schema: "appraisal",
                table: "LandTitles",
                newName: "AerialMapName");

            migrationBuilder.RenameColumn(
                name: "AppraisalPropertyId",
                schema: "appraisal",
                table: "LandTitles",
                newName: "LandAppraisalDetailId");

            migrationBuilder.RenameColumn(
                name: "AerialPhotoNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "AerialMapNumber");

            migrationBuilder.RenameIndex(
                name: "IX_LandTitles_AppraisalPropertyId",
                schema: "appraisal",
                table: "LandTitles",
                newName: "IX_LandTitles_LandAppraisalDetailId");

            migrationBuilder.RenameColumn(
                name: "EncroachmentRemark",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "EncroachingOthersRemark");

            migrationBuilder.RenameColumn(
                name: "EncroachmentArea",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "EncroachingOthersArea");

            migrationBuilder.AlterColumn<decimal>(
                name: "AreaRai",
                schema: "appraisal",
                table: "LandTitles",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AreaNgan",
                schema: "appraisal",
                table: "LandTitles",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryMarkerRemark",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBoundaryMarker",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMissingFromSurvey",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoyalDecree",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "RightOfWay",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OtherLegalLimitations",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsLandlocked",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsInExpropriationLine",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsForestBoundary",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsExpropriated",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEncroached",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "HasBuilding",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "RoyalDecree",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "RightOfWay",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NumberOfFloors",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LocationViewType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsInExpropriationLine",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsForestBoundary",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsExpropriated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "RoadSurfaceTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NumberOfFloors",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsAppraisable",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<bool>(
                name: "IsEncroachingOthers",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LandTitles_LandAppraisalDetails_LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandTitles",
                column: "LandAppraisalDetailId",
                principalSchema: "appraisal",
                principalTable: "LandAppraisalDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LandTitles_LandAppraisalDetails_LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "BoundaryMarkerRemark",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "HasBoundaryMarker",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "IsMissingFromSurvey",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "Remark",
                schema: "appraisal",
                table: "LandTitles");

            migrationBuilder.DropColumn(
                name: "RoadSurfaceTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "IsEncroachingOthers",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");

            migrationBuilder.RenameColumn(
                name: "TitleDeedType",
                schema: "appraisal",
                table: "LandTitles",
                newName: "DocumentType");

            migrationBuilder.RenameColumn(
                name: "MapSheetNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "SheetNumber");

            migrationBuilder.RenameColumn(
                name: "LandParcelNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "LandNumber");

            migrationBuilder.RenameColumn(
                name: "LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandTitles",
                newName: "AppraisalPropertyId");

            migrationBuilder.RenameColumn(
                name: "GovernmentPricePerSqWa",
                schema: "appraisal",
                table: "LandTitles",
                newName: "TotalAreaInSquareWa");

            migrationBuilder.RenameColumn(
                name: "AerialMapNumber",
                schema: "appraisal",
                table: "LandTitles",
                newName: "AerialPhotoNumber");

            migrationBuilder.RenameColumn(
                name: "AerialMapName",
                schema: "appraisal",
                table: "LandTitles",
                newName: "BoundaryMarkerOther");

            migrationBuilder.RenameIndex(
                name: "IX_LandTitles_LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandTitles",
                newName: "IX_LandTitles_AppraisalPropertyId");

            migrationBuilder.RenameColumn(
                name: "EncroachingOthersRemark",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "EncroachmentRemark");

            migrationBuilder.RenameColumn(
                name: "EncroachingOthersArea",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "EncroachmentArea");

            migrationBuilder.AlterColumn<int>(
                name: "AreaRai",
                schema: "appraisal",
                table: "LandTitles",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AreaNgan",
                schema: "appraisal",
                table: "LandTitles",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AerialPhotoName",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryMarker",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentValidation",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMissedOutSurvey",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerSquareWa",
                schema: "appraisal",
                table: "LandTitles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "appraisal",
                table: "LandTitles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "RoyalDecree",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RightOfWay",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OtherLegalLimitations",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsLandlocked",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsInExpropriationLine",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsForestBoundary",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsExpropriated",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEncroached",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "HasBuilding",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoyalDecree",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RightOfWay",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NumberOfFloors",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LocationViewType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsInExpropriationLine",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsForestBoundary",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsExpropriated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NumberOfFloors",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsUnderConstruction",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwnerVerified",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsAppraisable",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "HasObligation",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEncroached",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LandAndBuildingAppraisalDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AccessRoadType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AddressLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllocationStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    BuildingAreaUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BuildingCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuildingInsurancePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BuildingMaterial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuildingPermitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BuildingPermitNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingRemark = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BuildingStyle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuildingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BuiltOnTitleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CeilingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CeilingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionCompletionPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ConstructionLicenseExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConstructionStyleRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConstructionStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EastAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EastBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ElectricityAvailable = table.Column<bool>(type: "bit", nullable: true),
                    ElectricityDistance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EncroachmentArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EncroachmentRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvictionStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvictionStatusOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExteriorWallType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FenceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FloodRisk = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FloorMaterial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ForcedSalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FoundationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: false),
                    HasOccupancyPermit = table.Column<bool>(type: "bit", nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InteriorWallType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsAppraisable = table.Column<bool>(type: "bit", nullable: false),
                    IsEncroached = table.Column<bool>(type: "bit", nullable: false),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: false),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: false),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: false),
                    IsLandlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    IsResidentialRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsUnderConstruction = table.Column<bool>(type: "bit", nullable: false),
                    LandAccessibility = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandAccessibilityDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandCheckMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandCheckMethodOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandEntranceExit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandEntranceExitOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandFillPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandFillStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandFillStatusOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandLocationVerification = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandRemark = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LandShape = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandUseZoning = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandUseZoningOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandlockedRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaintenanceStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NorthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NorthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NumberOfBathrooms = table.Column<int>(type: "int", nullable: true),
                    NumberOfBedrooms = table.Column<int>(type: "int", nullable: true),
                    NumberOfBuildings = table.Column<int>(type: "int", nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    NumberOfSidesFacingRoad = table.Column<int>(type: "int", nullable: true),
                    NumberOfUnits = table.Column<int>(type: "int", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccupancyStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OtherLegalLimitations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OtherPurposeUsage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnershipDocument = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnershipPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OwnershipType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlotLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlotLocationOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PondArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PondDepth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PropertyAnticipation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PropertyUsage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PublicUtilities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicUtilitiesOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RenovationHistory = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RightOfWay = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadPassInFrontOfLand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofFrameType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoofFrameTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofMaterial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SewerageAvailable = table.Column<bool>(type: "bit", nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SoilCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SoilLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SouthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SouthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StructureType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StructureTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SurveyPageNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TerrainType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleDeedNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TitleDeedType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalBuildingArea = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TransportationAccess = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransportationAccessOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UtilizationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WallMaterial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WaterSupplyAvailable = table.Column<bool>(type: "bit", nullable: true),
                    WestAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WestBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LandAreaSqWa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandAndBuildingAppraisalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAndBuildingAppraisalDetails_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandTitles_AppraisalPropertyId_SequenceNumber",
                schema: "appraisal",
                table: "LandTitles",
                columns: new[] { "AppraisalPropertyId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandAndBuildingAppraisalDetails_AppraisalPropertyId",
                schema: "appraisal",
                table: "LandAndBuildingAppraisalDetails",
                column: "AppraisalPropertyId",
                unique: true);
        }
    }
}
