using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <summary>
    /// Records the removal of the <c>ReappraisalCandidate</c> entity from the Request model
    /// (the reappraisal vertical moved to the Collateral module).
    ///
    /// Intentionally a NO-OP for DDL: the actual <c>DROP TABLE request.ReappraisalCandidates</c>
    /// is performed by the Collateral migration <c>AddCasAs400InterfaceTables</c> immediately after
    /// it copies the data, so the copy-then-drop order holds regardless of module migration order
    /// (UseRequestModule runs before UseCollateralModule at startup). This migration exists only to
    /// keep the Request model snapshot consistent with the migration history. The EF-scaffolded
    /// DropTable/CreateTable bodies were replaced with no-ops for that reason.
    /// </summary>
    public partial class DropReappraisalCandidatesFromRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op — the table drop lives in the Collateral migration (see class summary).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — the table lifecycle is owned by the Collateral migration.
        }
    }
}
