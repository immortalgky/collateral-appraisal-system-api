namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Controls how a field value is padded within its fixed-width column.
/// Right = numeric (left-padded with spaces); Left = alpha (right-padded with spaces);
/// RightZero = implied-decimal numeric (left-padded with zeros; blank stays all spaces).
/// </summary>
public enum FixedWidthAlign
{
    /// <summary>Alpha / left-justified: value is right-padded with spaces.</summary>
    Left,

    /// <summary>Numeric / right-justified: value is left-padded with spaces.</summary>
    Right,

    /// <summary>
    /// Implied-decimal numeric: value is left-padded with <c>'0'</c> to fill the column width.
    /// A blank (empty) value stays all spaces — it is NOT zero-filled.
    /// Overflow (value longer than width) throws under <see cref="FixedWidthOverflow.ThrowOnNumeric"/>.
    /// </summary>
    RightZero,

    /// <summary>
    /// Implied-decimal numeric, ALWAYS zero-filled: value is left-padded with <c>'0'</c>, and a blank
    /// (empty / null) value becomes all zeros (not spaces). Used for outbound AS400 files where every
    /// numeric column — including the trailer count — must be zero-filled (zoned decimal cannot be blank).
    /// Overflow throws under <see cref="FixedWidthOverflow.ThrowOnNumeric"/>.
    /// </summary>
    RightZeroFill,
}
