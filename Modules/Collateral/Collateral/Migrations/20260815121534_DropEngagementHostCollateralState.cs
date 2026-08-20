using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class DropEngagementHostCollateralState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Carry the data up before the source is destroyed ────────────────────────────────
            //
            // Hand-added, and deliberately NOT a separate maintenance script. The DropColumn calls
            // below are irreversible: run them without this and every AS400 collateral id the bank
            // has is gone, with the regulatory export quietly reporting the remaining collateral as
            // unlinked. A step that can be forgotten must not stand between a copy and a delete —
            // and in production the DBA applies a generated SQL bundle in which the two migrations
            // sit back to back with nowhere to insert one. Inside Up() it shares the migration's
            // transaction, so it cannot be skipped or half-applied.
            //
            // WHICH ENGAGEMENT WINS — the latest one that ACTUALLY CARRIES AN ID, which is not the
            // same as the latest engagement. Construction inspections, annual revaluations and
            // appeals involve no drawdown, so AS400 never mints an id for them; such an appraisal
            // becomes the newest engagement with a NULL id. Taking the newest outright would copy
            // that NULL onto the master and erase an id the bank still holds collateral against.
            // So: filter to rows carrying an id FIRST, then order.
            //
            // RecordDate leads the ordering because it is AS400's own event date, whereas
            // AppraisalDate only says when we valued the property. NULLs sort last under DESC, so a
            // dated row always outranks an undated one.
            // QUOTED_IDENTIFIER/ANSI_NULLS are set explicitly because CollateralMasters carries a
            // filtered index: any UPDATE against it fails with error 1934 unless both are ON. The app
            // and `dotnet ef` reach SQL Server through ADO.NET, which turns them on by default, so this
            // only matters in production — where the DBA applies a generated SQL bundle through sqlcmd,
            // which does not.
            migrationBuilder.Sql("""
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_NULLS ON;

                WITH Ranked AS (
                    SELECT
                        e.CollateralMasterId,
                        e.HostCollateralId,
                        e.RecordIndicator,
                        e.RecordDate,
                        ROW_NUMBER() OVER (
                            PARTITION BY e.CollateralMasterId
                            ORDER BY     e.RecordDate DESC, e.AppraisalDate DESC,
                                         e.CreatedAt  DESC, e.Id           DESC
                        ) AS rn
                    FROM collateral.CollateralEngagements e
                    WHERE e.HostCollateralId IS NOT NULL
                )
                UPDATE m
                SET m.HostCollateralId = r.HostCollateralId,
                    m.IsRedeemed       = CASE WHEN r.RecordIndicator = 'R' THEN 1 ELSE 0 END,
                    m.RedeemedDate     = CASE WHEN r.RecordIndicator = 'R' THEN r.RecordDate END
                FROM collateral.CollateralMasters m
                JOIN Ranked r ON r.CollateralMasterId = m.Id AND r.rn = 1
                WHERE m.HostCollateralId IS NULL;
                """);

            // Aliases are separate CollateralMasters rows (IsMaster = 0) standing for the other
            // titles in the same physical group. They hold no engagements, so the copy above cannot
            // reach them — yet a redemption releases every title at once, and leaving them unflagged
            // keeps reporting released titles to the regulator as still held.
            //
            // Only the flags propagate. AS400 issued one id for the whole group, so copying it down
            // would make the same id appear on several rows and break any lookup by it.
            migrationBuilder.Sql("""
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_NULLS ON;

                UPDATE a
                SET a.IsRedeemed   = p.IsRedeemed,
                    a.RedeemedDate = p.RedeemedDate
                FROM collateral.CollateralMasters a
                JOIN collateral.CollateralMasters p ON p.Id = a.ParentMasterId
                WHERE a.IsMaster   = 0
                  AND p.IsRedeemed = 1
                  AND a.IsRedeemed = 0;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CollateralEngagements_HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "RecordDate",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "RecordIndicator",
                schema: "collateral",
                table: "CollateralEngagements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecordDate",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordIndicator",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagements_HostCollateralId",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "HostCollateralId",
                filter: "[HostCollateralId] IS NOT NULL");

            // The columns come back empty. Down() restores the SHAPE, not the data: the master holds
            // one current state per collateral and cannot say which appraisal each value belonged
            // to, so spreading it back across engagements would invent history. Re-run
            // HostCollateralIdBackfillJob or wait for the next HOST_COLLATERAL_LINK file instead.
        }
    }
}
