using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDateGrainToCompanyAppraisalSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Event-sourced read model: clear and let the reconcile endpoint
            // rebuild rows with the correct per-day grain.
            migrationBuilder.Sql("DELETE FROM common.CompanyAppraisalSummaries;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyAppraisalSummaries",
                schema: "common",
                table: "CompanyAppraisalSummaries");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                schema: "common",
                table: "CompanyAppraisalSummaries",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyAppraisalSummaries",
                schema: "common",
                table: "CompanyAppraisalSummaries",
                columns: new[] { "CompanyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyAppraisalSummaries",
                schema: "common",
                table: "CompanyAppraisalSummaries");

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "common",
                table: "CompanyAppraisalSummaries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyAppraisalSummaries",
                schema: "common",
                table: "CompanyAppraisalSummaries",
                column: "CompanyId");
        }
    }
}
