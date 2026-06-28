using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class CondoDropTitle_AddGeoKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "TitleNumber",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "TitleType",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubDistrict",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "CondoRegistrationNumber", "BuildingNumber", "FloorNumber", "RoomNumber", "Province", "District", "SubDistrict" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "District",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.DropColumn(
                name: "SubDistrict",
                schema: "collateral",
                table: "CondoDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "TitleNumber",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleType",
                schema: "collateral",
                table: "CondoDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_CondoDetails_DedupKey_Active",
                schema: "collateral",
                table: "CondoDetails",
                columns: new[] { "CondoRegistrationNumber", "BuildingNumber", "FloorNumber", "RoomNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
