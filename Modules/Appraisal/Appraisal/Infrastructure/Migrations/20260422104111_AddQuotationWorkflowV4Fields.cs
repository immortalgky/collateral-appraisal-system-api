using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationWorkflowV4Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuotationWorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RmNegotiationNote",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RmRequestsNegotiation",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_QuotationWorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "QuotationWorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuotationRequests_QuotationWorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "QuotationWorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "RmNegotiationNote",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "RmRequestsNegotiation",
                schema: "appraisal",
                table: "QuotationRequests");
        }
    }
}
