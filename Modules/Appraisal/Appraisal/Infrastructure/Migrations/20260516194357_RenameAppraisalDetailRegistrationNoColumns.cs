using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppraisalDetailRegistrationNoColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RegistrationNo",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                newName: "RegistrationNumber");

            migrationBuilder.RenameColumn(
                name: "RegistrationNo",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                newName: "RegistrationNumber");

            migrationBuilder.RenameColumn(
                name: "RegistrationNo",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "RegistrationNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RegistrationNumber",
                schema: "appraisal",
                table: "VesselAppraisalDetails",
                newName: "RegistrationNo");

            migrationBuilder.RenameColumn(
                name: "RegistrationNumber",
                schema: "appraisal",
                table: "VehicleAppraisalDetails",
                newName: "RegistrationNo");

            migrationBuilder.RenameColumn(
                name: "RegistrationNumber",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                newName: "RegistrationNo");
        }
    }
}
