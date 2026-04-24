using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationIbgShortlistNegotiation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankingSegment",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RmUserId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShortlistSentByAdminId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShortlistSentToRmAt",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionsClosedAt",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskExecutionId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TentativeWinnerQuotationId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativelySelectedAt",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TentativelySelectedBy",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TentativelySelectedByRole",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalShortlisted",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "QuotationItemId",
                schema: "appraisal",
                table: "QuotationNegotiations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentNegotiatedPrice",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShortlisted",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NegotiationRounds",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalQuotedPrice",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_Status_DueDate",
                schema: "appraisal",
                table: "QuotationRequests",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequests_WorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQuotations_QuotationRequestId_IsShortlisted",
                schema: "appraisal",
                table: "CompanyQuotations",
                columns: new[] { "QuotationRequestId", "IsShortlisted" });

            // ── Status CHECK constraints ──────────────────────────────────────────
            // Enforce the full status vocabulary at the database level.
            // NOTE: 'Closed' is retained for the legacy QuotationRequest.SelectQuotation()
            // (non-IBG) path where a quotation transitions Sent -> Closed on single-shot pick.
            // The IBG flow uses 'UnderAdminReview' -> 'PendingRmSelection' -> ... -> 'Finalized' instead.
            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[QuotationRequests]
                ADD CONSTRAINT [CK_QuotationRequests_Status]
                CHECK ([Status] IN (
                    'Draft', 'Sent', 'Closed', 'Cancelled',
                    'UnderAdminReview', 'PendingRmSelection',
                    'WinnerTentative', 'Negotiating', 'Finalized'
                ));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[CompanyQuotations]
                ADD CONSTRAINT [CK_CompanyQuotations_Status]
                CHECK ([Status] IN (
                    'Submitted', 'UnderReview', 'Tentative',
                    'Negotiating', 'Accepted', 'Rejected', 'Withdrawn'
                ));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [appraisal].[CompanyQuotations] DROP CONSTRAINT IF EXISTS [CK_CompanyQuotations_Status];");
            migrationBuilder.Sql("ALTER TABLE [appraisal].[QuotationRequests] DROP CONSTRAINT IF EXISTS [CK_QuotationRequests_Status];");

            migrationBuilder.DropIndex(
                name: "IX_QuotationRequests_Status_DueDate",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropIndex(
                name: "IX_QuotationRequests_WorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropIndex(
                name: "IX_CompanyQuotations_QuotationRequestId_IsShortlisted",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "AppraisalId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "BankingSegment",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "RequestId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "RmUserId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "ShortlistSentByAdminId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "ShortlistSentToRmAt",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "SubmissionsClosedAt",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TaskExecutionId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TentativeWinnerQuotationId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TentativelySelectedAt",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TentativelySelectedBy",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TentativelySelectedByRole",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "TotalShortlisted",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "CurrentNegotiatedPrice",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "IsShortlisted",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "NegotiationRounds",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "OriginalQuotedPrice",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuotationItemId",
                schema: "appraisal",
                table: "QuotationNegotiations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
