using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeMetadataFromRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeeNotes",
                schema: "appraisal",
                table: "AppraisalFees",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeePaymentType",
                schema: "appraisal",
                table: "AppraisalFees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeeNotes",
                schema: "appraisal",
                table: "AppraisalFees");

            migrationBuilder.DropColumn(
                name: "FeePaymentType",
                schema: "appraisal",
                table: "AppraisalFees");
        }
    }
}
