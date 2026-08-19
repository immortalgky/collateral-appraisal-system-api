namespace Collateral.CollateralMasters.Models;

/// <summary>
/// AS400's view of one appraisal's collateral, keyed the way AS400 itself keys it: by appraisal
/// report number (CCSURV). One row per appraisal number, refreshed by the nightly COLLATLINK feed.
///
/// Why this exists alongside <see cref="CollateralMaster.HostCollateralId"/>: the master needs a
/// CollateralMaster to exist first, and 6,699 completed appraisals on the production-like dataset
/// never get one (condo missing SubDistrict, land with no title number, leaseholds that never
/// resolve). Their AS400 ids had nowhere to land and the ingest reported them as NotFound. The feed
/// is keyed by appraisal number, so storing it that way needs nothing else to have succeeded first.
///
/// The master keeps its own copy for now — COLLATERAL_RESULT and the v1 regulatory export still read
/// it. Once v1 is retired the master's copy can go and this becomes the only home.
/// </summary>
public class HostCollateralLink
{
    public Guid Id { get; private set; }

    /// <summary>appraisal.Appraisals.AppraisalNumber — AS400 calls it CCSURV. Unique.</summary>
    public string AppraisalNumber { get; private set; } = null!;

    /// <summary>AS400's collateral id (CCDCID). Null is possible: the feed can report a row with no id.</summary>
    public string? HostCollateralId { get; private set; }

    /// <summary>True once AS400 reports 'R' (released). A later 'D' clears it again.</summary>
    public bool IsRedeemed { get; private set; }

    /// <summary>Date of the drawdown / redemption event as AS400 stated it.</summary>
    public DateOnly? RecordDate { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private HostCollateralLink() { }

    public HostCollateralLink(
        string appraisalNumber,
        string? hostCollateralId,
        bool isRedeemed,
        DateOnly? recordDate,
        DateTime updatedAt)
    {
        Id = Guid.CreateVersion7();
        AppraisalNumber = appraisalNumber;
        Apply(hostCollateralId, isRedeemed, recordDate, updatedAt);
    }

    /// <summary>
    /// Overwrites with the latest feed values. Deliberately unconditional: the feed is the authority
    /// on this state, and a drawdown after a redemption must clear the flag rather than merge with it.
    /// </summary>
    public void Apply(string? hostCollateralId, bool isRedeemed, DateOnly? recordDate, DateTime updatedAt)
    {
        HostCollateralId = string.IsNullOrWhiteSpace(hostCollateralId) ? null : hostCollateralId.Trim();
        IsRedeemed = isRedeemed;
        RecordDate = recordDate;
        UpdatedAt = updatedAt;
    }

    /// <summary>True when applying these values would leave the row exactly as it already is.</summary>
    public bool Matches(string? hostCollateralId, bool isRedeemed, DateOnly? recordDate)
        => HostCollateralId == (string.IsNullOrWhiteSpace(hostCollateralId) ? null : hostCollateralId.Trim())
           && IsRedeemed == isRedeemed
           && RecordDate == recordDate;
}
