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
    /// <b>Why this migration contains data changes.</b> Narrowing the key makes previously-distinct rows
    /// collide, so the new unique index cannot be created until they are merged. The merge is not seed
    /// data; it is a prerequisite of the schema change and must run between dropping the old index and
    /// creating the new one, which no separate DbUp script can do (DbUp runs after all EF migrations).
    ///
    /// <b>The merge is reversible.</b> Losing masters are soft-deleted (IsDeleted = 1), never removed, so
    /// every row survives and the existing Restore admin path can bring one back. Their children are
    /// repointed to the winner first, so nothing is orphaned, and each merge is recorded in
    /// CollateralMasterAuditLogs.
    ///
    /// Winner per group: an IsMaster row over an alias, then the oldest CreatedAt, then the lowest Id —
    /// deterministic, so every environment picks the same winner.
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

            migrationBuilder.Sql(MergeCollidingLandMastersSql);

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

        // Repoints every child of a losing master onto the winner, then soft-deletes the loser.
        // Audit logs are deliberately NOT repointed: they are the history of the row they were written
        // against, and moving them would rewrite the past. The soft-deleted master still resolves them.
        private const string MergeCollidingLandMastersSql = """
            SET NOCOUNT ON;

            WITH Ranked AS (
                SELECT ld.CollateralMasterId AS MasterId,
                       ROW_NUMBER() OVER (
                           PARTITION BY ld.Province, ld.District, ld.SubDistrict, ld.TitleNumber
                           ORDER BY CASE WHEN cm.IsMaster = 1 THEN 0 ELSE 1 END, cm.CreatedAt, ld.CollateralMasterId
                       ) AS rn,
                       FIRST_VALUE(ld.CollateralMasterId) OVER (
                           PARTITION BY ld.Province, ld.District, ld.SubDistrict, ld.TitleNumber
                           ORDER BY CASE WHEN cm.IsMaster = 1 THEN 0 ELSE 1 END, cm.CreatedAt, ld.CollateralMasterId
                       ) AS WinnerId
                FROM collateral.LandDetails ld
                JOIN collateral.CollateralMasters cm ON cm.Id = ld.CollateralMasterId
                WHERE ld.IsDeleted = 0
                  AND cm.IsDeleted = 0
            )
            SELECT MasterId AS LoserId, WinnerId
            INTO #LandDedupMerge
            FROM Ranked
            WHERE rn > 1;

            -- Appraisal history. CollateralEngagements is UNIQUE on AppraisalId only, so repointing
            -- the master can never collide.
            UPDATE e SET e.CollateralMasterId = m.WinnerId
            FROM collateral.CollateralEngagements e
            JOIN #LandDedupMerge m ON m.LoserId = e.CollateralMasterId;

            -- Alias rows that hung off a loser now hang off the winner.
            UPDATE cm SET cm.ParentMasterId = m.WinnerId
            FROM collateral.CollateralMasters cm
            JOIN #LandDedupMerge m ON m.LoserId = cm.ParentMasterId;

            UPDATE d SET d.CollateralMasterId = m.WinnerId
            FROM collateral.CollateralDocuments d
            JOIN #LandDedupMerge m ON m.LoserId = d.CollateralMasterId;

            -- A leasehold's dedup key includes the land it sits on, so it has to follow the winner.
            UPDATE lh SET lh.UnderlyingMasterId = m.WinnerId
            FROM collateral.LeaseholdDetails lh
            JOIN #LandDedupMerge m ON m.LoserId = lh.UnderlyingMasterId;

            INSERT INTO collateral.CollateralMasterAuditLogs
                (Id, CollateralMasterId, Action, ChangedFields, Reason, ChangedBy, ChangedAt)
            SELECT NEWID(), m.LoserId, 'Delete',
                   CONCAT('{"MergedIntoMasterId":"', CONVERT(nvarchar(36), m.WinnerId), '"}'),
                   'Merged by NarrowLandDedupKeyToFourColumns: the land dedup key was narrowed to '
                     + '(Province, District, SubDistrict, TitleNumber) and this row now shares a key with the winner.',
                   'system:migration', SYSDATETIME()
            FROM #LandDedupMerge m;

            -- Soft delete, never DELETE. LandDetails.IsDeleted is synced from the master and is what the
            -- filtered unique index reads, so both rows must be flagged.
            UPDATE ld SET ld.IsDeleted = 1
            FROM collateral.LandDetails ld
            JOIN #LandDedupMerge m ON m.LoserId = ld.CollateralMasterId;

            UPDATE cm SET cm.IsDeleted = 1
            FROM collateral.CollateralMasters cm
            JOIN #LandDedupMerge m ON m.LoserId = cm.Id;

            DROP TABLE #LandDedupMerge;
            """;
    }
}
