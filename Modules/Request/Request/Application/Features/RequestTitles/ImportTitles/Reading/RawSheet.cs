namespace Request.Application.Features.RequestTitles.ImportTitles.Reading;

/// <summary>
/// A worksheet reduced to plain strings, before any interpretation.
///
/// Separating "read the cells" from "understand the cells" is what lets an uploaded .xlsx and a
/// block of cells pasted from Excel go through exactly one validator, so the two entry points can
/// never drift apart in what they accept.
/// </summary>
/// <param name="Name">Worksheet name (or the sheet the user picked when pasting).</param>
/// <param name="Headers">Row 1, verbatim.</param>
/// <param name="Rows">Data rows, possibly truncated — see <paramref name="TotalDataRows"/>.</param>
/// <param name="TotalDataRows">
/// How many data rows the sheet actually has. A reader may stop materialising well before this when
/// the sheet is far past any usable size, but the true figure still has to reach the caller so the
/// "at most N rows" message can name a number the user recognises.
/// </param>
public record RawSheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int TotalDataRows,
    int FirstDataRowNumber = 2)
{
    public int RowNumberOf(int index) => FirstDataRowNumber + index;
}
