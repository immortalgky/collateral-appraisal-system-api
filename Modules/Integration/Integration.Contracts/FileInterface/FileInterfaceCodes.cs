namespace Integration.Contracts.FileInterface;

/// <summary>
/// Canonical <c>InterfaceCode</c> values for the <c>integration.FileInterfaceConfigs</c> table.
/// </summary>
public static class FileInterfaceCodes
{
    public const string Regulatory = "REGULATORY";

    /// <summary>
    /// Version 2 of the regulatory snapshot, produced from the appraisal chain instead of
    /// CollateralMaster. Its own config row so the two versions write different file names and can be
    /// produced side by side for comparison during the changeover.
    /// </summary>
    public const string RegulatoryV2 = "REGULATORY_V2";

    /// <summary>Regulatory snapshot v3 — one row per collateral, from the AS400 feed.</summary>
    public const string RegulatoryV3 = "REGULATORY_V3";
    public const string CollateralResult = "COLLATERAL_RESULT";
    public const string Reappraisal = "REAPPRAISAL";

    /// <summary>
    /// Nightly inbound feed mapping our AppraisalNumber to the AS400 IsMaster collateral id
    /// (CCDCID). This is the <b>only</b> authoritative source for that mapping — COLLATREV
    /// carries the same pair but is a reappraisal due-list, not a mapping feed.
    /// </summary>
    public const string HostCollateralLink = "HOST_COLLATERAL_LINK";
}
