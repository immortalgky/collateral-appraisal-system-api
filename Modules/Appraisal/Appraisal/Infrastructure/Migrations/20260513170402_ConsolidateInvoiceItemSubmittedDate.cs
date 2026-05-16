using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateInvoiceItemSubmittedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentSubmittedDate",
                schema: "appraisal",
                table: "InvoiceItems");

            migrationBuilder.RenameColumn(
                name: "ReceivedDate",
                schema: "appraisal",
                table: "InvoiceItems",
                newName: "SubmittedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubmittedDate",
                schema: "appraisal",
                table: "InvoiceItems",
                newName: "ReceivedDate");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignmentSubmittedDate",
                schema: "appraisal",
                table: "InvoiceItems",
                type: "datetime2",
                nullable: true);
        }
    }
}
