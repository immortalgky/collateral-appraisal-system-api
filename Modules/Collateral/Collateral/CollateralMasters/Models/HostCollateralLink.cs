namespace Collateral.CollateralMasters.Models;

/// <summary>
/// One collateral as AS400 holds it, keyed the way AS400 keys it: by collateral id (CCDCID).
/// Refreshed by the nightly COLLATLINK / AS400_COLLAT feed.
///
/// WHY THE KEY IS THE COLLATERAL ID, NOT THE APPRAISAL NUMBER. The feed is one row per collateral
/// and a single appraisal can cover many of them — 952 appraisals on the 2026-08-03 file carry more
/// than one collateral, up to ten each. This table used to be keyed by AppraisalNumber and collapsed
/// them, silently dropping 8,383 of 36,110 rows (23%). Those dropped rows included the per-unit ids
/// of block projects, which is the "unit key" the project export had been waiting on AS400 to supply
/// — it was in the file all along.
///
/// Why this exists alongside <see cref="CollateralMaster.HostCollateralId"/>: the master needs a
/// CollateralMaster to exist first, and 6,699 completed appraisals on the production-like dataset
/// never get one (condo missing SubDistrict, land with no title number, leaseholds that never
/// resolve). Their AS400 ids had nowhere to land and the ingest reported them as NotFound. Keyed by
/// the feed's own id, nothing else has to have succeeded first.
///
/// The master keeps its own copy for now — COLLATERAL_RESULT and the v1 regulatory export still read
/// it. Once v1 is retired the master's copy can go and this becomes the only home.
/// </summary>
public class HostCollateralLink
{
    public Guid Id { get; private set; }

    /// <summary>AS400's collateral id (CCDCID). The feed's own key — unique across the file.</summary>
    public string HostCollateralId { get; private set; } = null!;

    /// <summary>
    /// appraisal.Appraisals.AppraisalNumber — AS400 calls it CCSURV. NOT unique: one appraisal can
    /// hold many collateral. Stored exactly as the feed sent it, including the 'B' prefix that block
    /// projects carry.
    /// </summary>
    public string AppraisalNumber { get; private set; } = null!;

    /// <summary>AS400's own label for the collateral, usually a deed reference such as "ฉ.212567".</summary>
    public string? CollateralName { get; private set; }

    /// <summary>
    /// The collateral's street address as AS400 holds it, added to the feed on 2026-08-26. It
    /// normally opens with the house or room number — "129/517 โครงการเพอร์เฟคเพลส" — and the
    /// regulatory export matches that leading token against appraisal.ProjectUnits to price a
    /// block-project unit. It is the only key that works for a house in a development, whose
    /// CollateralName is a deed number that appears nowhere in the unit table.
    /// </summary>
    public string? Address1 { get; private set; }

    /// <summary>True once AS400 reports 'R' (released). A later 'D' clears it again.</summary>
    public bool IsRedeemed { get; private set; }

    /// <summary>
    /// AS400's master-title flag (pos 132), exactly as the feed wrote it: "Y", "N", or NULL when the
    /// row stopped short of that position.
    ///
    /// NULL and "N" are NOT the same, and the regulatory export separates them. The bank does report
    /// collateral flagged "N" — collateral 59305 is flagged N and appears in the bank's own 2026-08-02
    /// file three times — so "N" is included. A row that never stated a flag at all is a different
    /// case and stays out. Stored raw rather than filtered on ingest so the rule can change without
    /// re-reading the feed.
    /// </summary>
    public string? MasterTitle { get; private set; }

    /// <summary>AS400 location code, dec(6).</summary>
    public string? LocationCode { get; private set; }

    /// <summary>AS400 collateral code — 23 distinct values on the 2026-08-03 feed.</summary>
    public string? CollateralCode { get; private set; }

    /// <summary>
    /// AS400's property type (PCO, PSH, PTH, PWH, PLO, POT, PBC …). The regulatory export reports
    /// THIS, not CAS's own BuildingTypeCode — the two taxonomies do not line up, and CAS's code is
    /// "99 อื่นๆ" for 85–100% of rows in every bucket.
    /// </summary>
    public string? PropertyType { get; private set; }

    /// <summary>
    /// Human-readable form of <see cref="PropertyType"/>, e.g. "บ้านเดี่ยว (SINGLE HOUSE)". The feed
    /// supplies it, so no separate code table is needed from AS400.
    /// </summary>
    public string? PropertyTypeDesc { get; private set; }

    /// <summary>Date of the drawdown / redemption event as AS400 stated it.</summary>
    public DateOnly? RecordDate { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private HostCollateralLink() { }

    public HostCollateralLink(string hostCollateralId, HostCollateralLinkValues values, DateTime updatedAt)
    {
        Id = Guid.CreateVersion7();
        HostCollateralId = hostCollateralId;
        Apply(values, updatedAt);
    }

    /// <summary>
    /// Overwrites with the latest feed values. Deliberately unconditional: the feed is the authority
    /// on this state, and a drawdown after a redemption must clear the flag rather than merge with it.
    /// </summary>
    public void Apply(HostCollateralLinkValues values, DateTime updatedAt)
    {
        AppraisalNumber  = values.AppraisalNumber;
        CollateralName   = Clean(values.CollateralName);
        Address1         = Clean(values.Address1);
        IsRedeemed       = values.IsRedeemed;
        MasterTitle      = Clean(values.MasterTitle);
        LocationCode     = Clean(values.LocationCode);
        CollateralCode   = Clean(values.CollateralCode);
        PropertyType     = Clean(values.PropertyType);
        PropertyTypeDesc = Clean(values.PropertyTypeDesc);
        RecordDate       = values.RecordDate;
        UpdatedAt        = updatedAt;
    }

    /// <summary>
    /// True when applying these values would leave the row exactly as it already is.
    ///
    /// Every stored field has to be compared. Leaving one out makes a row that changed only in that
    /// field look Unchanged, and the new value never reaches the database — the bug that would have
    /// hidden PropertyType if it had been added without extending this.
    /// </summary>
    public bool Matches(HostCollateralLinkValues values)
        => AppraisalNumber  == values.AppraisalNumber
           && CollateralName   == Clean(values.CollateralName)
           && Address1         == Clean(values.Address1)
           && IsRedeemed       == values.IsRedeemed
           && MasterTitle      == Clean(values.MasterTitle)
           && LocationCode     == Clean(values.LocationCode)
           && CollateralCode   == Clean(values.CollateralCode)
           && PropertyType     == Clean(values.PropertyType)
           && PropertyTypeDesc == Clean(values.PropertyTypeDesc)
           && RecordDate       == values.RecordDate;

    private static string? Clean(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// The mutable half of <see cref="HostCollateralLink"/> — everything the feed can restate for a
/// collateral id it has already reported. Grouped into one type so <see cref="HostCollateralLink.Apply"/>
/// and <see cref="HostCollateralLink.Matches"/> cannot drift apart as fields are added.
/// </summary>
public record HostCollateralLinkValues(
    string AppraisalNumber,
    string? CollateralName,
    string? Address1,
    bool IsRedeemed,
    string? MasterTitle,
    string? LocationCode,
    string? CollateralCode,
    string? PropertyType,
    string? PropertyTypeDesc,
    DateOnly? RecordDate);
