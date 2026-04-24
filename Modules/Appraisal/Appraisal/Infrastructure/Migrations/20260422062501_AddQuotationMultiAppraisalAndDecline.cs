using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationMultiAppraisalAndDecline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalId",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAckedAt",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeclinedAt",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclinedBy",
                schema: "appraisal",
                table: "CompanyQuotations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuotationRequestAppraisals",
                schema: "appraisal",
                columns: table => new
                {
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationRequestAppraisals", x => new { x.QuotationRequestId, x.AppraisalId });
                    table.ForeignKey(
                        name: "FK_QuotationRequestAppraisals_QuotationRequests_QuotationRequestId",
                        column: x => x.QuotationRequestId,
                        principalSchema: "appraisal",
                        principalTable: "QuotationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequestAppraisals_AppraisalId",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationRequestAppraisals_QuotationRequestId",
                schema: "appraisal",
                table: "QuotationRequestAppraisals",
                column: "QuotationRequestId");

            // ── Update CK_CompanyQuotations_Status to include 'Declined' ─────────
            migrationBuilder.Sql("ALTER TABLE [appraisal].[CompanyQuotations] DROP CONSTRAINT IF EXISTS [CK_CompanyQuotations_Status];");
            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[CompanyQuotations]
                ADD CONSTRAINT [CK_CompanyQuotations_Status]
                CHECK ([Status] IN (
                    'Submitted', 'UnderReview', 'Tentative',
                    'Negotiating', 'Accepted', 'Rejected', 'Withdrawn', 'Declined'
                ));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationRequestAppraisals",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "FinalizedAckedAt",
                schema: "appraisal",
                table: "QuotationRequests");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "DeclinedAt",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.DropColumn(
                name: "DeclinedBy",
                schema: "appraisal",
                table: "CompanyQuotations");

            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalId",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "uniqueidentifier",
                nullable: true);

            // Restore CK without 'Declined'
            migrationBuilder.Sql("ALTER TABLE [appraisal].[CompanyQuotations] DROP CONSTRAINT IF EXISTS [CK_CompanyQuotations_Status];");
            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[CompanyQuotations]
                ADD CONSTRAINT [CK_CompanyQuotations_Status]
                CHECK ([Status] IN (
                    'Submitted', 'UnderReview', 'Tentative',
                    'Negotiating', 'Accepted', 'Rejected', 'Withdrawn'
                ));
                """);
        }
    }
}
