using System.Text;

namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Reusable engine that pads a dictionary of field values into a single fixed-width record string.
///
/// Usage pattern:
/// <code>
///   var builder = new FixedWidthRecordBuilder(fields, recordLength, FixedWidthOverflow.ThrowOnNumeric);
///   string line = builder.Build(new Dictionary&lt;string, string?&gt; { ["FieldName"] = "value", ... });
/// </code>
///
/// The caller is responsible for formatting values before passing them (e.g. decimal formatting,
/// date formatting). Missing / null entries produce a blank (space-padded) column.
/// </summary>
public sealed class FixedWidthRecordBuilder
{
    private readonly IReadOnlyList<FixedWidthField> _fields;
    private readonly int _recordLength;
    private readonly FixedWidthOverflow _overflow;

    /// <param name="fields">Ordered field definitions. Their widths must sum to
    /// <paramref name="recordLength"/>.</param>
    /// <param name="recordLength">Expected total character length of the produced record.</param>
    /// <param name="overflow">Policy applied when a value is longer than its column width.
    /// Defaults to <see cref="FixedWidthOverflow.ThrowOnNumeric"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown at construction time if the field widths
    /// do not sum to <paramref name="recordLength"/>.</exception>
    public FixedWidthRecordBuilder(
        IReadOnlyList<FixedWidthField> fields,
        int recordLength,
        FixedWidthOverflow overflow = FixedWidthOverflow.ThrowOnNumeric)
    {
        _fields = fields;
        _recordLength = recordLength;
        _overflow = overflow;

        var sum = fields.Sum(f => f.Width);
        if (sum != recordLength)
            throw new InvalidOperationException(
                $"Fixed-width field widths sum to {sum}, expected {recordLength}.");
    }

    /// <summary>
    /// Builds one fixed-width record from the supplied value map.
    ///
    /// For each field in order:
    ///   - Looks up <c>values[field.Name]</c>; missing or null yields an empty string.
    ///   - Pads/truncates to exactly <c>field.Width</c> characters per <c>field.Align</c> and
    ///     the configured <see cref="FixedWidthOverflow"/> policy.
    ///   - Appends to the output.
    ///
    /// The returned string is guaranteed to be exactly <c>recordLength</c> characters long.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a Right-aligned value overflows and
    /// the policy is <see cref="FixedWidthOverflow.ThrowOnNumeric"/>, or if the assembled record
    /// length is unexpectedly wrong (internal invariant).</exception>
    public string Build(IReadOnlyDictionary<string, string?> values)
    {
        var sb = new StringBuilder(_recordLength);

        foreach (var field in _fields)
        {
            var raw = (values.TryGetValue(field.Name, out var v) ? v : null) ?? string.Empty;
            sb.Append(Pad(raw, field.Width, field.Align, _overflow));
        }

        var line = sb.ToString();
        if (line.Length != _recordLength)
            throw new InvalidOperationException(
                $"Built record length {line.Length} != expected {_recordLength}.");

        return line;
    }

    /// <summary>
    /// Pads (and optionally truncates) a single value to exactly <paramref name="width"/> chars.
    /// Exposed as a public static method so callers that build partial dictionaries can test padding
    /// logic in isolation without constructing a full builder.
    /// </summary>
    /// <param name="value">The string value to pad.</param>
    /// <param name="width">Target column width.</param>
    /// <param name="align">
    /// Left = alpha (right-pad with spaces);
    /// Right = numeric (left-pad with spaces);
    /// RightZero = implied-decimal numeric (blank stays spaces; non-blank left-pads with '0').
    /// </param>
    /// <param name="overflow">Overflow policy. Defaults to
    /// <see cref="FixedWidthOverflow.ThrowOnNumeric"/>.</param>
    public static string Pad(
        string value,
        int width,
        FixedWidthAlign align,
        FixedWidthOverflow overflow = FixedWidthOverflow.ThrowOnNumeric)
    {
        if (align is FixedWidthAlign.RightZero or FixedWidthAlign.RightZeroFill)
        {
            // Blank/empty: RightZero → all spaces (absent value); RightZeroFill → all zeros.
            if (string.IsNullOrEmpty(value))
                return align == FixedWidthAlign.RightZeroFill
                    ? new string('0', width)
                    : new string(' ', width);

            if (value.Length > width)
            {
                if (overflow == FixedWidthOverflow.ThrowOnNumeric)
                    throw new InvalidOperationException(
                        $"Numeric field overflow: '{value}' exceeds width {width}.");
                value = value[..width];
            }

            return value.PadLeft(width, '0');
        }

        if (value.Length > width)
        {
            if (align == FixedWidthAlign.Right && overflow == FixedWidthOverflow.ThrowOnNumeric)
                throw new InvalidOperationException(
                    $"Numeric field overflow: '{value}' exceeds width {width}.");

            value = value[..width];
        }

        return align == FixedWidthAlign.Right
            ? value.PadLeft(width)
            : value.PadRight(width);
    }
}
