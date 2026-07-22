using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitAppraiserOpinionIntoInternalExternal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old polymorphic AppraiserOpinion held the *performer's* opinion — the external
            // appraiser on the external path, the internal appraiser on the internal path. EF's
            // detected rename lands it all in InternalAppraiserOpinion; the corrective below then
            // moves only the external-path rows out to ExternalAppraiserOpinion. Internal-path and
            // unresolvable rows stay in Internal (the latter kept there deliberately so the value
            // still surfaces in report Field 39, which reads InternalAppraiserOpinion).
            migrationBuilder.RenameColumn(
                name: "AppraiserOpinionType",
                schema: "appraisal",
                table: "AppraisalDecisions",
                newName: "InternalAppraiserOpinionType");

            migrationBuilder.RenameColumn(
                name: "AppraiserOpinion",
                schema: "appraisal",
                table: "AppraisalDecisions",
                newName: "InternalAppraiserOpinion");

            migrationBuilder.AddColumn<string>(
                name: "ExternalAppraiserOpinion",
                schema: "appraisal",
                table: "AppraisalDecisions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAppraiserOpinionType",
                schema: "appraisal",
                table: "AppraisalDecisions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Path-corrective backfill: external-path rows -> External opinion. Path is the latest
            // non-Rejected/Cancelled assignment, mirroring the report loader
            // (AppraisalSummaryCommonLoader RS04). AssignmentType stores AssignmentType.Code
            // ("Internal"/"External"). INNER JOIN + '= External' means internal-path AND
            // unresolvable rows are left untouched in InternalAppraiserOpinion.
            migrationBuilder.Sql("""
                ;WITH latest AS (
                    SELECT aa.AppraisalId, aa.AssignmentType,
                           ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId
                                              ORDER BY aa.AssignedAt DESC, aa.Id DESC) AS rn
                    FROM appraisal.AppraisalAssignments aa
                    WHERE aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                )
                UPDATE ad SET
                    ExternalAppraiserOpinion     = ad.InternalAppraiserOpinion,
                    ExternalAppraiserOpinionType = ad.InternalAppraiserOpinionType,
                    InternalAppraiserOpinion     = NULL,
                    InternalAppraiserOpinionType = NULL
                FROM appraisal.AppraisalDecisions ad
                JOIN latest l ON l.AppraisalId = ad.AppraisalId AND l.rn = 1
                WHERE l.AssignmentType = N'External';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the corrective first (columns still carry the new names), pulling the
            // external-path opinion back into the column that renames to AppraiserOpinion.
            migrationBuilder.Sql("""
                ;WITH latest AS (
                    SELECT aa.AppraisalId, aa.AssignmentType,
                           ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId
                                              ORDER BY aa.AssignedAt DESC, aa.Id DESC) AS rn
                    FROM appraisal.AppraisalAssignments aa
                    WHERE aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                )
                UPDATE ad SET
                    InternalAppraiserOpinion     = ad.ExternalAppraiserOpinion,
                    InternalAppraiserOpinionType = ad.ExternalAppraiserOpinionType
                FROM appraisal.AppraisalDecisions ad
                JOIN latest l ON l.AppraisalId = ad.AppraisalId AND l.rn = 1
                WHERE l.AssignmentType = N'External';
                """);

            migrationBuilder.DropColumn(
                name: "ExternalAppraiserOpinion",
                schema: "appraisal",
                table: "AppraisalDecisions");

            migrationBuilder.DropColumn(
                name: "ExternalAppraiserOpinionType",
                schema: "appraisal",
                table: "AppraisalDecisions");

            migrationBuilder.RenameColumn(
                name: "InternalAppraiserOpinionType",
                schema: "appraisal",
                table: "AppraisalDecisions",
                newName: "AppraiserOpinionType");

            migrationBuilder.RenameColumn(
                name: "InternalAppraiserOpinion",
                schema: "appraisal",
                table: "AppraisalDecisions",
                newName: "AppraiserOpinion");
        }
    }
}
