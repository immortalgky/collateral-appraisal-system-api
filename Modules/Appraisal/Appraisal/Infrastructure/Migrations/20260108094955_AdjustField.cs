using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OtherPurposeUsage",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "UtilizationTypeOther");

            migrationBuilder.RenameColumn(
                name: "IsResidentialRemark",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "ResidentialRemark");

            migrationBuilder.RenameColumn(
                name: "BuildingStyle",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingStyleType");

            migrationBuilder.RenameColumn(
                name: "BuildingMaterial",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingMaterialType");

            migrationBuilder.AlterColumn<string>(
                name: "StructureType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoofType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoofFrameType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InteriorWallType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FenceType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExteriorWallType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CeilingType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(500)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UtilizationTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "OtherPurposeUsage");

            migrationBuilder.RenameColumn(
                name: "ResidentialRemark",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "IsResidentialRemark");

            migrationBuilder.RenameColumn(
                name: "BuildingStyleType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingStyle");

            migrationBuilder.RenameColumn(
                name: "BuildingMaterialType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingMaterial");

            migrationBuilder.AlterColumn<string>(
                name: "StructureType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoofType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoofFrameType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InteriorWallType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FenceType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExteriorWallType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CeilingType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldNullable: true);
        }
    }
}
