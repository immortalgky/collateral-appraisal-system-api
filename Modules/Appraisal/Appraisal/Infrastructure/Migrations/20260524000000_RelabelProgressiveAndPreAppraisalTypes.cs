using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <summary>
    /// Relabel AppraisalType values:
    ///  - "ConstructionInspection" → "Progressive": the progressive-inspection flow was mis-labelled.
    ///  - block/project deals (collateral type 32/33) that defaulted to "New" → "PreAppraisal";
    ///    identified via the appraisal.Projects rows created for those appraisals.
    /// Data-only; no schema change.
    /// </summary>
    public partial class RelabelProgressiveAndPreAppraisalTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mis-labelled progressive appraisals.
            migrationBuilder.Sql(
                "UPDATE [appraisal].[Appraisals] SET AppraisalType = 'Progressive' WHERE AppraisalType = 'ConstructionInspection'");

            // Block/project deals (32/33) that defaulted to New.
            migrationBuilder.Sql(
                """
                UPDATE [appraisal].[Appraisals] SET AppraisalType = 'PreAppraisal'
                WHERE AppraisalType = 'New'
                  AND Id IN (SELECT AppraisalId FROM [appraisal].[Projects]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [appraisal].[Appraisals] SET AppraisalType = 'ConstructionInspection' WHERE AppraisalType = 'Progressive'");

            // Only revert block rows; legacy PreAppraisal (renamed from "Special") must stay.
            migrationBuilder.Sql(
                """
                UPDATE [appraisal].[Appraisals] SET AppraisalType = 'New'
                WHERE AppraisalType = 'PreAppraisal'
                  AND Id IN (SELECT AppraisalId FROM [appraisal].[Projects]);
                """);
        }
    }
}
