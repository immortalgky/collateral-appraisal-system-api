namespace Integration.Contracts.HostLink;

/// <summary>Result of parsing a single AS400 COLLATLINK (host collateral link) file.</summary>
public record ParsedHostLinkFile(
    DateOnly EffectiveDate,
    List<ParsedHostLinkRecord> Records
);

/// <summary>
/// Parsed values from a single Detail ('D') record line of the host-link feed.
///
/// AS400 mints a collateral row at <b>drawdown</b>, keyed to (physical collateral, owner) —
/// so a reappraisal under the same owner reuses the same <c>HostCollateralId</c>, while a new
/// owner of the same land gets a brand-new id.
/// </summary>
/// <param name="AppraisalReportNumber">
/// Our <c>appraisal.Appraisals.AppraisalNumber</c> — AS400 calls it CCSURV (pos 2–11).
/// </param>
/// <param name="HostCollateralId">The AS400 IsMaster collateral id, CCDCID, dec(19) (pos 12–30).</param>
/// <param name="RecordDate">Date of the drawdown / redemption event, DDMMYYYY (pos 31–38).</param>
/// <param name="RecordIndicator">'D' = drawdown (pledged), 'R' = redeemed (released) (pos 39).</param>
/// <param name="RowHash">SHA-256 hex of the raw line — used to skip unchanged rows on re-ingest.</param>
public record ParsedHostLinkRecord(
    string AppraisalReportNumber,
    string HostCollateralId,
    DateOnly? RecordDate,
    string RecordIndicator,
    string RowHash
);

/// <summary>Canonical <see cref="ParsedHostLinkRecord.RecordIndicator"/> values.</summary>
public static class HostLinkRecordIndicators
{
    /// <summary>Drawdown — AS400 holds a collateral row for this appraisal; the collateral is pledged.</summary>
    public const string Drawdown = "D";

    /// <summary>Redeemed — the loan was repaid and the collateral released.</summary>
    public const string Redeemed = "R";
}
