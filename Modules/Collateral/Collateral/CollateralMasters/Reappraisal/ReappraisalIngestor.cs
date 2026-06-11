using Collateral.Contracts.Reappraisal;
using Collateral.Data;
using Dapper;
using Integration.Contracts.Reappraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// Upserts reappraisal candidates from a parsed COLLATREV file into the Collateral data store.
/// Handles the existing-row lookup, Pending/Consumed/Deleted skip, RowHash dedupe,
/// ReappraisalCandidate.Create/UpdateFrom, lat/lon enrichment (cross-schema Dapper), and SaveChanges.
/// Injected IDateTimeProvider replaces the inline DateTime.Now from the original job.
/// </summary>
public class ReappraisalIngestor(
    CollateralDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReappraisalIngestor> logger) : IReappraisalIngestor
{
    public async Task IngestAsync(
        string fileName,
        DateOnly fileDate,
        ParsedReappraisalFile parsed,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.ApplicationNow;

        // Load existing rows for this SourceFileDate into a lookup for O(1) upsert.
        var existing = await dbContext.ReappraisalCandidates
            .AsNoTracking()
            .Where(c => c.SourceFileDate == fileDate)
            .ToDictionaryAsync(c => (c.CollateralId, c.SurveyNumber), cancellationToken);

        // Collect SurveyNumbers that need lat/lon enrichment (new or updated rows).
        var needsEnrichment = new List<string>();

        foreach (var detail in parsed.Details)
        {
            var key = (detail.CollateralId, detail.SurveyNumber);

            if (existing.TryGetValue(key, out var existingEntity))
            {
                // Never resurrect a candidate that staff already acted on.
                if (existingEntity.Status != ReappraisalCandidateStatus.Pending)
                    continue;

                var tracked = dbContext.ReappraisalCandidates.Attach(existingEntity);

                if (existingEntity.RowHash == detail.RowHash)
                {
                    tracked.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    continue;
                }

                existingEntity.UpdateFrom(
                    detail.RowHash,
                    parsed.EffectiveDate,
                    detail.ReviewType,
                    detail.ReviewDate,
                    detail.CollateralCode,
                    detail.CollateralCategory,
                    detail.CollateralName,
                    detail.CollateralAddress,
                    detail.CifName,
                    detail.AoCode,
                    detail.AoName,
                    detail.TitleNumber,
                    detail.CurrentValue,
                    detail.ValuationDate,
                    detail.InternalExternal,
                    detail.BusinessSize,
                    detail.BusinessSizeDesc,
                    detail.MortgageAmount,
                    detail.PastDueDay,
                    detail.ApplicationNumber,
                    detail.FacilityCode,
                    detail.FacilitySequence,
                    detail.CpNumber,
                    detail.CarCode,
                    detail.FacilityLimit,
                    detail.FlagLessAge4Y,
                    detail.FlagGreaterAge4Y,
                    detail.CountAgeingDate,
                    detail.CollateralDescription,
                    detail.ExternalValuerName,
                    detail.InternalValuerName,
                    detail.SllOver100M,
                    detail.SllDescription,
                    detail.Stage,
                    detail.IBGRetail,
                    detail.Group,
                    detail.EffectiveDateAppraisal);

                needsEnrichment.Add(detail.SurveyNumber);
            }
            else
            {
                var candidate = ReappraisalCandidate.Create(
                    fileName,
                    fileDate,
                    parsed.EffectiveDate,
                    now,
                    detail.RowHash,
                    detail.ReviewType,
                    detail.ReviewDate,
                    detail.CollateralId,
                    detail.SurveyNumber,
                    detail.CollateralCode,
                    detail.CollateralCategory,
                    detail.CollateralName,
                    detail.CollateralAddress,
                    detail.CifNumber,
                    detail.CifName,
                    detail.AoCode,
                    detail.AoName,
                    detail.TitleNumber,
                    detail.CurrentValue,
                    detail.ValuationDate,
                    detail.InternalExternal,
                    detail.BusinessSize,
                    detail.BusinessSizeDesc,
                    detail.MortgageAmount,
                    detail.PastDueDay,
                    detail.ApplicationNumber,
                    detail.FacilityCode,
                    detail.FacilitySequence,
                    detail.CpNumber,
                    detail.CarCode,
                    detail.FacilityLimit,
                    detail.FlagLessAge4Y,
                    detail.FlagGreaterAge4Y,
                    detail.CountAgeingDate,
                    detail.CollateralDescription,
                    detail.ExternalValuerName,
                    detail.InternalValuerName,
                    detail.SllOver100M,
                    detail.SllDescription,
                    detail.Stage,
                    detail.IBGRetail,
                    detail.Group,
                    detail.EffectiveDateAppraisal);

                await dbContext.ReappraisalCandidates.AddAsync(candidate, cancellationToken);
                needsEnrichment.Add(detail.SurveyNumber);
            }
        }

        // Lat/lon enrichment — cross-schema Dapper read-only.
        if (needsEnrichment.Count > 0)
        {
            var coords = await FetchCoordinatesAsync(needsEnrichment, cancellationToken);

            var pendingEntries = dbContext.ChangeTracker
                .Entries<ReappraisalCandidate>()
                .Where(e => e.State != Microsoft.EntityFrameworkCore.EntityState.Detached);

            foreach (var entry in pendingEntries)
            {
                var surveyNumber = entry.Entity.SurveyNumber;
                if (coords.TryGetValue(surveyNumber, out var coord))
                    entry.Entity.SetCoordinates(coord.Latitude, coord.Longitude);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[ReappraisalIngestor] Ingested {Count} records from {File}",
            parsed.Details.Count, fileName);
    }

    /// <summary>
    /// Cross-schema Dapper query: looks up the best available coordinates for each SurveyNumber
    /// (= AppraisalNumber). Checks both Land and Condo detail tables (union).
    /// </summary>
    private async Task<Dictionary<string, (decimal Latitude, decimal Longitude)>> FetchCoordinatesAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken cancellationToken)
    {
        if (surveyNumbers.Count == 0)
            return new Dictionary<string, (decimal, decimal)>();

        const string sql = """
            SELECT a.AppraisalNumber AS SurveyNumber,
                   CAST(d.Latitude AS decimal(10,7)) AS Latitude,
                   CAST(d.Longitude AS decimal(10,7)) AS Longitude
            FROM appraisal.Appraisals a
            CROSS APPLY (
                SELECT TOP 1 u.Latitude, u.Longitude
                FROM (
                    SELECT TOP 1 ld.Latitude, ld.Longitude, 1 AS Pref
                    FROM appraisal.LandAppraisalDetails ld
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = ld.AppraisalPropertyId
                    WHERE ap.AppraisalId = a.Id AND ld.Latitude IS NOT NULL AND ld.Longitude IS NOT NULL
                    UNION ALL
                    SELECT TOP 1 cd.Latitude, cd.Longitude, 2 AS Pref
                    FROM appraisal.CondoAppraisalDetails cd
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = cd.AppraisalPropertyId
                    WHERE ap.AppraisalId = a.Id AND cd.Latitude IS NOT NULL AND cd.Longitude IS NOT NULL
                ) u
                ORDER BY u.Pref
            ) d
            WHERE a.AppraisalNumber IN @SurveyNumbers
            """;

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<CoordinateRow>(sql, new { SurveyNumbers = surveyNumbers });

        return rows
            .GroupBy(r => r.SurveyNumber)
            .ToDictionary(g => g.Key, g => (g.First().Latitude, g.First().Longitude));
    }

    private sealed record CoordinateRow(string SurveyNumber, decimal Latitude, decimal Longitude);
}
