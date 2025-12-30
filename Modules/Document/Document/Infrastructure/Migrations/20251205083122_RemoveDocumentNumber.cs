using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentNumber",
                schema: "document",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                schema: "document",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Checksum",
                schema: "document",
                table: "Documents",
                column: "Checksum",
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Checksum",
                schema: "document",
                table: "Documents");

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                schema: "document",
                table: "Documents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentNumber",
                schema: "document",
                table: "Documents",
                column: "DocumentNumber");
        }
    }
}
