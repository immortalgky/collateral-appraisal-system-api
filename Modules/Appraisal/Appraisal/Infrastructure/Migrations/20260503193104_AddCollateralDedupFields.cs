using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollateralDedupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerialNo",
                schema: "appraisal",
                table: "MachineryAppraisalDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialNo",
                schema: "appraisal",
                table: "MachineryAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "TitleNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropColumn(
                name: "TitleType",
                schema: "appraisal",
                table: "CondoAppraisalDetails");
        }
    }
}
