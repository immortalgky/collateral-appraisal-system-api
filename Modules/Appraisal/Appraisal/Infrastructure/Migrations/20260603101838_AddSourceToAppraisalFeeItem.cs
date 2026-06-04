using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceToAppraisalFeeItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "appraisal",
                table: "AppraisalFeeItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "System");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                schema: "appraisal",
                table: "AppraisalFeeItems");
        }
    }
}
