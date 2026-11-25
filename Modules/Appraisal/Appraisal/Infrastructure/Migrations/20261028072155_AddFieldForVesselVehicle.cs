using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldForVesselVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HullNumber",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicensePlateNumber",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HullNumber",
                schema: "appraisal",
                table: "VesselAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "LicensePlateNumber",
                schema: "appraisal",
                table: "VehicleAppraisalDetails");
        }
    }
}
