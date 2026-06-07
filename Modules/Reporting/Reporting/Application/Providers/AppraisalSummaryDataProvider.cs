using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Unified "Appraisal Summary" report (FSD §2.1.3–§2.1.5). Rather than rendering a single
/// template, it inspects the appraisal and resolves to the applicable per-property summary
/// form(s), which are rendered individually and concatenated into one PDF by
/// <see cref="ReportGenerationService"/>.
///
/// Dispatch rules (confirmed with the business):
///   • Block project (appraisal.Projects row exists)       → block form only
///   • Progressive appraisal type                          → construction form only
///   • Otherwise, additive fan-out by property type present (merged in order):
///       land/building (L/B/LB/LSL/LSB/LS) → land-building form
///       condo (U/LSU)                     → condo form
///       machine (MAC)                     → machine form
///
/// Detection is by <c>appraisal.AppraisalProperties.PropertyType</c>, which stores the internal
/// domain codes (L/B/LB/U/MAC/LSL/LSB/LS/LSU/…), NOT the external/parameter CollateralType codes.
/// </summary>
public sealed class AppraisalSummaryDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalSummaryDataProvider> logger)
    : IReportDataProvider, ICompositeReportProvider
{
    public string ReportTypeKey => "appraisal-summary";

    // Mirrors AppraisalTypes.Progressive in the Appraisal domain. Kept as a local literal because
    // Reporting reads via Dapper and must not take a compile dependency on the Appraisal assembly.
    private const string ProgressiveAppraisalType = "Progressive";

    // A composite provider never renders its own template; the pipeline reads the child keys
    // from ICompositeReportProvider instead. Guard so a misconfiguration surfaces loudly.
    public Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "appraisal-summary is a composite report; render its child reports instead.");

    public async Task<IReadOnlyList<string>> GetChildReportKeysAsync(
        string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        const string sql = """
            SELECT
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.Projects pr
                                       WHERE pr.AppraisalId = @AppraisalId)
                          THEN 1 ELSE 0 END AS bit)                              AS ProjectExists,
                (SELECT a.AppraisalType FROM appraisal.Appraisals a
                 WHERE a.Id = @AppraisalId)                                      AS AppraisalType,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.AppraisalProperties ap
                                       WHERE ap.AppraisalId = @AppraisalId
                                         AND ap.PropertyType IN ('U','LSU'))
                          THEN 1 ELSE 0 END AS bit)                              AS HasCondo,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.AppraisalProperties ap
                                       WHERE ap.AppraisalId = @AppraisalId
                                         AND ap.PropertyType = 'MAC')
                          THEN 1 ELSE 0 END AS bit)                              AS HasMachine,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.AppraisalProperties ap
                                       WHERE ap.AppraisalId = @AppraisalId
                                         AND ap.PropertyType IN ('L','B','LB','LSL','LSB','LS'))
                          THEN 1 ELSE 0 END AS bit)                              AS HasLandBuilding;
            """;

        var p = new DynamicParameters();
        p.Add("@AppraisalId", appraisalId);

        var row = await connection.QuerySingleOrDefaultAsync<DispatchRow>(
            new CommandDefinition(sql, p, cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Appraisal", entityId);

        // Exclusive overrides
        if (row.ProjectExists)
            return Selected(appraisalId, "appraisal-summary-block");

        if (string.Equals(row.AppraisalType, ProgressiveAppraisalType, StringComparison.OrdinalIgnoreCase))
            return Selected(appraisalId, "appraisal-summary-construction");

        // Additive fan-out (fixed order: land-building, condo, machine)
        var keys = new List<string>(3);
        if (row.HasLandBuilding) keys.Add("appraisal-summary-land-building");
        if (row.HasCondo) keys.Add("appraisal-summary-condo");
        if (row.HasMachine) keys.Add("appraisal-summary-machine");

        logger.LogInformation(
            "appraisal-summary dispatch for {AppraisalId}: [{Keys}]",
            appraisalId, string.Join(", ", keys));

        return keys;
    }

    private IReadOnlyList<string> Selected(Guid appraisalId, string key)
    {
        logger.LogInformation("appraisal-summary dispatch for {AppraisalId}: [{Key}]", appraisalId, key);
        return [key];
    }

    private sealed class DispatchRow
    {
        public bool ProjectExists { get; init; }
        public string? AppraisalType { get; init; }
        public bool HasCondo { get; init; }
        public bool HasMachine { get; init; }
        public bool HasLandBuilding { get; init; }
    }
}
