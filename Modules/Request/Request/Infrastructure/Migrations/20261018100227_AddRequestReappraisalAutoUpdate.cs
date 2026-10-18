using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestReappraisalAutoUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReappraisalAutoUpdate",
                schema: "request",
                table: "Requests",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReappraisalAutoUpdate",
                schema: "request",
                table: "Requests");
        }
    }
}
