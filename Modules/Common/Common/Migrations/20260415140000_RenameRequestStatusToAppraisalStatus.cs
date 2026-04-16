using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RenameRequestStatusToAppraisalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestStatusSummaries",
                schema: "common");

            migrationBuilder.CreateTable(
                name: "AppraisalStatusSummaries",
                schema: "common",
                columns: table => new
                {
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalStatusSummaries", x => x.Status);
                });

            // Seed initial rows for all 5 appraisal lifecycle statuses
            migrationBuilder.InsertData(
                table: "AppraisalStatusSummaries",
                schema: "common",
                columns: ["Status", "Count", "LastUpdatedAt"],
                values: new object[,]
                {
                    { "Pending",     0, DateTimeOffset.UtcNow },
                    { "InProgress",  0, DateTimeOffset.UtcNow },
                    { "UnderReview", 0, DateTimeOffset.UtcNow },
                    { "Completed",   0, DateTimeOffset.UtcNow },
                    { "Cancelled",   0, DateTimeOffset.UtcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalStatusSummaries",
                schema: "common");

            migrationBuilder.CreateTable(
                name: "RequestStatusSummaries",
                schema: "common",
                columns: table => new
                {
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestStatusSummaries", x => x.Status);
                });
        }
    }
}
