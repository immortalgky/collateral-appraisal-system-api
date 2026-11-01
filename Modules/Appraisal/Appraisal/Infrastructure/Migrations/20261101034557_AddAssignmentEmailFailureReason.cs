using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentEmailFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "appraisal",
                table: "AssignmentEmails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "appraisal",
                table: "AssignmentEmails");
        }
    }
}
