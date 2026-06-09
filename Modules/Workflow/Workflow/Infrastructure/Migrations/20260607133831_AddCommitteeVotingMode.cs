using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitteeVotingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VotingMode",
                schema: "workflow",
                table: "Committees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WaitForAll");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VotingMode",
                schema: "workflow",
                table: "Committees");
        }
    }
}
