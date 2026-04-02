using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMethodGroupDescriptionAndRemark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupDescription",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupDescription",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "Remark",
                schema: "appraisal",
                table: "PricingAnalysisMethods");
        }
    }
}
