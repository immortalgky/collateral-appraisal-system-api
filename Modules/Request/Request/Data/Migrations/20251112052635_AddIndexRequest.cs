using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Requests_AppraisalNo",
                schema: "request",
                table: "Requests",
                newName: "IX_Request_RequestNumber");

            migrationBuilder.AlterColumn<string>(
                name: "RequestBy",
                schema: "request",
                table: "Requests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestDate",
                schema: "request",
                table: "Requests",
                column: "RequestDate",
                descending: new bool[0],
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestedBy",
                schema: "request",
                table: "Requests",
                column: "RequestBy",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_Status",
                schema: "request",
                table: "Requests",
                column: "Status",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RequestProperty_PropertyType",
                schema: "request",
                table: "RequestProperties",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_Request_LoanApplicationNumber",
                schema: "request",
                table: "RequestDetails",
                column: "LoanApplicationNo",
                filter: "[LoanApplicationNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers",
                column: "CustomerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Request_RequestDate",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Request_RequestedBy",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Request_Status",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_RequestProperty_PropertyType",
                schema: "request",
                table: "RequestProperties");

            migrationBuilder.DropIndex(
                name: "IX_Request_LoanApplicationNumber",
                schema: "request",
                table: "RequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers");

            migrationBuilder.RenameIndex(
                name: "IX_Request_RequestNumber",
                schema: "request",
                table: "Requests",
                newName: "IX_Requests_AppraisalNo");

            migrationBuilder.AlterColumn<string>(
                name: "RequestBy",
                schema: "request",
                table: "Requests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
