using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRequestAppraisalGroupNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Request_AppraisalGroupNumber",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AppraisalGroupNumber",
                schema: "request",
                table: "Requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisalGroupNumber",
                schema: "request",
                table: "Requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_AppraisalGroupNumber",
                schema: "request",
                table: "Requests",
                column: "AppraisalGroupNumber",
                filter: "[AppraisalGroupNumber] IS NOT NULL");
        }
    }
}
