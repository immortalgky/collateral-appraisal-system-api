using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevertStatusToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE appraisal.Appraisals
                SET Status = CASE Status
                    WHEN 'Pending'      THEN 'PENDING'
                    WHEN 'Assigned'     THEN 'ASSIGNED'
                    WHEN 'InProgress'   THEN 'IN_PROGRESS'
                    WHEN 'UnderReview'  THEN 'UNDER_REVIEW'
                    WHEN 'Completed'    THEN 'COMPLETED'
                    WHEN 'Cancelled'    THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Pending','Assigned','InProgress','UnderReview','Completed','Cancelled')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.Appraisals
                SET SLAStatus = CASE SLAStatus
                    WHEN 'OnTrack'  THEN 'ON_TRACK'
                    WHEN 'AtRisk'   THEN 'AT_RISK'
                    WHEN 'Breached' THEN 'BREACHED'
                    ELSE SLAStatus
                END
                WHERE SLAStatus IN ('OnTrack','AtRisk','Breached')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentStatus = CASE AssignmentStatus
                    WHEN 'Pending'      THEN 'PENDING'
                    WHEN 'Assigned'     THEN 'ASSIGNED'
                    WHEN 'InProgress'   THEN 'IN_PROGRESS'
                    WHEN 'Completed'    THEN 'COMPLETED'
                    WHEN 'Rejected'     THEN 'REJECTED'
                    WHEN 'Cancelled'    THEN 'CANCELLED'
                    ELSE AssignmentStatus
                END
                WHERE AssignmentStatus IN ('Pending','Assigned','InProgress','Completed','Rejected','Cancelled')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentType = CASE AssignmentType
                    WHEN 'Internal' THEN 'INTERNAL'
                    WHEN 'External' THEN 'EXTERNAL'
                    ELSE AssignmentType
                END
                WHERE AssignmentType IN ('Internal','External')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentMethod = CASE AssignmentMethod
                    WHEN 'Manual'       THEN 'MANUAL'
                    WHEN 'AutoRule'     THEN 'AUTO_RULE'
                    WHEN 'RoundRobin'   THEN 'ROUND_ROBIN'
                    WHEN 'Quotation'    THEN 'QUOTATION'
                    ELSE AssignmentMethod
                END
                WHERE AssignmentMethod IN ('Manual','AutoRule','RoundRobin','Quotation')
                """);
        }
    }
}
