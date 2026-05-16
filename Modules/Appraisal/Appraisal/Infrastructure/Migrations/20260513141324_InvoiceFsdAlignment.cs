using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceFsdAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "appraisal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                schema: "appraisal",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "PaymentDate",
                schema: "appraisal",
                table: "Invoices",
                newName: "PaidDate");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidDate",
                schema: "appraisal",
                table: "Invoices",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Draft");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentOrderNo",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignmentSubmittedDate",
                schema: "appraisal",
                table: "InvoiceItems",
                type: "datetime2",
                nullable: true);

            // Migrate existing status values to FSD vocabulary
            migrationBuilder.Sql("""
                UPDATE appraisal.Invoices
                SET Status = CASE Status
                    WHEN 'Draft' THEN 'Pending'
                    WHEN 'Submitted' THEN 'Sent'
                    WHEN 'Approved' THEN 'Paid'
                    ELSE Status
                END
                WHERE Status IN ('Draft', 'Submitted', 'Approved')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentOrderNo",
                schema: "appraisal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AssignmentSubmittedDate",
                schema: "appraisal",
                table: "InvoiceItems");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidDate",
                schema: "appraisal",
                table: "Invoices",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "PaidDate",
                schema: "appraisal",
                table: "Invoices",
                newName: "PaymentDate");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                schema: "appraisal",
                table: "Invoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
