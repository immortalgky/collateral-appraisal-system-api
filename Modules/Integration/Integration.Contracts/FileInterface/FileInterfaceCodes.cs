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

    /// <summary>
    /// Destination of the <c>.xlsx</c> companion the regulatory job writes alongside the fixed-width
    /// file. Separate from <see cref="Regulatory"/> because the two have different readers and
    /// therefore different destinations: AS400 collects the <c>.txt</c> over SFTP, while the Excel is
    /// opened by hand off a Windows file share the app-pool account can write to directly.
    ///
    /// Optional. With no active row the job writes the workbook exactly where it always did — beside
    /// the <c>.txt</c>, through the default sink — so setting <c>IsActive = 0</c> is a working kill
    /// switch, not a way to lose the file.
    /// </summary>
    public const string RegulatoryExcel = "REGULATORY_XLSX";

    public const string CollateralResult = "COLLATERAL_RESULT";
    public const string Reappraisal = "REAPPRAISAL";

    /// <summary>
    /// Nightly inbound feed mapping our AppraisalNumber to the AS400 IsMaster collateral id
    /// (CCDCID). This is the <b>only</b> authoritative source for that mapping — COLLATREV
    /// carries the same pair but is a reappraisal due-list, not a mapping feed.
    /// </summary>
    public const string HostCollateralLink = "HOST_COLLATERAL_LINK";
}
