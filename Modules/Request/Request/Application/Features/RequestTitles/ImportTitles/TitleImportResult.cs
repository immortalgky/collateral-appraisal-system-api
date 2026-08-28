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
public record TitleImportResult(
    int TotalRows,
    IReadOnlyList<TitleImportRow> Rows,
    IReadOnlyList<TitleImportRowError> Errors,
    IReadOnlyList<string> IgnoredSheets)
{
    public int ValidRows => Rows.Count;
    public int InvalidRows => TotalRows - Rows.Count;
}
