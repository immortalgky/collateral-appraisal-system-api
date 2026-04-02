using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMethodGroupDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupDescription",
                schema: "appraisal",
                table: "PricingAnalysisMethods");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupDescription",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
