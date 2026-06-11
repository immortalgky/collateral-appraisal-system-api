namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Specifies how <see cref="FixedWidthRecordBuilder"/> handles a value that is longer than its
/// column width.
/// </summary>
public enum FixedWidthOverflow
{
    /// <summary>
    /// All fields (alpha and numeric) silently truncate to <c>width</c> characters.
    /// Matches the legacy COLLATREV writer behaviour where column alignment is always preserved.
    /// </summary>
    TruncateAll,

    /// <summary>
    /// Right-aligned (numeric) fields throw <see cref="InvalidOperationException"/> on overflow
    /// because silent truncation would corrupt the numeric value sent to the host.
    /// Left-aligned (alpha) fields still truncate.
    /// Matches the Collateral Result writer behaviour.
    /// </summary>
    ThrowOnNumeric,
}
