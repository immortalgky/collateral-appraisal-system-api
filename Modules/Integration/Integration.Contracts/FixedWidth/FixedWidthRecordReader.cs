namespace Integration.Contracts.FixedWidth;

/// <summary>
/// Static helpers for reading individual columns out of a fixed-width record string.
///
/// Indexing is done by Unicode code-point position (i.e. <c>string</c> char index in .NET),
/// NOT by UTF-8 byte offset — matching the behaviour expected by the vendor spec when Thai
/// multi-byte characters appear in name/address/description columns.
/// </summary>
public static class FixedWidthRecordReader
{
    /// <summary>
    /// Returns the substring of <paramref name="line"/> starting at 0-based index
    /// <paramref name="start"/> with the given <paramref name="length"/>.
    ///
    /// Lenient: if the line is shorter than <c>start + length</c>, the result is right-padded
    /// with spaces to the requested length rather than throwing. This mirrors
    /// <c>CollatrevFileParser.Slice</c> exactly so a future parser refactor is drop-in.
    /// </summary>
    /// <param name="line">The fixed-width record line to slice.</param>
    /// <param name="start">0-based character position to start reading from.</param>
    /// <param name="length">Number of characters to read.</param>
    public static string Slice(string line, int start, int length)
    {
        if (start >= line.Length)
            return new string(' ', length);

        var available = Math.Min(length, line.Length - start);
        var result = line.Substring(start, available);

        if (available < length)
            result = result.PadRight(length);

        return result;
    }
}
