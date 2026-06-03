using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingMarketReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PricingAnalysis: merge PropertyGroupId / ProjectModelId into a single AnchorId ──
            // (+ AnchorRefKey for room-type keys, + HostMethodId for reference cleanup scope).
            // No FK is created on AnchorId — it is polymorphic (PropertyGroup / ProjectModel /
            // IncomeAnalysis / AppraisalProperty ids). The dropped ProjectModels cascade is
            // replaced by app-level cleanup (PricingReferenceCleanupService).
            migrationBuilder.AddColumn<Guid>(
                name: "AnchorId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnchorRefKey",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HostMethodId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill AnchorId from the two old columns before dropping them.
            migrationBuilder.Sql(
                "UPDATE appraisal.PricingAnalysis " +
                "SET AnchorId = COALESCE(PropertyGroupId, ProjectModelId) " +
                "WHERE AnchorId IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_PricingAnalysis_ProjectModels_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysis_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PricingAnalysis_SubjectXor",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_SubjectType_AnchorId_AnchorRefKey",
                schema: "appraisal",
                table: "PricingAnalysis",
                columns: new[] { "SubjectType", "AnchorId", "AnchorRefKey" },
                unique: true,
                filter: "[AnchorId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PricingAnalysis_AnchorNotNull",
                schema: "appraisal",
                table: "PricingAnalysis",
                sql: "[AnchorId] IS NOT NULL");

            // ── PricingComparativeFactors: persist the manually-entered subject value ──
            migrationBuilder.AddColumn<string>(
                name: "CollateralValue",
                schema: "appraisal",
                table: "PricingComparativeFactors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollateralValue",
                schema: "appraisal",
                table: "PricingComparativeFactors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PricingAnalysis_AnchorNotNull",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysis_SubjectType_AnchorId_AnchorRefKey",
                schema: "appraisal",
                table: "PricingAnalysis");

            // Restore the old two-column subject schema.
            migrationBuilder.AddColumn<Guid>(
                name: "PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true);

            // Reference rows (SubjectType >= 2) cannot be represented in the old 2-column schema —
            // remove them, then re-split AnchorId by SubjectType. Done while AnchorId still exists.
            migrationBuilder.Sql(
                "DELETE FROM appraisal.PricingAnalysis WHERE SubjectType >= 2;");
            migrationBuilder.Sql(
                "UPDATE appraisal.PricingAnalysis " +
                "SET PropertyGroupId = CASE WHEN SubjectType = 0 THEN AnchorId END, " +
                "    ProjectModelId  = CASE WHEN SubjectType = 1 THEN AnchorId END;");

            migrationBuilder.DropColumn(
                name: "AnchorId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "AnchorRefKey",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "HostMethodId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "PropertyGroupId",
                unique: true,
                filter: "[PropertyGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "ProjectModelId",
                unique: true,
                filter: "[ProjectModelId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PricingAnalysis_SubjectXor",
                schema: "appraisal",
                table: "PricingAnalysis",
                sql: "([PropertyGroupId] IS NOT NULL AND [ProjectModelId] IS NULL) OR ([PropertyGroupId] IS NULL AND [ProjectModelId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PricingAnalysis_ProjectModels_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "ProjectModelId",
                principalSchema: "appraisal",
                principalTable: "ProjectModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
