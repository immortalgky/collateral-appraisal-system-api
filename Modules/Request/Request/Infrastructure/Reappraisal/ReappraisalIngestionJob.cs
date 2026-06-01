using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Reappraisal;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// Hangfire recurring job that ingests the monthly AS400 COLLATREV file.
///
/// Algorithm per file:
///   1. List files from IReappraisalFileSource.
///   2. Download + parse (UTF-8 fixed-width, 660-char Detail records; H/D/T).
///   3. Upsert each detail row into request.ReappraisalCandidates (keyed on SourceFileDate +
///      CollateralId + SurveyNumber):
///      - Existing Consumed/Deleted row → skip (staff already acted; never resurrect).
///      - Existing Pending row, RowHash unchanged → skip (RowHash dedupe).
///      - Existing Pending row, RowHash changed → UpdateFrom() (refresh fields).
///      - New row → Create() + Add.
///   4. Lat/lon enrichment: for each new/updated row, join SurveyNumber → appraisal.Appraisals.AppraisalNumber
///      to get Land/CondoAppraisalDetails coords. Dapper read-only query — no EF navigation across schemas.
///   5. SaveChangesAsync (single transaction wraps entire file's upserts).
///   6. Archive file via IReappraisalFileSource.ArchiveAsync — so a successfully-ingested file leaves the
///      inbound folder and is not reprocessed.
///   7. Per-file try/catch so one bad file does not block others.
///
/// Idempotency: archiving removes a processed file from the inbound folder; if a same-named file
/// reappears, RowHash dedupe makes unchanged rows a no-op and the Consumed/Deleted skip above protects
/// staff actions. Multi-server safety relies on Hangfire single-execution of the recurring job.
/// </summary>
public class ReappraisalIngestionJob(
    RequestDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    IReappraisalFileSource fileSource,
    CollatrevFileParser parser,
    ILogger<ReappraisalIngestionJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[REAPPRAISAL-INGEST] Starting ingestion run");

        var files = await fileSource.ListFilesAsync(cancellationToken);

        if (files.Count == 0)
        {
            logger.LogInformation("[REAPPRAISAL-INGEST] No files found — nothing to do");
            return;
        }

        foreach (var file in files)
        {
            try
            {
                await IngestFileAsync(file, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REAPPRAISAL-INGEST] Failed to ingest {File}", file.FileName);
                // Continue with next file.
            }
        }

        logger.LogInformation("[REAPPRAISAL-INGEST] Ingestion run complete");
    }

    private async Task IngestFileAsync(ReappraisalFileInfo file, CancellationToken cancellationToken)
    {
        logger.LogInformation("[REAPPRAISAL-INGEST] Processing {File}", file.FileName);

        var fileDate = CollatrevFileParser.ParseFilenameDate(file.FileName);
        if (fileDate is null)
        {
            logger.LogWarning("[REAPPRAISAL-INGEST] Cannot parse date from filename '{File}', skipping", file.FileName);
            return;
        }

        // Parse file content.
        var parsedFile = await ParseFileAsync(file, cancellationToken);

        var now = DateTime.UtcNow;

        // Load existing rows for this SourceFileDate into a lookup for O(1) upsert.
        var existing = await dbContext.ReappraisalCandidates
            .AsNoTracking()
            .Where(c => c.SourceFileDate == fileDate.Value)
            .ToDictionaryAsync(c => (c.CollateralId, c.SurveyNumber), cancellationToken);

        // Collect CollateralIds + SurveyNumbers that need lat/lon enrichment (new or updated rows).
        var needsEnrichment = new List<string>();

        foreach (var detail in parsedFile.Details)
        {
            var key = (detail.CollateralId, detail.SurveyNumber);

            if (existing.TryGetValue(key, out var existingEntity))
            {
                // Never resurrect a candidate that staff already acted on: a Consumed row already
                // became a Request, and a Deleted row was explicitly removed by staff. Only Pending
                // rows are refreshed from a re-ingested file.
                if (existingEntity.Status != ReappraisalCandidateStatus.Pending)
                    continue;

                // Re-attach to change-tracker for update.
                var tracked = dbContext.ReappraisalCandidates.Attach(existingEntity);

                if (existingEntity.RowHash == detail.RowHash)
                {
                    // No change — skip.
                    tracked.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    continue;
                }

                // Content changed — update.
                existingEntity.UpdateFrom(
                    detail.RowHash,
                    parsedFile.EffectiveDate,
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
                // New row.
                var candidate = ReappraisalCandidate.Create(
                    file.FileName,
                    fileDate.Value,
                    parsedFile.EffectiveDate,
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

        // Lat/lon enrichment: join SurveyNumber → appraisal.Appraisals.AppraisalNumber → detail coords.
        // Dapper read-only, cross-schema. No EF navigation across module boundaries.
        if (needsEnrichment.Count > 0)
        {
            var coords = await FetchCoordinatesAsync(needsEnrichment, cancellationToken);

            // Apply coords to tracked entities.
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

        logger.LogInformation("[REAPPRAISAL-INGEST] Ingested {Count} records from {File}",
            parsedFile.Details.Count, file.FileName);

        await fileSource.ArchiveAsync(file, cancellationToken);

        logger.LogInformation("[REAPPRAISAL-INGEST] Archived {File}", file.FileName);
    }

    private async Task<ParsedReappraisalFile> ParseFileAsync(
        ReappraisalFileInfo file,
        CancellationToken cancellationToken)
    {
        await using var stream = await fileSource.OpenReadAsync(file, cancellationToken);
        return parser.ParseStream(stream);
    }

    /// <summary>
    /// Cross-schema Dapper query: looks up the best available coordinates for each SurveyNumber
    /// (= AppraisalNumber). Checks both Land and Condo detail tables (union).
    /// Returns NULL for SurveyNumbers with no in-system appraisal match.
    /// </summary>
    private async Task<Dictionary<string, (decimal Latitude, decimal Longitude)>> FetchCoordinatesAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken cancellationToken)
    {
        if (surveyNumbers.Count == 0)
            return new Dictionary<string, (decimal, decimal)>();

        // SurveyNumbers come from the parsed file (trimmed, nvarchar(10)); passed as a Dapper IN parameter.
        // The outer APPLY takes a single coordinate per appraisal (prefer Land, then Condo) so an
        // appraisal carrying BOTH a land and a condo detail yields exactly one row — otherwise the
        // ToDictionary below would throw on a duplicate SurveyNumber key.
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

        // Defensive: GroupBy guards against any unexpected duplicate SurveyNumber.
        return rows
            .GroupBy(r => r.SurveyNumber)
            .ToDictionary(g => g.Key, g => (g.First().Latitude, g.First().Longitude));
    }

    private sealed record CoordinateRow(string SurveyNumber, decimal Latitude, decimal Longitude);
}
