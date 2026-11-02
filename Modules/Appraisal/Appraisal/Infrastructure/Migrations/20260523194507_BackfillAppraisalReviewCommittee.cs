using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <summary>
    /// One-time backfill (SQL since removed, so Up is a no-op): created the committee-approval outcome row
    /// in appraisal.AppraisalReviews for every appraisal approved before the write-path existed.
    /// </summary>
    public partial class BackfillAppraisalReviewCommittee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-time data backfill; not reversed.
        }
    }
}
