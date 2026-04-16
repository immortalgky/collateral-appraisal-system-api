using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RetireTeamWorkloadSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamWorkloadSummaries",
                schema: "common");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamWorkloadSummaries",
                schema: "common",
                columns: table => new
                {
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TeamId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NotStarted = table.Column<int>(type: "int", nullable: false),
                    InProgress = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamWorkloadSummaries", x => x.Username);
                });
        }
    }
}
