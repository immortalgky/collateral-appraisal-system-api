using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceNumberToRequestTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "request",
                table: "RequestTitles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "request",
                table: "RequestTitles");
        }
    }
}
