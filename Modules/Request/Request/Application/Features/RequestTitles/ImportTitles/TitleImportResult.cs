namespace Request.Application.Features.RequestTitles.ImportTitles;

/// <summary>
/// One problem found in one row. <paramref name="RowNumber"/> is the spreadsheet's own row number
/// (row 1 is the header) so the user can jump straight to it.
/// </summary>
public record TitleImportRowError(
    string Sheet,
    int RowNumber,
    string? Column,
    string Message);

/// <summary>
/// A row that passed every check and is ready to be added to the request's title list.
///
/// The address is returned twice over: <paramref name="Title"/> carries the geocodes that get
/// persisted, while the *Name properties carry what the user should see on screen. The form keeps
/// both, and re-deriving names in the browser would mean shipping the whole address master there.
/// </summary>
public record TitleImportRow(
    string Sheet,
    int RowNumber,
    RequestTitleDto Title,
    string? SubDistrictName,
    string? DistrictName,
    string? ProvinceName,
    string? DopaSubDistrictName,
    string? DopaDistrictName,
    string? DopaProvinceName);

/// <summary>
/// Outcome of parsing one workbook or one pasted block.
///
/// Partial by design: valid rows are returned alongside the errors rather than the whole file being
/// rejected, because a hundred-row file with two typos is the normal case and re-uploading it in
/// full is the expensive part for the user.
/// </summary>
/// <param name="MissingColumns">
/// Columns a row actually needed that the uploaded sheet does not have. Deleting columns you do not
/// use is supported, so this lists only the ones that were reached and found absent — not every
/// column the template ships. Without it, dropping "Notes" from a 300-row land file produces 300
/// identical row errors and no hint that the header row is the thing to fix.
/// </param>
public record TitleImportResult(
    int TotalRows,
    IReadOnlyList<TitleImportRow> Rows,
    IReadOnlyList<TitleImportRowError> Errors,
    IReadOnlyList<string> IgnoredSheets,
    IReadOnlyList<string> MissingColumns)
{
    public int ValidRows => Rows.Count;
    public int InvalidRows => TotalRows - Rows.Count;
}
