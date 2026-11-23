using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationCriteriaRemarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Criteria1Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria2Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria3Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria4Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Criteria5Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Criteria1Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria2Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria3Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria4Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations");

            migrationBuilder.DropColumn(
                name: "Criteria5Remark",
                schema: "appraisal",
                table: "AppraisalEvaluations");
        }
    }
}
