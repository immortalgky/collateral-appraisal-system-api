using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeQuotationRequestedByToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old name column first
            migrationBuilder.DropColumn(
                name: "RequestedByName",
                schema: "appraisal",
                table: "QuotationRequests");

            // Drop the Guid column and re-add as nvarchar(50).
            // AlterColumn cannot CONVERT uniqueidentifier -> nvarchar on SQL Server;
            // existing rows hold Guid values that would be nonsense as usernames anyway
            // (dev-DB churn accepted).
            migrationBuilder.DropColumn(
                name: "RequestedBy",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Recreate the view so it picks up the new string column immediately
            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW appraisal.vw_QuotationList AS
                SELECT
                    q.Id,
                    q.QuotationNumber,
                    q.RequestDate,
                    q.DueDate,
                    q.Status,
                    q.RequestedBy,
                    q.TotalAppraisals,
                    q.TotalCompaniesInvited,
                    q.TotalQuotationsReceived,
                    q.RmUserId
                    -- v2: AppraisalId column dropped; use QuotationRequestAppraisals join table for filtering.
                    -- TotalAppraisals reflects the join table row count (denormalised counter updated by domain methods).
                    -- v4: FinalizedAckedAt dropped (AckFinalizedQuotation workflow task removed).
                FROM appraisal.QuotationRequests q
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the string column and restore the Guid column + name column
            migrationBuilder.DropColumn(
                name: "RequestedBy",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedBy",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RequestedByName",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Restore prior view DDL (RequestedByName instead of RequestedBy)
            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW appraisal.vw_QuotationList AS
                SELECT
                    q.Id,
                    q.QuotationNumber,
                    q.RequestDate,
                    q.DueDate,
                    q.Status,
                    q.RequestedByName,
                    q.TotalAppraisals,
                    q.TotalCompaniesInvited,
                    q.TotalQuotationsReceived,
                    q.RmUserId
                FROM appraisal.QuotationRequests q
                """);
        }
    }
}
