using Collateral.Contracts.FileInterface;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.FileInterface.Format.CollateralResult;

/// <summary>
/// Builds the outbound Collateral Result rows.
///
/// <b>Replaces the Collateral-module query.</b> That one started from CollateralEngagements joined to
/// CollateralMasters and treated <c>HostCollateralId IS NOT NULL</c> as the eligibility gate, which
/// made the file a function of whether a master had been created — and thousands of completed
/// appraisals never get one. They were silently absent from every file. Here the row set is the
/// appraisals themselves; see <c>vw_CollateralResultExport.sql</c> for how the AS400 collateral id is
/// found by walking the appraisal chain.
///
/// <b>Why the query lives in Integration now.</b> The file is an interface concern, and its shape is
/// driven by the AS400 spec rather than by our collateral model. The view reads the appraisal schema
/// with the AS400 link table as its key source; nothing here needs the Collateral module.
/// </summary>
public class CollateralResultExportQuery(
    ISqlConnectionFactory connectionFactory,
    ILogger<CollateralResultExportQuery> logger) : ICollateralResultQuery
{
    /// <summary>AS400 field width for InternalValuerCode (positions 107-110).</summary>
    private const int InternalValuerCodeWidth = 4;

    /// <summary>
    /// The chain walk is recursive and the view cannot carry the hint itself — SQL Server rejects
    /// OPTION() inside a view definition, so the caller supplies it. 0 means "no limit": the guard is
    /// the visited path, and a numeric cap would silently truncate long construction-inspection
    /// chains instead of erroring.
    /// </summary>
    private const string ApprovedSql = """
        SELECT
            AppraisalId, CollateralId, AppraisalReportNumber, AutoUpdate, IsExternal,
            AppraisalValue, LandValue, BuildingValue, ForceSaleValue,
            CurrentAppraisalDate, NextAppraisalDate,
            InternalValuerEmployeeId, InternalValuerName, ExternalValuerCode, ExternalValuerName,
            LifeYear, BuildingAge, AreaUtilization,
            LandAreaRai, LandAreaNgan, LandAreaSquareWa, LandAreaTotalSqWa
        FROM collateral.vw_CollateralResultExport
        OPTION (MAXRECURSION 0)
        """;

    /// <summary>
    /// Rejected appraisals still owed an 'R' record.
    ///
    /// The AppraisalType join carries the same scope as the approved side: this interface answers
    /// COLLATREV, so a rejected appraisal the host never asked about is not its business.
    /// AppraisalRejectedConsumer spools EVERY rejection — it has no reason to know the file's scope —
    /// so the narrowing happens on read. A row left out today is still in the table and would be
    /// picked up unchanged if the scope ever widens, which spooling selectively would have prevented.
    /// The cost is that rejections outside the scope keep SentAt NULL for good; that is a row nobody
    /// is waiting on, not a backlog.
    /// </summary>
    private const string RejectedSql = """
        SELECT p.AppraisalId, p.AppraisalNumber, p.HostCollateralId
        FROM collateral.PendingCollateralResults p
        JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId
        WHERE p.SentAt IS NULL
          AND a.AppraisalType = 'ReAppraisal'
        """;

    public async Task<IReadOnlyList<CollateralResultRow>> GetUnsentRowsAsync(
        CancellationToken cancellationToken = default)
    {
        var approved = await GetApprovedRowsAsync(cancellationToken);
        var rejected = await GetRejectedRowsAsync(cancellationToken);

        return [.. approved, .. rejected];
    }

    private async Task<List<CollateralResultRow>> GetApprovedRowsAsync(CancellationToken ct)
    {
        var connection = connectionFactory.GetOpenConnection();

        var raw = (await connection.QueryAsync<ApprovedRow>(
            new CommandDefinition(ApprovedSql, cancellationToken: ct, commandTimeout: 600))).ToList();

        var rows = raw.Select(Map).ToList();

        // An employee id that will not fit the 4-character field goes out blank, and blank is
        // indistinguishable from "no id on file". One warning per run rather than per row.
        var tooLong = raw
            .Where(r => !r.IsExternal
                        && !string.IsNullOrWhiteSpace(r.InternalValuerEmployeeId)
                        && ToInternalValuerCode(r.InternalValuerEmployeeId) is null)
            .Select(r => r.InternalValuerEmployeeId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tooLong.Count > 0)
            logger.LogWarning(
                "[CollateralResultExportQuery] {Count} appraiser(s) have an EmployeeId that does not fit "
                + "the {Width}-character InternalValuerCode field even after stripping leading zeros; "
                + "those rows go out with a blank code rather than a truncated one. {Offenders}",
                tooLong.Count, InternalValuerCodeWidth, string.Join(", ", tooLong.Take(50)));

        var unmatched = rows.Count(r => r.AutoUpdate != "Y");
        if (unmatched > 0)
            logger.LogInformation(
                "[CollateralResultExportQuery] {Unmatched} of {Total} row(s) could not be tied to a single "
                + "AS400 collateral; sent with a blank id and AutoUpdate 'N'",
                unmatched, rows.Count);

        return rows;
    }

    private async Task<List<CollateralResultRow>> GetRejectedRowsAsync(CancellationToken ct)
    {
        var connection = connectionFactory.GetOpenConnection();

        var raw = await connection.QueryAsync<RejectedRow>(
            new CommandDefinition(RejectedSql, cancellationToken: ct));

        // A rejected appraisal never reached drawdown, so AS400 never minted an id for it. The row
        // goes out with a blank CCDCID and the appraisal number carries the identification.
        return raw.Select(r => new CollateralResultRow(
                AppraisalId: r.AppraisalId,
                CollateralId: r.HostCollateralId ?? string.Empty,
                AppraisalReportNumber: r.AppraisalNumber,
                AppraisalValue: null,
                LandValue: null,
                BuildingValue: null,
                ForceSaleValue: null,
                CurrentAppraisalDate: null,
                NextAppraisalDate: null,
                InternalValuerCode: null,
                InternalValuerName: null,
                ExternalValuerCode: null,
                ExternalValuerName: null,
                LifeYear: null,
                AppraisalStatus: "R",
                BuildingAge: null,
                AreaUtilization: null,
                AutoUpdate: "N"))
            .ToList();
    }

    private static CollateralResultRow Map(ApprovedRow r)
    {
        // The two valuer pairs are mutually exclusive — an appraisal ran on the external path or the
        // internal one, never both. The view decides which; inferring it from whether the company
        // columns came back populated would misread an external appraisal whose company row is
        // missing as internal, and send the bank staffer's name for work a company did.
        var isExternal = r.IsExternal;

        return new CollateralResultRow(
            AppraisalId: r.AppraisalId,
            CollateralId: r.CollateralId ?? string.Empty,
            AppraisalReportNumber: r.AppraisalReportNumber,
            AppraisalValue: r.AppraisalValue,
            LandValue: r.LandValue,
            BuildingValue: r.BuildingValue,
            ForceSaleValue: r.ForceSaleValue,
            CurrentAppraisalDate: r.CurrentAppraisalDate,
            NextAppraisalDate: r.NextAppraisalDate,
            InternalValuerCode: isExternal ? null : ToInternalValuerCode(r.InternalValuerEmployeeId),
            InternalValuerName: isExternal ? null : r.InternalValuerName,
            ExternalValuerCode: isExternal ? r.ExternalValuerCode : null,
            ExternalValuerName: isExternal ? r.ExternalValuerName : null,
            LifeYear: ToLifeYear(r.LifeYear),
            AppraisalStatus: "A",
            BuildingAge: r.BuildingAge,
            AreaUtilization: r.AreaUtilization,
            AutoUpdate: r.AutoUpdate == "Y" ? "Y" : "N",
            LandAreaRai: r.LandAreaRai,
            LandAreaNgan: r.LandAreaNgan,
            LandAreaSquareWa: r.LandAreaSquareWa,
            LandAreaTotalSqWa: r.LandAreaTotalSqWa);
    }

    /// <summary>
    /// Turns an <c>EmployeeId</c> into the AS400 InternalValuerCode.
    ///
    /// The field is 4 characters while employee ids are 5, almost all zero-padded (<c>06327</c>), so
    /// the leading zeros come off first. Anything still too long returns null and is sent blank:
    /// truncating <c>81018</c> to <c>8101</c> would name a different member of staff in the bank's
    /// core system. Blank is wrong; a wrong person is worse.
    ///
    /// Public so it can be unit-tested directly.
    /// </summary>
    public static string? ToInternalValuerCode(string? employeeId)
    {
        var trimmed = employeeId?.Trim().TrimStart('0');
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > InternalValuerCodeWidth ? null : trimmed;
    }

    /// <summary>Machinery life span, rounded to the whole years the 3-char field carries.</summary>
    public static int? ToLifeYear(decimal? lifeSpanYears)
    {
        if (lifeSpanYears is not { } value)
            return null;

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded is >= 0 and <= 999 ? rounded : null;
    }

    private sealed class ApprovedRow
    {
        public Guid AppraisalId { get; init; }
        public string? CollateralId { get; init; }
        public string AppraisalReportNumber { get; init; } = null!;
        public string? AutoUpdate { get; init; }
        public bool IsExternal { get; init; }
        public decimal? AppraisalValue { get; init; }
        public decimal? LandValue { get; init; }
        public decimal? BuildingValue { get; init; }
        public decimal? ForceSaleValue { get; init; }
        public DateOnly? CurrentAppraisalDate { get; init; }
        public DateOnly? NextAppraisalDate { get; init; }
        public string? InternalValuerEmployeeId { get; init; }
        public string? InternalValuerName { get; init; }
        public string? ExternalValuerCode { get; init; }
        public string? ExternalValuerName { get; init; }
        public decimal? LifeYear { get; init; }
        public int? BuildingAge { get; init; }
        public decimal? AreaUtilization { get; init; }
        public int? LandAreaRai { get; init; }
        public int? LandAreaNgan { get; init; }
        public decimal? LandAreaSquareWa { get; init; }
        public decimal? LandAreaTotalSqWa { get; init; }
    }

    private sealed class RejectedRow
    {
        public Guid AppraisalId { get; init; }
        public string AppraisalNumber { get; init; } = null!;
        public string? HostCollateralId { get; init; }
    }
}
