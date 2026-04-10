using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeStatusToScreamingSnake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize Requests.Status from PascalCase to SCREAMING_SNAKE_CASE
            migrationBuilder.Sql("""
                UPDATE request.Requests
                SET Status = CASE Status
                    WHEN 'Draft'      THEN 'DRAFT'
                    WHEN 'New'        THEN 'NEW'
                    WHEN 'Submitted'  THEN 'SUBMITTED'
                    WHEN 'Assigned'   THEN 'ASSIGNED'
                    WHEN 'INPROGRESS' THEN 'IN_PROGRESS'
                    WHEN 'InProgress' THEN 'IN_PROGRESS'
                    WHEN 'Completed'  THEN 'COMPLETED'
                    WHEN 'Cancelled'  THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Draft','New','Submitted','Assigned','INPROGRESS','InProgress','Completed','Cancelled')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert SCREAMING_SNAKE_CASE back to PascalCase
            migrationBuilder.Sql("""
                UPDATE request.Requests
                SET Status = CASE Status
                    WHEN 'DRAFT'        THEN 'Draft'
                    WHEN 'NEW'          THEN 'New'
                    WHEN 'SUBMITTED'    THEN 'Submitted'
                    WHEN 'ASSIGNED'     THEN 'Assigned'
                    WHEN 'IN_PROGRESS'  THEN 'InProgress'
                    WHEN 'COMPLETED'    THEN 'Completed'
                    WHEN 'CANCELLED'    THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('DRAFT','NEW','SUBMITTED','ASSIGNED','IN_PROGRESS','COMPLETED','CANCELLED')
                """);
        }
    }
}
