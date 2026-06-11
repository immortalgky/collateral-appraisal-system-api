namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Describes a single column in a fixed-width record: its logical name, exact character width,
/// and padding alignment.
/// </summary>
/// <param name="Name">Logical field name — used as the key in the values dictionary passed to
/// <see cref="FixedWidthRecordBuilder.Build"/>.</param>
/// <param name="Width">Exact character width of this column in the output record.</param>
/// <param name="Align">Padding direction: <see cref="FixedWidthAlign.Left"/> for alpha fields
/// (right-padded), <see cref="FixedWidthAlign.Right"/> for numeric fields (left-padded).</param>
public sealed record FixedWidthField(string Name, int Width, FixedWidthAlign Align);
