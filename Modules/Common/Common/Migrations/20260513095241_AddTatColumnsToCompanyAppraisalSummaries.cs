using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddTatColumnsToCompanyAppraisalSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionCount",
                schema: "common",
                table: "CompanyAppraisalSummaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalBusinessMinutes",
                schema: "common",
                table: "CompanyAppraisalSummaries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionCount",
                schema: "common",
                table: "CompanyAppraisalSummaries");

            migrationBuilder.DropColumn(
                name: "TotalBusinessMinutes",
                schema: "common",
                table: "CompanyAppraisalSummaries");
        }
    }
}
