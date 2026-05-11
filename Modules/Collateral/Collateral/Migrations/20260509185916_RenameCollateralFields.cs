using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class RenameCollateralFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.RenameColumn(
                name: "TitleDeedType",
                schema: "collateral",
                table: "LandDetails",
                newName: "TitleType");

            migrationBuilder.RenameColumn(
                name: "TitleDeedNo",
                schema: "collateral",
                table: "LandDetails",
                newName: "TitleNumber");

            migrationBuilder.RenameColumn(
                name: "Tambon",
                schema: "collateral",
                table: "LandDetails",
                newName: "SubDistrict");

            migrationBuilder.RenameColumn(
                name: "SurveyOrParcelNo",
                schema: "collateral",
                table: "LandDetails",
                newName: "SurveyNumber");

            migrationBuilder.RenameColumn(
                name: "Amphur",
                schema: "collateral",
                table: "LandDetails",
                newName: "District");

            migrationBuilder.RenameIndex(
                name: "IX_LandDetails_LandOffice_TitleDeedNo",
                schema: "collateral",
                table: "LandDetails",
                newName: "IX_LandDetails_LandOffice_TitleNumber");

            migrationBuilder.RenameColumn(
                name: "UnitNumber",
                schema: "collateral",
                table: "CondoDetails",
                newName: "RoomNumber");

            migrationBuilder.RenameColumn(
                name: "UpdatedOn",
                schema: "collateral",
                table: "CollateralMasters",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "collateral",
                table: "CollateralMasters",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "collateral",
                table: "CollateralEngagements",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<string>(
                name: "LandParcelNumber",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "Province", "District", "SubDistrict", "TitleType", "TitleNumber", "SurveyNumber", "LandParcelNumber" },
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

            migrationBuilder.DropColumn(
                name: "LandParcelNumber",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.RenameColumn(
                name: "TitleType",
                schema: "collateral",
                table: "LandDetails",
                newName: "TitleDeedType");

            migrationBuilder.RenameColumn(
                name: "TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                newName: "TitleDeedNo");

            migrationBuilder.RenameColumn(
                name: "SurveyNumber",
                schema: "collateral",
                table: "LandDetails",
                newName: "SurveyOrParcelNo");

            migrationBuilder.RenameColumn(
                name: "SubDistrict",
                schema: "collateral",
                table: "LandDetails",
                newName: "Tambon");

            migrationBuilder.RenameColumn(
                name: "District",
                schema: "collateral",
                table: "LandDetails",
                newName: "Amphur");

            migrationBuilder.RenameIndex(
                name: "IX_LandDetails_LandOffice_TitleNumber",
                schema: "collateral",
                table: "LandDetails",
                newName: "IX_LandDetails_LandOffice_TitleDeedNo");

            migrationBuilder.RenameColumn(
                name: "RoomNumber",
                schema: "collateral",
                table: "CondoDetails",
                newName: "UnitNumber");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "collateral",
                table: "CollateralMasters",
                newName: "UpdatedOn");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "collateral",
                table: "CollateralMasters",
                newName: "CreatedOn");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "collateral",
                table: "CollateralEngagements",
                newName: "CreatedOn");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "collateral",
                table: "LandDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "LandOfficeCode", "Province", "Amphur", "Tambon", "TitleDeedType", "TitleDeedNo", "SurveyOrParcelNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
