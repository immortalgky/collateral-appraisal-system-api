namespace Request.Application.Features.RequestTitles.ImportTitles.Reading;

/// <summary>
/// Reads the tab-separated block Excel puts on the clipboard when you copy a range.
///
/// It is not "split on tab": a cell containing a newline, tab or quote is wrapped in double quotes
/// with inner quotes doubled, exactly like CSV. Splitting naively tears such a row apart and every
/// column after it lands in the wrong field, so this is a real parser.
/// </summary>
internal static class TsvSheetReader
{
    internal static RawSheet Read(string sheetName, string text)
    {
        var rows = Split(text ?? string.Empty);

        // Trailing blank line from the clipboard is normal — drop fully empty rows at the end only,
        // so a deliberate gap in the middle still shows up (and gets skipped by the validator).
        while (rows.Count > 0 && rows[^1].All(string.IsNullOrWhiteSpace)) rows.RemoveAt(rows.Count - 1);

        if (rows.Count == 0)
            throw new BadRequestException(
                "No pasted data found. Copy the range from Excel, including the header row, and paste it again.");

        var headers = rows[0].Select(h => h.Trim()).ToList();
        var data = rows.Skip(1).Cast<IReadOnlyList<string>>().ToList();

        return new RawSheet(sheetName, headers, data, data.Count);
    }

    private static List<List<string>> Split(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new System.Text.StringBuilder();
        var inQuotes = false;
        var i = 0;

        void EndCell()
        {
            row.Add(cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            rows.Add(row);
            row = [];
        }

        while (i < text.Length)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    // "" inside a quoted cell is a literal quote; a lone " closes the cell.
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i += 2; continue; }
                    inQuotes = false;
                    i++;
                    continue;
                }

                cell.Append(ch);
                i++;
                continue;
            }

            switch (ch)
            {
                case '"' when cell.Length == 0:
                    inQuotes = true;
                    i++;
                    break;
                case '\t':
                    EndCell();
                    i++;
                    break;
                case '\r':
                    EndRow();
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    i++;
                    break;
                case '\n':
                    EndRow();
                    i++;
                    break;
                default:
                    cell.Append(ch);
                    i++;
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0) EndRow();

        return rows;
    }
}
