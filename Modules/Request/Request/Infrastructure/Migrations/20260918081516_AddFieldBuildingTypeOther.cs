using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldBuildingTypeOther : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildingTypeOther",
                schema: "request",
                table: "RequestProperties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingTypeOther",
                schema: "request",
                table: "RequestProperties");
        }
    }
}
