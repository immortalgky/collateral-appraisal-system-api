using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSearchCoveringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers");

            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_RequestId",
                schema: "request",
                table: "RequestCustomers");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers",
                column: "Name")
                .Annotation("SqlServer:Include", new[] { "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_RequestId",
                schema: "request",
                table: "RequestCustomers",
                column: "RequestId")
                .Annotation("SqlServer:Include", new[] { "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers");

            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_RequestId",
                schema: "request",
                table: "RequestCustomers");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_RequestId",
                schema: "request",
                table: "RequestCustomers",
                column: "RequestId");
        }
    }
}
