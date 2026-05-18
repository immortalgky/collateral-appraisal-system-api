using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropAppraisalEvaluationDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Criteria1Description",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria2Description",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria3Description",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria4Description",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria5Description",
                schema: "appraisal",
                table: "AppraisalEvaluations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Criteria1Description",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria2Description",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria3Description",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria4Description",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria5Description",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
