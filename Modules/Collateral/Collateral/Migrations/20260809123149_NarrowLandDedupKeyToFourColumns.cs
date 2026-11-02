using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <summary>
    /// Narrows the land dedup key from eight columns to four:
    /// Province, District, SubDistrict, TitleNumber.
    ///
    /// TitleType, SurveyNumber, LandParcelNumber and Rawang were splitting one physical parcel across
    /// several masters whenever an appraiser recorded them differently — Rawang alone is blank on about
    /// 99.8% of title rows, so filling it in on a later appraisal minted a second master for land that
    /// already had one.
    ///
    /// Narrowing the key made previously-distinct rows collide, so the colliding masters were merged
    /// (losers soft-deleted, children repointed, audit-logged) between dropping the old index and
    /// creating the new one. That merge SQL was removed on 2026-10-28: every environment had already
    /// applied it, and a fresh database has no rows to merge.
    /// </summary>
    public partial class NarrowLandDedupKeyToFourColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "Province", "District", "SubDistrict", "TitleNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails");

            migrationBuilder.CreateIndex(
                name: "UX_LandDetails_DedupKey_Active",
                schema: "collateral",
                table: "LandDetails",
                columns: new[] { "Province", "District", "SubDistrict", "TitleType", "TitleNumber", "SurveyNumber", "LandParcelNumber", "Rawang" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Down does NOT un-merge. Soft-deleted losers stay soft-deleted, which is safe: the old
            // eight-column key is stricter, so the wider index is satisfied by any subset of rows.
            // Recovering a merged master is an operator decision — use the Restore admin endpoint.
        }
    }
}
