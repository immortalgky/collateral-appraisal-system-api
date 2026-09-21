using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryRegistrationAndPriceCertification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstallationStatus",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPriceCertified",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineType",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RegistrationStatus",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallationStatus",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "IsPriceCertified",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "MachineType",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");
        }
    }
}
