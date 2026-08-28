using Request.Application.Features.RequestTitles.ImportTitles.Reading;

namespace Request.Application.Features.RequestTitles.ImportTitles;

/// <summary>
/// Parses an uploaded .xlsx into request titles and reports what is wrong with the rest.
///
/// A query, not a command: nothing is written. The rows come back to the browser, land in the
/// on-screen title list, and are saved by the ordinary create/update request call — which is what
/// lets this work while the request is still being created and has no id yet.
/// </summary>
public record ParseTitleImportFileQuery(Stream FileStream) : IQuery<TitleImportResult>;

/// <summary>Same, for a block of cells pasted straight out of Excel.</summary>
public record ParseTitleImportPasteQuery(string Sheet, string Tsv) : IQuery<TitleImportResult>;

internal class ParseTitleImportFileQueryHandler(TitleImportValidator validator)
    : IQueryHandler<ParseTitleImportFileQuery, TitleImportResult>
{
    public Task<TitleImportResult> Handle(ParseTitleImportFileQuery query, CancellationToken cancellationToken)
    {
        // Row cap lives in the validator, which is the only place that knows which sheets hold data.
        var sheets = XlsxSheetReader.Read(query.FileStream);
        return validator.ValidateAsync(sheets, cancellationToken);
    }
}

internal class ParseTitleImportPasteQueryHandler(TitleImportValidator validator)
    : IQueryHandler<ParseTitleImportPasteQuery, TitleImportResult>
{
    public Task<TitleImportResult> Handle(ParseTitleImportPasteQuery query, CancellationToken cancellationToken)
    {
        if (TitleImportCatalog.FindSheet(query.Sheet) is null)
            throw new BadRequestException(
                $"Unknown collateral group '{query.Sheet}'. Valid groups: {string.Join(", ", TitleImportCatalog.Sheets.Select(s => s.Key))}.");

        var sheet = TsvSheetReader.Read(query.Sheet, query.Tsv);
        return validator.ValidateAsync([sheet], cancellationToken);
    }
}

public static class TitleImportLimits
{
    /// <summary>5 MB, matching the supporting-data bulk upload.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>1 MB of pasted text — far more than 500 rows of titles ever needs.</summary>
    public const int MaxPasteChars = 1024 * 1024;

    /// <summary>
    /// Every imported title also spawns its own required-document checklist in the form, so the cap
    /// is about what the create-request screen can carry, not about parsing speed.
    /// </summary>
    public const int MaxRows = 500;

    public static void GuardRowCount(int rows)
    {
        if (rows > MaxRows)
            throw new BadRequestException(
                $"At most {MaxRows} rows can be imported at once, but this contains {rows}. Split it into smaller files.");
    }
}
