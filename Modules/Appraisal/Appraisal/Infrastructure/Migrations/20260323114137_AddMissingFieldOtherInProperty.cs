using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFieldOtherInProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LandZoneTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyAnticipationTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingConditionTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingConditionTypeOther",
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
                name: "LandZoneTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "PropertyAnticipationTypeOther",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingConditionTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "BuildingConditionTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalDetails");
        }
    }
}
