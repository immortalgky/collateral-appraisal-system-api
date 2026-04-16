using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevertStatusToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Requests.Status: SCREAMING_SNAKE → PascalCase
            migrationBuilder.Sql("""
                UPDATE request.Requests
                SET Status = CASE Status
                    WHEN 'DRAFT'        THEN 'Draft'
                    WHEN 'NEW'          THEN 'New'
                    WHEN 'SUBMITTED'    THEN 'Submitted'
                    WHEN 'ASSIGNED'     THEN 'Assigned'
                    WHEN 'IN_PROGRESS'  THEN 'InProgress'
                    WHEN 'INPROGRESS'   THEN 'InProgress'
                    WHEN 'COMPLETED'    THEN 'Completed'
                    WHEN 'CANCELLED'    THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('DRAFT','NEW','SUBMITTED','ASSIGNED','IN_PROGRESS','INPROGRESS','COMPLETED','CANCELLED')
                """);

            // common.RequestStatusSummaries: UPPER → PascalCase (dashboard aggregation table)
            migrationBuilder.Sql("""
                UPDATE common.RequestStatusSummaries
                SET Status = CASE Status
                    WHEN 'DRAFT'        THEN 'Draft'
                    WHEN 'NEW'          THEN 'New'
                    WHEN 'SUBMITTED'    THEN 'Submitted'
                    WHEN 'ASSIGNED'     THEN 'Assigned'
                    WHEN 'IN_PROGRESS'  THEN 'InProgress'
                    WHEN 'INPROGRESS'   THEN 'InProgress'
                    WHEN 'COMPLETED'    THEN 'Completed'
                    WHEN 'CANCELLED'    THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('DRAFT','NEW','SUBMITTED','ASSIGNED','IN_PROGRESS','INPROGRESS','COMPLETED','CANCELLED')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE request.Requests
                SET Status = CASE Status
                    WHEN 'Draft'        THEN 'DRAFT'
                    WHEN 'New'          THEN 'NEW'
                    WHEN 'Submitted'    THEN 'SUBMITTED'
                    WHEN 'Assigned'     THEN 'ASSIGNED'
                    WHEN 'InProgress'   THEN 'IN_PROGRESS'
                    WHEN 'Completed'    THEN 'COMPLETED'
                    WHEN 'Cancelled'    THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Draft','New','Submitted','Assigned','InProgress','Completed','Cancelled')
                """);

            migrationBuilder.Sql("""
                UPDATE common.RequestStatusSummaries
                SET Status = CASE Status
                    WHEN 'Draft'        THEN 'DRAFT'
                    WHEN 'New'          THEN 'NEW'
                    WHEN 'Submitted'    THEN 'SUBMITTED'
                    WHEN 'Assigned'     THEN 'ASSIGNED'
                    WHEN 'InProgress'   THEN 'IN_PROGRESS'
                    WHEN 'Completed'    THEN 'COMPLETED'
                    WHEN 'Cancelled'    THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Draft','New','Submitted','Assigned','InProgress','Completed','Cancelled')
                """);
        }
    }
}
