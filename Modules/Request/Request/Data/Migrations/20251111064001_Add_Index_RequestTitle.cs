using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Index_RequestTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_RequestTitles_RequestId",
                schema: "request",
                table: "RequestTitles",
                newName: "IX_TitleDeedInfo_RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDeedInfo_TitleDeedNumber",
                schema: "request",
                table: "RequestTitles",
                column: "TitleNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TitleDeedInfo_TitleDeedNumber",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.RenameIndex(
                name: "IX_TitleDeedInfo_RequestId",
                schema: "request",
                table: "RequestTitles",
                newName: "IX_RequestTitles_RequestId");
        }
    }
}
