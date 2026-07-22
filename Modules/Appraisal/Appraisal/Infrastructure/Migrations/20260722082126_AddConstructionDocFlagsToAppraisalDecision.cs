using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionDocFlagsToAppraisalDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasConstructionLicenseDoc",
                schema: "appraisal",
                table: "AppraisalDecisions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasConstructionPhotoDoc",
                schema: "appraisal",
                table: "AppraisalDecisions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasConstructionProgressTableDoc",
                schema: "appraisal",
                table: "AppraisalDecisions",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasConstructionLicenseDoc",
                schema: "appraisal",
                table: "AppraisalDecisions");

            migrationBuilder.DropColumn(
                name: "HasConstructionPhotoDoc",
                schema: "appraisal",
                table: "AppraisalDecisions");

            migrationBuilder.DropColumn(
                name: "HasConstructionProgressTableDoc",
                schema: "appraisal",
                table: "AppraisalDecisions");
        }
    }
}
