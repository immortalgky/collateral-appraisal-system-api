using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeStatusToScreamingSnake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Appraisals.Status
            migrationBuilder.Sql("""
                UPDATE appraisal.Appraisals
                SET Status = CASE Status
                    WHEN 'Pending'     THEN 'PENDING'
                    WHEN 'Assigned'    THEN 'ASSIGNED'
                    WHEN 'InProgress'  THEN 'IN_PROGRESS'
                    WHEN 'UnderReview' THEN 'UNDER_REVIEW'
                    WHEN 'Completed'   THEN 'COMPLETED'
                    WHEN 'Cancelled'   THEN 'CANCELLED'
                    ELSE Status
                END
                WHERE Status IN ('Pending','Assigned','InProgress','UnderReview','Completed','Cancelled')
                """);

            // Appraisals.SLAStatus
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

            // AppraisalAssignments.AssignmentStatus
            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentStatus = CASE AssignmentStatus
                    WHEN 'Pending'    THEN 'PENDING'
                    WHEN 'Assigned'   THEN 'ASSIGNED'
                    WHEN 'InProgress' THEN 'IN_PROGRESS'
                    WHEN 'Completed'  THEN 'COMPLETED'
                    WHEN 'Rejected'   THEN 'REJECTED'
                    WHEN 'Cancelled'  THEN 'CANCELLED'
                    ELSE AssignmentStatus
                END
                WHERE AssignmentStatus IN ('Pending','Assigned','InProgress','Completed','Rejected','Cancelled')
                """);

            // AppraisalAssignments.AssignmentType
            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentType = CASE AssignmentType
                    WHEN 'Internal' THEN 'INTERNAL'
                    WHEN 'External' THEN 'EXTERNAL'
                    ELSE AssignmentType
                END
                WHERE AssignmentType IN ('Internal','External')
                """);

            // AppraisalAssignments.AssignmentMethod
            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentMethod = CASE AssignmentMethod
                    WHEN 'Manual'     THEN 'MANUAL'
                    WHEN 'AutoRule'   THEN 'AUTO_RULE'
                    WHEN 'RoundRobin' THEN 'ROUND_ROBIN'
                    WHEN 'Quotation'  THEN 'QUOTATION'
                    ELSE AssignmentMethod
                END
                WHERE AssignmentMethod IN ('Manual','AutoRule','RoundRobin','Quotation')
                """);

            // AppraisalAssignments.InternalFollowupAssignmentMethod
            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET InternalFollowupAssignmentMethod = CASE InternalFollowupAssignmentMethod
                    WHEN 'Manual'     THEN 'MANUAL'
                    WHEN 'RoundRobin' THEN 'ROUND_ROBIN'
                    ELSE InternalFollowupAssignmentMethod
                END
                WHERE InternalFollowupAssignmentMethod IN ('Manual','RoundRobin')
                """);

            // AppraisalReviews.Status (if table exists)
            migrationBuilder.Sql("""
                IF OBJECT_ID('appraisal.AppraisalReviews', 'U') IS NOT NULL
                BEGIN
                    UPDATE appraisal.AppraisalReviews
                    SET Status = CASE Status
                        WHEN 'Pending'  THEN 'PENDING'
                        WHEN 'Approved' THEN 'APPROVED'
                        WHEN 'Returned' THEN 'RETURNED'
                        ELSE Status
                    END
                    WHERE Status IN ('Pending','Approved','Returned')
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE appraisal.Appraisals
                SET Status = CASE Status
                    WHEN 'PENDING'      THEN 'Pending'
                    WHEN 'ASSIGNED'     THEN 'Assigned'
                    WHEN 'IN_PROGRESS'  THEN 'InProgress'
                    WHEN 'UNDER_REVIEW' THEN 'UnderReview'
                    WHEN 'COMPLETED'    THEN 'Completed'
                    WHEN 'CANCELLED'    THEN 'Cancelled'
                    ELSE Status
                END
                WHERE Status IN ('PENDING','ASSIGNED','IN_PROGRESS','UNDER_REVIEW','COMPLETED','CANCELLED')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.Appraisals
                SET SLAStatus = CASE SLAStatus
                    WHEN 'ON_TRACK' THEN 'OnTrack'
                    WHEN 'AT_RISK'  THEN 'AtRisk'
                    WHEN 'BREACHED' THEN 'Breached'
                    ELSE SLAStatus
                END
                WHERE SLAStatus IN ('ON_TRACK','AT_RISK','BREACHED')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentStatus = CASE AssignmentStatus
                    WHEN 'PENDING'    THEN 'Pending'
                    WHEN 'ASSIGNED'   THEN 'Assigned'
                    WHEN 'IN_PROGRESS' THEN 'InProgress'
                    WHEN 'COMPLETED'  THEN 'Completed'
                    WHEN 'REJECTED'   THEN 'Rejected'
                    WHEN 'CANCELLED'  THEN 'Cancelled'
                    ELSE AssignmentStatus
                END
                WHERE AssignmentStatus IN ('PENDING','ASSIGNED','IN_PROGRESS','COMPLETED','REJECTED','CANCELLED')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentType = CASE AssignmentType
                    WHEN 'INTERNAL' THEN 'Internal'
                    WHEN 'EXTERNAL' THEN 'External'
                    ELSE AssignmentType
                END
                WHERE AssignmentType IN ('INTERNAL','EXTERNAL')
                """);

            migrationBuilder.Sql("""
                UPDATE appraisal.AppraisalAssignments
                SET AssignmentMethod = CASE AssignmentMethod
                    WHEN 'MANUAL'       THEN 'Manual'
                    WHEN 'AUTO_RULE'    THEN 'AutoRule'
                    WHEN 'ROUND_ROBIN'  THEN 'RoundRobin'
                    WHEN 'QUOTATION'    THEN 'Quotation'
                    ELSE AssignmentMethod
                END
                WHERE AssignmentMethod IN ('MANUAL','AUTO_RULE','ROUND_ROBIN','QUOTATION')
                """);
        }
    }
}
