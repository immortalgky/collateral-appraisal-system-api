using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestPropertiesToAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankingSegment",
                schema: "appraisal",
                table: "Appraisals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "appraisal",
                table: "Appraisals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FacilityLimit",
                schema: "appraisal",
                table: "Appraisals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPma",
                schema: "appraisal",
                table: "Appraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "appraisal",
                table: "Appraisals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankingSegment",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "FacilityLimit",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "IsPma",
                schema: "appraisal",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "appraisal",
                table: "Appraisals");
        }
    }
}
