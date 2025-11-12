using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class Update_RequestTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RequestTitles_RequestId",
                schema: "request",
                table: "RequestTitles",
                column: "RequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestTitles_Request",
                schema: "request",
                table: "RequestTitles",
                column: "RequestId",
                principalSchema: "request",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestTitles_Request",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitles_RequestId",
                schema: "request",
                table: "RequestTitles");
        }
    }
}
