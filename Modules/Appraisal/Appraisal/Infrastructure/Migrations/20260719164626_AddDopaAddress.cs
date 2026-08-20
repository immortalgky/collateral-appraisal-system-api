using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDopaAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLandOffice",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "DopaDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DopaProvince",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DopaSubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DopaDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DopaProvince",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DopaSubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DopaDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DopaProvince",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DopaSubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DopaDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DopaProvince",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "DopaSubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.AddColumn<string>(
                name: "AddressLandOffice",
                schema: "appraisal",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
