using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDedupKeys_LandRawang_Condo4Field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoDetails_LandOffice_TitleNumber_TitleType",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.AddColumn<string>(
                name: "Rawang",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleType",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TitleNumber",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "LandOfficeCode",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "Province", "District", "SubDistrict", "TitleType", "TitleNumber", "SurveyNumber", "LandParcelNumber", "Rawang" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "CondoRegistrationNumber", "BuildingNumber", "FloorNumber", "RoomNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "Rawang",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.AlterColumn<string>(
                name: "TitleType",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleNumber",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LandOfficeCode",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "Province", "District", "SubDistrict", "TitleType", "TitleNumber", "SurveyNumber", "LandParcelNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CondoDetails_LandOffice_TitleNumber_TitleType",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "LandOfficeCode", "TitleNumber", "TitleType" });

            migrationBuilder.CreateIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "LandOfficeCode", "CondoRegistrationNumber", "BuildingNumber", "FloorNumber", "RoomNumber", "TitleNumber", "TitleType" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
