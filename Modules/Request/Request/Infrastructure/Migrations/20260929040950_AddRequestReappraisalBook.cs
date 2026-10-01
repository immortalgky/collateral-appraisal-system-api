using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestReappraisalBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupTag",
                schema: "request",
                table: "Requests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReappraisalBookNumber",
                schema: "request",
                table: "Requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_ReappraisalBookNumber",
                schema: "request",
                table: "Requests",
                column: "ReappraisalBookNumber",
                filter: "[ReappraisalBookNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Request_ReappraisalBookNumber",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "GroupTag",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ReappraisalBookNumber",
                schema: "request",
                table: "Requests");
        }
    }
}
