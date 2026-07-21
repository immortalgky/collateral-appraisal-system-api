using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeNameTh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameTh",
                schema: "parameter",
                table: "DocumentTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameTh",
                schema: "parameter",
                table: "DocumentTypes");
        }
    }
}
