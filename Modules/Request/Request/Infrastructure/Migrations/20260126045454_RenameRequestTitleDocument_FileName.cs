using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRequestTitleDocument_FileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCaseKey",
                schema: "request",
                table: "Requests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSystem",
                schema: "request",
                table: "Requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_ExternalCaseKey",
                schema: "request",
                table: "Requests",
                column: "ExternalCaseKey",
                filter: "[ExternalCaseKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Request_ExternalCaseKey",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ExternalCaseKey",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ExternalSystem",
                schema: "request",
                table: "Requests");
        }
    }
}
