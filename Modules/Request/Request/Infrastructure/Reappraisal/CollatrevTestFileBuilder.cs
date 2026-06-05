using System.Globalization;
using Dapper;
using Microsoft.Extensions.Options;
using Shared.Reappraisal;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// DEV-ONLY generator that builds a realistic AS400 COLLATREV file from real completed appraisals.
///
/// Why this exists: the legacy Python script emits hardcoded synthetic rows whose SurveyNumbers match
/// no real appraisal, so the ingestion job's SurveyNumber → appraisal.Appraisals.AppraisalNumber join
/// (lat/lon enrichment + prior-appraisal linkage) never fires. This generator instead queries completed
/// appraisals, fills the appraisal-derived fields from actual data, mocks the bank-only fields
/// (CIF / AO / facility — data the bank's core system would supply), and writes the file into the
/// ingestion inbox so the existing <see cref="As400ReappraisalJob"/> consumes it unchanged.
///
/// Mocked fields are derived deterministically from the row index so repeated runs over the same data
/// produce an identical file (stable RowHash → ingestion dedupe behaves predictably).
/// </summary>
public sealed class CollatrevTestFileBuilder(
    ISqlConnectionFactory connectionFactory,
    IOptions<ReappraisalOptions> options,
    CollatrevFileWriter writer,
    ILogger<CollatrevTestFileBuilder> logger)
{
    public async Task<GenerateReappraisalTestFileResult> GenerateAsync(
        int count,
        DateOnly fileDate,
        CancellationToken cancellationToken = default)
    {
        var appraisals = await QueryCompletedAppraisalsAsync(count, cancellationToken);

        var rows = new List<IReadOnlyDictionary<string, string?>>(appraisals.Count);
        var surveyNumbers = new List<string>(appraisals.Count);

        for (var i = 0; i < appraisals.Count; i++)
        {
            rows.Add(BuildRow(appraisals[i], i, fileDate));
            surveyNumbers.Add(appraisals[i].AppraisalNumber);
        }

        var dir = Path.GetFullPath(options.Value.Local.Path);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"AS400_COLLATREV_{fileDate:yyyyMMdd}.txt");

        await writer.WriteFileAsync(path, fileDate, rows, cancellationToken);

        logger.LogInformation(
            "[REAPPRAISAL-TESTGEN] Wrote {Count} rows from completed appraisals to {Path}",
            rows.Count, path);

        return new GenerateReappraisalTestFileResult(path, rows.Count, surveyNumbers);
    }

    // ── Row mapping ─────────────────────────────────────────────────────────

    private static Dictionary<string, string?> BuildRow(CompletedAppraisalRow a, int index, DateOnly fileDate)
    {
        // ReviewType / Stage cycle 1→2→3 across rows so the file exercises every review variant.
        var stage = (index % 3) + 1;
        var stageStr = stage.ToString();

        // ReviewDate = appraisal completion + the AS400 review interval (+5y; +3y for stage-3 reviews).
        // Fall back to the file date when an appraisal carries no completion date.
        var baseDate = a.CompletedAt ?? fileDate.ToDateTime(TimeOnly.MinValue);
        var reviewDate = baseDate.AddYears(stage == 3 ? 3 : 5);

        // Deterministic mock identifiers (bank-core data — not present on our appraisal).
        var collateralId = (60_000_000 + index).ToString();
        var cifNo = (69_000_000 + index).ToString();

        var (collateralCode, collateralCategory) = MapPropertyType(a.PropertyType);
        var over100M = a.AppraisedValue is > 100_000_000m;

        return new Dictionary<string, string?>
        {
            ["ReviewType"] = stageStr,
            ["ReviewDate"] = Ddmmyyyy(reviewDate),
            ["CollateralId"] = collateralId,
            ["SurveyNo"] = a.AppraisalNumber,                                  // KEY — real AppraisalNumber
            ["CollateralCode"] = collateralCode,
            ["CollateralCategory"] = collateralCategory,
            ["CollateralName"] = a.PropertyName ?? $"Appraisal {a.AppraisalNumber}",
            ["CollateralAddress"] = JoinAddress(a),
            ["CifNo"] = cifNo,
            ["CifName"] = a.OwnerName ?? $"Customer {index + 1}",
            ["AoCode"] = "A" + (index % 900 + 100),
            ["AoName"] = "Mock AO",
            ["TitleNo"] = a.TitleNumber,
            ["CurrentValue"] = Money(a.AppraisedValue),
            ["ValuationDate"] = a.ValuationDate is { } vd ? Ddmmyyyy(vd) : null,
            ["InternalExternal"] = "I",
            ["BusinessSize"] = "C",
            ["BusinessSizeDesc"] = "SME Standard",
            ["MortgageAmount"] = a.AppraisedValue is { } v ? Money(Math.Round(v * 0.8m, 2)) : null,
            ["PastDueDay"] = "0",
            ["ApplicationNo"] = cifNo,
            ["FacilityCode"] = "TL",
            ["FacilitySequence"] = "1",
            ["CpNumber"] = cifNo + "TL",
            ["CarCode"] = "A",
            ["FacilityLimit"] = Money(a.FacilityLimit),
            ["FlagLessAge4Y"] = "",
            ["FlagGreaterAge4Y"] = "Y",
            ["CountAgeingDate"] = "",
            ["CollateralDescription"] = a.PropertyName,
            ["ExternalValuerName"] = "",
            ["InternalValuerName"] = "Bank Appraiser",
            ["SllOver100M"] = over100M ? "Y" : "N",
            ["SllDescription"] = over100M ? "External appraisal >100M" : "Internal appraisal <=100M",
            ["Stage"] = stageStr,
            ["IBGRetail"] = a.BankingSegment,
            ["Group"] = stageStr,
            ["EffectiveDateAppraisal"] = a.CompletedAt is { } ca ? Ddmmyyyy(ca) : null,
        };
    }

    // Invariant culture is mandatory: on a Thai-culture host (the bank's deployment), the default
    // calendar is Buddhist, so "ddMMyyyy" would emit the year +543 and corrupt every date the parser reads.
    private static string Ddmmyyyy(DateTime d) => d.ToString("ddMMyyyy", CultureInfo.InvariantCulture);

    private static string? Money(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? JoinAddress(CompletedAppraisalRow a)
    {
        var parts = new[] { a.Street, a.Soi, a.SubDistrict, a.District, a.Province }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(" ", parts);
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>Maps our PropertyType code to a representative AS400 collateral code/category (cosmetic).</summary>
    private static (string Code, string Category) MapPropertyType(string? propertyType)
    {
        if (propertyType is null) return ("11A", "RE");
        if (propertyType.Contains("Condo", StringComparison.OrdinalIgnoreCase)) return ("12B", "RE");
        if (propertyType.Contains("Land", StringComparison.OrdinalIgnoreCase)) return ("11A", "RE");
        return ("11A", "RE");
    }

    // ── Query ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Pulls the most-recently-completed appraisals with their first property's detail (Land preferred,
    /// then Condo) and valuation. Cross-schema Dapper read — mirrors the join shape proven in
    /// <see cref="As400ReappraisalJob"/> and <c>vw_AppraisalList</c>.
    /// </summary>
    private async Task<IReadOnlyList<CompletedAppraisalRow>> QueryCompletedAppraisalsAsync(
        int count,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Count)
                   a.AppraisalNumber  AS AppraisalNumber,
                   a.CompletedAt      AS CompletedAt,
                   a.BankingSegment   AS BankingSegment,
                   a.FacilityLimit    AS FacilityLimit,
                   va.AppraisedValue  AS AppraisedValue,
                   va.ValuationDate   AS ValuationDate,
                   p.PropertyType     AS PropertyType,
                   p.PropertyName     AS PropertyName,
                   p.OwnerName        AS OwnerName,
                   p.Province         AS Province,
                   p.District         AS District,
                   p.SubDistrict      AS SubDistrict,
                   p.Street           AS Street,
                   p.Soi              AS Soi,
                   p.TitleNumber      AS TitleNumber
            FROM appraisal.Appraisals a
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
            OUTER APPLY (
                SELECT TOP 1 props.*
                FROM (
                    SELECT ap.SequenceNumber,
                           ap.PropertyType,
                           lad.PropertyName,
                           lad.OwnerName,
                           lad.Province, lad.District, lad.SubDistrict, lad.Street, lad.Soi,
                           (SELECT TOP 1 lt.TitleNumber
                            FROM appraisal.LandTitles lt
                            WHERE lt.LandAppraisalDetailId = lad.Id
                            ORDER BY lt.TitleNumber) AS TitleNumber
                    FROM appraisal.AppraisalProperties ap
                    JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
                    WHERE ap.AppraisalId = a.Id
                    UNION ALL
                    SELECT ap.SequenceNumber,
                           ap.PropertyType,
                           cd.PropertyName,
                           cd.OwnerName,
                           cd.Province, cd.District, cd.SubDistrict, cd.Street, cd.Soi,
                           cd.BuiltOnTitleNumber AS TitleNumber  -- condo's title = underlying land deed, not a unit deed
                    FROM appraisal.AppraisalProperties ap
                    JOIN appraisal.CondoAppraisalDetails cd ON cd.AppraisalPropertyId = ap.Id
                    WHERE ap.AppraisalId = a.Id
                ) props
                ORDER BY props.SequenceNumber
            ) p
            WHERE a.Status = 'Completed'
              AND a.IsDeleted = 0
              AND a.AppraisalNumber IS NOT NULL
              AND a.AppraisalNumber <> ''
            -- AppraisalNumber tiebreaker keeps row order (and thus the index-derived mock IDs +
            -- resulting RowHash) stable across reruns when CompletedAt ties or is NULL.
            ORDER BY a.CompletedAt DESC, a.AppraisalNumber
            """;

        var connection = connectionFactory.GetOpenConnection();
        var command = new CommandDefinition(sql, new { Count = count }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CompletedAppraisalRow>(command);
        return rows.ToList();
    }

    private sealed record CompletedAppraisalRow(
        string AppraisalNumber,
        DateTime? CompletedAt,
        string? BankingSegment,
        decimal? FacilityLimit,
        decimal? AppraisedValue,
        DateTime? ValuationDate,
        string? PropertyType,
        string? PropertyName,
        string? OwnerName,
        string? Province,
        string? District,
        string? SubDistrict,
        string? Street,
        string? Soi,
        string? TitleNumber);
}

/// <summary>Result of a dev test-file generation run.</summary>
public sealed record GenerateReappraisalTestFileResult(
    string FilePath,
    int RowCount,
    IReadOnlyList<string> SurveyNumbers);
