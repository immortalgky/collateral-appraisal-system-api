using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppraisalOtherFieldForParameter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LandShapeTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationViewTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingStyleTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandShapeTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "EnvironmentTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "LocationViewTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingStyleTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");
        }
    }
}
