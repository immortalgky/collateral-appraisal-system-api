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
/// <param name="CollateralName">AS400's own label for the collateral, e.g. a deed reference (pos 31–70).</param>
/// <param name="Address1">
/// The collateral's street address as AS400 holds it (pos 71–110), added to the feed on 2026-08-26.
/// It normally opens with the house or room number — "129/517 โครงการเพอร์เฟคเพลส", "47/18 ซ.เอกมัย 12"
/// — and that leading token is what the regulatory export matches against appraisal.ProjectUnits to
/// price a block-project unit. Not always a number: 2,030 rows of the 2026-08-03 feed leave it blank
/// and a few open with a word ("ติด…", "ภายในอาคาร…").
/// </param>
/// <param name="RecordDate">Date of the drawdown / redemption event, DDMMYYYY (pos 111–118).</param>
/// <param name="RecordIndicator">'D' = drawdown (pledged), 'R' = redeemed (released) (pos 119).</param>
/// <param name="LocationCode">AS400 location code, dec(6) (pos 120–125).</param>
/// <param name="CollateralCode">AS400 collateral code, 23 distinct values on the 2026-08-03 feed (pos 126–128).</param>
/// <param name="PropertyType">
/// AS400's property type — PCO, PSH, PTH, PWH, PLO, POT, PBC … (pos 129–131). This is the value the
/// regulatory export has to report: CAS's own BuildingTypeCode does not map onto AS400's taxonomy.
/// </param>
/// <param name="PropertyTypeDesc">
/// Human-readable form of <paramref name="PropertyType"/>, supplied by the feed itself (pos 132–171),
/// e.g. "บ้านเดี่ยว (SINGLE HOUSE)". Removes the need for a separate code table from AS400.
/// </param>
/// <param name="MasterTitle">
/// Pos 172, kept as the feed wrote it: "Y", "N", or NULL when the row does not reach that position.
///
/// Stored raw rather than as a bool because BLANK and "N" are different things and the regulatory
/// export treats them differently. AS400 truncates trailing spaces, so 1,516 rows of the 2026-08-03
/// file stop short of pos 172 — those are rows the feed never stated a flag for, and only 37 of them
/// are still held. An explicit "N" is a statement; a blank is the absence of one.
/// </param>
/// <param name="RowHash">SHA-256 hex of the raw line — used to skip unchanged rows on re-ingest.</param>
public record ParsedHostLinkRecord(
    string AppraisalReportNumber,
    string HostCollateralId,
    string? CollateralName,
    string? Address1,
    DateOnly? RecordDate,
    string RecordIndicator,
    string? LocationCode,
    string? CollateralCode,
    string? PropertyType,
    string? PropertyTypeDesc,
    string? MasterTitle,
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
