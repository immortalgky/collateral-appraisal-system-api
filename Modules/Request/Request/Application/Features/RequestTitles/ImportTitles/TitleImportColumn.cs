namespace Request.Application.Features.RequestTitles.ImportTitles;

public enum TitleImportColumnKind
{
    Text,
    Integer,
    Decimal,
    Boolean
}

/// <summary>
/// One column of the title bulk-import spreadsheet.
///
/// This record is the single source of truth for the feature: it drives the generated template's
/// header row, the header→column matching when a user brings their own file, the cell parsing, and
/// the length/range checks. Add a column here and all four follow.
/// </summary>
/// <param name="Key">Field name on <see cref="Request.Contracts.Requests.Dtos.RequestTitleDto"/>, camelCase. Address columns use "titleAddress.moo" style paths.</param>
/// <param name="Label">Header written into the generated template, and the column name used in error messages.</param>
/// <param name="Aliases">
/// Other headers accepted on upload. Always includes the Thai wording used by the on-screen form, so
/// a file someone built by hand from what they see in the UI still maps; matching ignores case and
/// whitespace, so "Title No" and "TitleNo" need not both be listed.
/// </param>
/// <param name="ParameterGroup">The parameter group this column's codes come from. Listed in the template's Reference sheet, and validated against unless <paramref name="CheckedByDomain"/> says a domain type already owns the check.</param>
/// <param name="CheckedByDomain">Suppresses the parameter-table check because a domain type validates this column with a better message.</param>
/// <param name="MaxLength">The smaller of the DB column's HasMaxLength and the on-screen form's own cap — whichever would reject the value first.</param>
/// <param name="MaxIntegerDigits">Mirrors the frontend number-input rule, so imported rows survive the form's own zod check.</param>
/// <param name="ForceTextFormat">Format the template column as Text so Excel keeps leading zeros.</param>
/// <param name="Hint">Shown as a cell comment on the template header.</param>
/// <param name="AppliesTo">
/// Which collateral types this column is for, as prose for the template's Reference sheet. All rows
/// live on one worksheet, so with ~50 columns this is what tells a user which ones concern them.
/// Filled in by <see cref="TitleImportCatalog"/> when it assembles the sheet, not at each call site.
/// </param>
public record TitleImportColumn(
    string Key,
    string Label,
    string[]? Aliases = null,
    TitleImportColumnKind Kind = TitleImportColumnKind.Text,
    string? ParameterGroup = null,
    bool CheckedByDomain = false,
    int? MaxLength = null,
    int? MaxIntegerDigits = null,
    int DecimalPlaces = 0,
    decimal? Min = null,
    decimal? Max = null,
    bool ForceTextFormat = false,
    string? Hint = null,
    string AppliesTo = "All")
{
    /// <summary>Every header spelling that maps to this column.</summary>
    public IEnumerable<string> AllHeaders()
    {
        yield return Label;
        yield return Key;
        if (Aliases is null) yield break;
        foreach (var alias in Aliases) yield return alias;
    }
}
