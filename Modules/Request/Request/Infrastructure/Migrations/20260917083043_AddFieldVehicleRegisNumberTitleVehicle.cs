using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldVehicleRegisNumberTitleVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleRegistrationNumber",
                schema: "request",
                table: "RequestTitles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VehicleRegistrationNumber",
                schema: "request",
                table: "RequestTitles");
        }
    }
}
