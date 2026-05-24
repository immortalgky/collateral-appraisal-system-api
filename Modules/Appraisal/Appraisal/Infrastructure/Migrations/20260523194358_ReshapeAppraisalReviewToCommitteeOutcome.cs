using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeAppraisalReviewToCommitteeOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive: the old schema allowed multiple review rows per appraisal. Collapse any
            // duplicates (keep the latest by Id) so the UNIQUE index below cannot fail. The table
            // is historically unused so this is a no-op in practice, but guards fresh deploys.
            migrationBuilder.Sql(
                """
                DELETE r
                FROM appraisal.AppraisalReviews r
                WHERE r.Id <> (SELECT MAX(r2.Id) FROM appraisal.AppraisalReviews r2
                               WHERE r2.AppraisalId = r.AppraisalId);
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppraisalReviews_AppraisalId",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalReviews_AssignedTo",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalReviews_TeamId",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ApprovalTier",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "AssignedBy",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "MeetingDate",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "MeetingReference",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ReviewComments",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ReviewLevel",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ReviewSequence",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "TeamId",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "TeamName",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_AppraisalId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "AppraisalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppraisalReviews_AppraisalId",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalTier",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedBy",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTo",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MeetingDate",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingReference",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComments",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewLevel",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReviewSequence",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_AppraisalId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_AssignedTo",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_TeamId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "TeamId");
        }
    }
}
