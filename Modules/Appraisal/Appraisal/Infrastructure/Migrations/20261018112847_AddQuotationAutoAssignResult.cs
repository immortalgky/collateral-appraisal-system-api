using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationAutoAssignResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoAssignMessage",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoAssignReasonCode",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoAssignResult",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoAssignedAt",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAssignMessage",
                schema: "appraisal",
                table: "QuotationRequestAppraisals");

            migrationBuilder.DropColumn(
                name: "AutoAssignReasonCode",
                schema: "appraisal",
                table: "QuotationRequestAppraisals");

            migrationBuilder.DropColumn(
                name: "AutoAssignResult",
                schema: "appraisal",
                table: "QuotationRequestAppraisals");

            migrationBuilder.DropColumn(
                name: "AutoAssignedAt",
                schema: "appraisal",
                table: "QuotationRequestAppraisals");
        }
    }
}
