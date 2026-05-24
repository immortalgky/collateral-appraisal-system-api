using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalReviewTierAndMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalTier",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MeetingId",
                schema: "appraisal",
                table: "AppraisalReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReviews_MeetingId",
                schema: "appraisal",
                table: "AppraisalReviews",
                column: "MeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppraisalReviews_MeetingId",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "ApprovalTier",
                schema: "appraisal",
                table: "AppraisalReviews");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                schema: "appraisal",
                table: "AppraisalReviews");
        }
    }
}
