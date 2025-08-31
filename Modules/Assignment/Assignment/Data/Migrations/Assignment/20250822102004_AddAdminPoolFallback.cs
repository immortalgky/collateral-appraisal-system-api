using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Data.Migrations.Assignment
{
    /// <inheritdoc />
    public partial class AddAdminPoolFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminPoolId",
                schema: "assignment",
                table: "TaskAssignmentConfigurations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EscalateToAdminPool",
                schema: "assignment",
                table: "TaskAssignmentConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminPoolId",
                schema: "assignment",
                table: "TaskAssignmentConfigurations");

            migrationBuilder.DropColumn(
                name: "EscalateToAdminPool",
                schema: "assignment",
                table: "TaskAssignmentConfigurations");
        }
    }
}
