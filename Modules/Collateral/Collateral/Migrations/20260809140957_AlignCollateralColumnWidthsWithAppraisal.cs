using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AlignCollateralColumnWidthsWithAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-added: SQL Server refuses to ALTER a column an index depends on (error 5074), and the
            // scaffolder does not notice. LandDetails.TitleNumber and .LandOfficeCode are both indexed,
            // so their indexes come down first and go back up at the end of Up().
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandDetails_LandOffice_TitleNumber",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectName",
                schema: "collateral",
                table: "ProjectDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LeaseRegistrationNo",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "UrbanPlanningType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "LandZoneType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandShapeType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandOfficeCode",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "LocationType",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestNumber",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "InternalAppraiserName",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AppraisalType",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "BuildingTypeCode",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            // Hand-added: rebuild the two indexes dropped at the top, now over the widened columns.
            // Key size check for the unique one: Province(100) + District(100) + SubDistrict(100)
            // + TitleNumber(200) = 500 chars = 1,000 bytes, under the 1,700-byte nonclustered limit.
            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "Province", "District", "SubDistrict", "TitleNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LandDetails_LandOffice_TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "TitleNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandDetails_LandOffice_TitleNumber",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectName",
                schema: "collateral",
                table: "ProjectDetails",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LeaseRegistrationNo",
                schema: "collateral",
                table: "LeaseholdDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "UrbanPlanningType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "LandZoneType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandShapeType",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandOfficeCode",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LocationType",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                schema: "collateral",
                table: "CollateralMasters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestNumber",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "InternalAppraiserName",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AppraisalType",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BuildingTypeCode",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            // Hand-added: rebuild the two indexes dropped at the top, now over the widened columns.
            // Key size check for the unique one: Province(100) + District(100) + SubDistrict(100)
            // + TitleNumber(200) = 500 chars = 1,000 bytes, under the 1,700-byte nonclustered limit.
            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "Province", "District", "SubDistrict", "TitleNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LandDetails_LandOffice_TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "TitleNumber" });
        }
    }
}
