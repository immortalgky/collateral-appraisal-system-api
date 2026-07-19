using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryBookIntroFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Assignment",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyCharacteristics",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationPurpose",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Assignment",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries");

            migrationBuilder.DropColumn(
                name: "PropertyCharacteristics",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries");

            migrationBuilder.DropColumn(
                name: "ValuationPurpose",
                schema: "appraisal",
                table: "MachineryAppraisalSummaries");
        }
    }
}
