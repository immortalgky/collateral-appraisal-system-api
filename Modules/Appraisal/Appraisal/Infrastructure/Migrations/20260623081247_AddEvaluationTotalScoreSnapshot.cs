using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationTotalScoreSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalScore",
                schema: "appraisal",
                table: "AppraisalEvaluations",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            // Backfill the snapshot for already-Completed evaluations using the config
            // weights currently in force, so each frozen score equals exactly what
            // vw_AppraisalEvaluationList shows today. From now on, weight edits will not
            // move these values. Pending rows are intentionally left NULL (scored live).
            // Segment resolution mirrors the view: NULL/blank BankingSegment → 'IBG',
            // matched case-insensitively.
            migrationBuilder.Sql(@"
UPDATE e
SET e.TotalScore = CAST(
        (ISNULL(w.W1, 0) * ISNULL(e.Criteria1Rating, 0))
      + (ISNULL(w.W2, 0) * ISNULL(e.Criteria2Rating, 0))
      + (ISNULL(w.W3, 0) * ISNULL(e.Criteria3Rating, 0))
      + (ISNULL(w.W4, 0) * ISNULL(e.Criteria4Rating, 0))
      + (ISNULL(w.W5, 0) * ISNULL(e.Criteria5Rating, 0))
    AS DECIMAL(5, 2))
FROM appraisal.AppraisalEvaluations e
     JOIN appraisal.Appraisals a ON a.Id = e.AppraisalId
     OUTER APPLY (
         SELECT
             MAX(CASE WHEN cfg.CriteriaSlot = 1 THEN cfg.Weight END) AS W1,
             MAX(CASE WHEN cfg.CriteriaSlot = 2 THEN cfg.Weight END) AS W2,
             MAX(CASE WHEN cfg.CriteriaSlot = 3 THEN cfg.Weight END) AS W3,
             MAX(CASE WHEN cfg.CriteriaSlot = 4 THEN cfg.Weight END) AS W4,
             MAX(CASE WHEN cfg.CriteriaSlot = 5 THEN cfg.Weight END) AS W5
         FROM appraisal.EvaluationCriteriaConfigs cfg
         WHERE UPPER(cfg.BankingSegment) = UPPER(ISNULL(a.BankingSegment, 'IBG'))
     ) w
WHERE e.EvaluationStatus = 'Completed'
  AND e.TotalScore IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalScore",
                schema: "appraisal",
                table: "AppraisalEvaluations");
        }
    }
}
