using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddCasAs400InterfaceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundServiceLease",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeasedUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundServiceLease", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationEventOutbox",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEventOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingCollateralResults",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HostCollateralId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCollateralResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReappraisalCandidates",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceFileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CollateralId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    SurveyNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CollateralCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CollateralCategory = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CollateralName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CollateralAddress = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CifNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    CifName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AoCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AoName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TitleNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InternalExternal = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    BusinessSize = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    BusinessSizeDesc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MortgageAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    PastDueDay = table.Column<int>(type: "int", nullable: true),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    FacilityCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FacilitySequence = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    CpNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CarCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: true),
                    FlagLessAge4Y = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    FlagGreaterAge4Y = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    CountAgeingDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CollateralDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExternalValuerName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    InternalValuerName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SllOver100M = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    SllDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    IBGRetail = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Group = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    EffectiveDateAppraisal = table.Column<DateOnly>(type: "date", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReappraisalCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Cleanup",
                schema: "collateral",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Correlation",
                schema: "collateral",
                table: "IntegrationEventOutbox",
                columns: new[] { "CorrelationId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_DeadLetter",
                schema: "collateral",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Polling",
                schema: "collateral",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingCollateralResults_SentAt",
                schema: "collateral",
                table: "PendingCollateralResults",
                column: "SentAt",
                filter: "[SentAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PendingCollateralResults_Appraisal",
                schema: "collateral",
                table: "PendingCollateralResults",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_FileDate_CollateralId_SurveyNumber",
                schema: "collateral",
                table: "ReappraisalCandidates",
                columns: new[] { "SourceFileDate", "CollateralId", "SurveyNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_ReviewDate",
                schema: "collateral",
                table: "ReappraisalCandidates",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReappraisalCandidate_Status_Pending",
                schema: "collateral",
                table: "ReappraisalCandidates",
                column: "Status",
                filter: "[Status] = 'Pending'");

            // ── GeoPoint persisted computed column + spatial index (raw SQL — not model-mapped) ──
            // NULL-safe: geography::Point() called only when both coords are non-NULL.
            migrationBuilder.Sql("""
                ALTER TABLE collateral.ReappraisalCandidates
                    ADD GeoPoint AS
                        CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL
                             THEN geography::Point(CAST(Latitude AS float), CAST(Longitude AS float), 4326)
                             ELSE NULL
                        END PERSISTED;
                """);

            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX IX_ReappraisalCandidates_GeoPoint
                    ON collateral.ReappraisalCandidates(GeoPoint);
                """);

            // ── Copy existing data from request.ReappraisalCandidates (the vertical moved here). ──
            // Guarded so it no-ops if the source is already gone. GeoPoint is computed — excluded.
            migrationBuilder.Sql("""
                IF OBJECT_ID('request.ReappraisalCandidates', 'U') IS NOT NULL
                INSERT INTO collateral.ReappraisalCandidates (
                    Id, SourceFileName, SourceFileDate, EffectiveDate, IngestedAt, RowHash,
                    Status, ReviewType, ReviewDate, CollateralId, SurveyNumber,
                    CollateralCode, CollateralCategory, CollateralName, CollateralAddress,
                    CifNumber, CifName, AoCode, AoName, TitleNumber,
                    CurrentValue, ValuationDate, InternalExternal, BusinessSize, BusinessSizeDesc,
                    MortgageAmount, PastDueDay, ApplicationNumber, FacilityCode, FacilitySequence,
                    CpNumber, CarCode, FacilityLimit, FlagLessAge4Y, FlagGreaterAge4Y,
                    CountAgeingDate, CollateralDescription, ExternalValuerName, InternalValuerName,
                    SllOver100M, SllDescription, Stage, IBGRetail, [Group],
                    EffectiveDateAppraisal, Latitude, Longitude
                )
                SELECT
                    Id, SourceFileName, SourceFileDate, EffectiveDate, IngestedAt, RowHash,
                    Status, ReviewType, ReviewDate, CollateralId, SurveyNumber,
                    CollateralCode, CollateralCategory, CollateralName, CollateralAddress,
                    CifNumber, CifName, AoCode, AoName, TitleNumber,
                    CurrentValue, ValuationDate, InternalExternal, BusinessSize, BusinessSizeDesc,
                    MortgageAmount, PastDueDay, ApplicationNumber, FacilityCode, FacilitySequence,
                    CpNumber, CarCode, FacilityLimit, FlagLessAge4Y, FlagGreaterAge4Y,
                    CountAgeingDate, CollateralDescription, ExternalValuerName, InternalValuerName,
                    SllOver100M, SllDescription, Stage, IBGRetail, [Group],
                    EffectiveDateAppraisal, Latitude, Longitude
                FROM request.ReappraisalCandidates;
                """);

            // ── Drop the source table now that data is copied. Done HERE (Collateral context),
            // not in the Request module, so copy-then-drop order is guaranteed: UseRequestModule
            // runs before UseCollateralModule at startup, so a Request-side drop would run first
            // and the copy above would lose its source. Guarded with IF EXISTS for re-run safety.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_ReappraisalCandidates_GeoPoint
                    ON request.ReappraisalCandidates;
                """);
            migrationBuilder.Sql("DROP TABLE IF EXISTS request.ReappraisalCandidates;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundServiceLease",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "IntegrationEventOutbox",
                schema: "collateral");

            migrationBuilder.DropTable(
                name: "PendingCollateralResults",
                schema: "collateral");

            // Drop the spatial index on the computed column first (defensive; DROP TABLE would
            // cascade, but keep this explicit to mirror the create order).
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_ReappraisalCandidates_GeoPoint
                    ON collateral.ReappraisalCandidates;
                """);

            migrationBuilder.DropTable(
                name: "ReappraisalCandidates",
                schema: "collateral");
        }
    }
}
