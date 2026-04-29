using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssigneeCompanyIdToCompletedTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeCompanyId",
                schema: "workflow",
                table: "CompletedTasks",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill: copy CompanyId from QuotationInvitations onto completed ext-role tasks
            // where exactly one invitation exists for the quotation (unambiguous single-company match).
            // Multi-invitation rows stay NULL — PoolTaskAccess username/group match still narrows visibility.
            migrationBuilder.Sql(@"
                UPDATE ct
                SET ct.AssigneeCompanyId = qi.CompanyId
                FROM workflow.CompletedTasks ct
                JOIN appraisal.QuotationInvitations qi
                  ON qi.QuotationRequestId = ct.CorrelationId
                WHERE ct.AssigneeCompanyId IS NULL
                  AND ct.AssignedType = '2'
                  AND ct.AssignedTo LIKE 'Ext%'
                  AND (
                      SELECT COUNT(*)
                      FROM appraisal.QuotationInvitations qi2
                      WHERE qi2.QuotationRequestId = ct.CorrelationId
                  ) = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeCompanyId",
                schema: "workflow",
                table: "CompletedTasks");
        }
    }
}
