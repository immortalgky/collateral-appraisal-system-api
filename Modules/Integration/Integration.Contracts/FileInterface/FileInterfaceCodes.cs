namespace Integration.Contracts.FileInterface;

/// <summary>
/// Canonical <c>InterfaceCode</c> values for the <c>integration.FileInterfaceConfigs</c> table.
/// </summary>
public static class FileInterfaceCodes
{
    /// <summary>
    /// Monthly Basel/RDT snapshot — one row per collateral the bank holds, taken from the AS400 feed.
    /// </summary>
    public const string Regulatory = "REGULATORY";

    public const string CollateralResult = "COLLATERAL_RESULT";
    public const string Reappraisal = "REAPPRAISAL";

    /// <summary>
    /// Nightly inbound feed mapping our AppraisalNumber to the AS400 IsMaster collateral id
    /// (CCDCID). This is the <b>only</b> authoritative source for that mapping — COLLATREV
    /// carries the same pair but is a reappraisal due-list, not a mapping feed.
    /// </summary>
    public const string HostCollateralLink = "HOST_COLLATERAL_LINK";
}
