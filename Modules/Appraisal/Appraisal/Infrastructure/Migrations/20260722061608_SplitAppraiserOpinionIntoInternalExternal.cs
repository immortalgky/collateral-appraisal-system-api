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
            // detected rename lands it all in InternalAppraiserOpinion. A corrective step that moved the
            // external-path rows into ExternalAppraiserOpinion was removed from Up().
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

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
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
