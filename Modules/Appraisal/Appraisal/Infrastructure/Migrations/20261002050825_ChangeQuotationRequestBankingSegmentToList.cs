using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeQuotationRequestBankingSegmentToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Widen first, still nullable — so the JSON text written in step 2 always fits,
            // and so the UPDATE below isn't blocked by a NOT NULL constraint on rows it hasn't
            // reached yet.
            migrationBuilder.AlterColumn<string>(
                name: "BankingSegment",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            // 2. Backfill every existing row from its REAL current appraisal set — not just the
            // old single value reformatted. CreateQuotation-origin rows never set that column at
            // all (always null), and StartQuotationFromTask-origin rows had it frozen at creation
            // time even if appraisals were added/removed since. Recomputing from the actual join
            // gives a column that's correct the moment this migration finishes, not just
            // differently-shaped. Quotations with no appraisals, or whose appraisals all have a
            // null/blank segment, get '[]'.
            migrationBuilder.Sql("""
                ;WITH SegmentSets AS (
                    SELECT
                        qra.QuotationRequestId,
                        (
                            SELECT '[' + STRING_AGG('"' + REPLACE(seg.Segment, '"', '\"') + '"', ',') + ']'
                            FROM (
                                SELECT DISTINCT a2.BankingSegment AS Segment
                                FROM appraisal.QuotationRequestAppraisals qra2
                                JOIN appraisal.Appraisals a2 ON a2.Id = qra2.AppraisalId
                                WHERE qra2.QuotationRequestId = qra.QuotationRequestId
                                  AND a2.BankingSegment IS NOT NULL
                                  AND LTRIM(RTRIM(a2.BankingSegment)) <> ''
                            ) seg
                        ) AS SegmentJson
                    FROM appraisal.QuotationRequestAppraisals qra
                    GROUP BY qra.QuotationRequestId
                )
                UPDATE q
                SET q.BankingSegment = COALESCE(s.SegmentJson, '[]')
                FROM appraisal.QuotationRequests q
                LEFT JOIN SegmentSets s ON s.QuotationRequestId = q.Id;
                """);

            // 3. Every row now holds a valid JSON array — safe to constrain.
            migrationBuilder.AlterColumn<string>(
                name: "BankingSegment",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValueSql: "'[]'",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BankingSegment",
                schema: "appraisal",
                table: "QuotationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValueSql: "'[]'");
        }
    }
}
