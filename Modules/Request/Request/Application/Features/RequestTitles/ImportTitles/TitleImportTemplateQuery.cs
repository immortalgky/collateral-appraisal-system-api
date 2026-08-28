using ClosedXML.Excel;
using Dapper;

namespace Request.Application.Features.RequestTitles.ImportTitles;

/// <summary>Builds the blank workbook a user fills in and uploads back.</summary>
public record TitleImportTemplateQuery : IQuery<TitleImportTemplateResult>;

public record TitleImportTemplateResult(byte[] FileBytes, string ContentType, string FileName);

internal class TitleImportTemplateQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<TitleImportTemplateQuery, TitleImportTemplateResult>
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<TitleImportTemplateResult> Handle(
        TitleImportTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var codes = await LoadParameterCodesAsync(cancellationToken);

        using var workbook = new XLWorkbook();

        // The dropdown sources have to exist before any sheet can point a validation rule at them.
        var listNames = WriteListsSheet(workbook, codes);

        foreach (var sheet in TitleImportCatalog.Sheets)
            WriteDataSheet(workbook, sheet, listNames);

        WriteReferenceSheet(workbook, codes);
        WriteInstructionsSheet(workbook);

        // Built first because the validations depend on it, but it belongs at the back of the tab
        // strip for anyone who unhides it.
        workbook.Worksheet(TitleImportCatalog.ListsSheetName).Position = workbook.Worksheets.Count;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new TitleImportTemplateResult(
            stream.ToArray(), XlsxContentType, "request-titles-template.xlsx");
    }

    /// <summary>
    /// Writes the hidden sheet the dropdowns read from, one column per parameter group, and returns
    /// the defined name created for each.
    ///
    /// A named range rather than a direct "Lists!A2:A34" reference: Excel has historically refused
    /// cross-sheet list validation written any other way, and a name also survives the user
    /// reordering or renaming sheets.
    /// </summary>
    private static Dictionary<string, string> WriteListsSheet(
        XLWorkbook workbook, IReadOnlyList<ParameterCode> codes)
    {
        var ws = workbook.Worksheets.Add(TitleImportCatalog.ListsSheetName);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var column = 1;

        foreach (var group in codes.Select(c => c.Group).Distinct())
        {
            var entries = codes.Where(c => c.Group == group).ToList();
            if (entries.Count == 0) continue;

            ws.Cell(1, column).Value = group;
            for (var i = 0; i < entries.Count; i++)
            {
                // Code first so stripping the label is a plain prefix cut, and so the column reads
                // in the same order as the Reference sheet.
                ws.Cell(i + 2, column).Value =
                    $"{entries[i].Code}{TitleImportCatalog.CodeLabelSeparator}{entries[i].Description}";
                ws.Cell(i + 2, column).Style.NumberFormat.Format = "@";
            }

            var name = $"List_{group}";
            workbook.NamedRanges.Add(name, ws.Range(2, column, entries.Count + 1, column));
            names[group] = name;
            column++;
        }

        ws.Hide();
        return names;
    }

    /// <summary>
    /// Puts an in-cell dropdown on a column that takes a fixed set of values.
    ///
    /// Deliberately a warning, not a hard stop: the list holds "code — description" entries, so a
    /// user who knows the codes and types a bare "01" would otherwise be blocked by Excel even
    /// though the importer accepts it. Validation also does not survive a paste, and pasting is a
    /// supported way to fill this template.
    /// </summary>
    private static void AddDropdown(
        IXLWorksheet ws, int columnNumber, TitleImportColumn column,
        IReadOnlyDictionary<string, string> listNames)
    {
        string source;

        if (column.ParameterGroup is { } group && listNames.TryGetValue(group, out var name))
            source = $"={name}";
        else if (column.Kind == TitleImportColumnKind.Boolean)
            source = "\"Y,N\"";
        else
            return;

        var validation = ws.Range(2, columnNumber, TitleImportLimits.MaxRows + 1, columnNumber)
            .CreateDataValidation();

        validation.List(source, inCellDropdown: true);
        validation.IgnoreBlanks = true;
        validation.ErrorStyle = XLErrorStyle.Warning;
        validation.ErrorTitle = "Value not in the list";
        validation.ErrorMessage =
            "Pick from the dropdown, or type the code on its own. See the Reference sheet for the valid codes.";
    }

    private static void WriteDataSheet(
        XLWorkbook workbook, TitleImportSheet sheet, IReadOnlyDictionary<string, string> listNames)
    {
        var ws = workbook.Worksheets.Add(sheet.Key);

        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            var column = sheet.Columns[i];
            var cell = ws.Cell(1, i + 1);
            cell.Value = column.Label;

            if (column.Hint is not null)
                cell.CreateComment().AddText(column.Hint);

            // Deed numbers, house numbers and codes are text that happens to look numeric.
            // Without this Excel drops leading zeros the moment the user types them.
            if (column.ForceTextFormat)
                ws.Column(i + 1).Style.NumberFormat.Format = "@";

            AddDropdown(ws, i + 1, column, listNames);
        }

        var header = ws.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        header.Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 1, 12, 40);
    }

    private static void WriteReferenceSheet(XLWorkbook workbook, IReadOnlyList<ParameterCode> codes)
    {
        var ws = workbook.Worksheets.Add("Reference");
        var row = 1;

        void Title(string text)
        {
            var cell = ws.Cell(row, 1);
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 12;
            row += 1;
        }

        void Head(params string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            row += 1;
        }

        Title("Valid codes (read from the system at download time)");
        Head("Group", "Code", "Description");
        foreach (var code in codes)
        {
            ws.Cell(row, 1).Value = code.Group;
            ws.Cell(row, 2).Value = code.Code;
            ws.Cell(row, 3).Value = code.Description;
            ws.Cell(row, 2).Style.NumberFormat.Format = "@";
            row += 1;
        }

        row += 2;
        Title("Accepted column headers (case and spacing are ignored)");
        Head("Sheet", "Header", "Applies to", "Also accepted");
        foreach (var sheet in TitleImportCatalog.Sheets)
        {
            foreach (var column in sheet.Columns)
            {
                ws.Cell(row, 1).Value = sheet.Key;
                ws.Cell(row, 2).Value = column.Label;
                ws.Cell(row, 3).Value = column.AppliesTo;
                ws.Cell(row, 4).Value = string.Join(", ", new[] { column.Key }.Concat(column.Aliases ?? []));
                row += 1;
            }
        }

        ws.Columns().AdjustToContents(1, 1, 12, 60);
        ws.SheetView.FreezeRows(2);
    }

    private static void WriteInstructionsSheet(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Instructions");
        var row = 1;

        void Line(string text, bool bold = false, int size = 11)
        {
            var cell = ws.Cell(row, 1);
            cell.Value = text;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.FontSize = size;
            row += 1;
        }

        Line("How to bulk-import collateral titles", bold: true, size: 14);
        row += 1;

        Line("1. Use the sheet that matches the collateral type", bold: true);
        foreach (var sheet in TitleImportCatalog.Sheets)
            Line($"      \u2022 {sheet.Key} = {sheet.Name}");
        Line("      \u2022 All real estate goes on the Property sheet; the Collateral Type Code decides");
        Line("        which of its columns apply. Leave the rest empty — see \"Applies to\" on the Reference sheet.");
        row += 1;

        Line("2. Enter data from row 2 onwards", bold: true);
        Line("      \u2022 Blank rows are skipped and are not reported as errors.");
        Line("      \u2022 Leave columns that do not apply empty; extra columns of your own are ignored.");
        Line($"      \u2022 Up to {TitleImportLimits.MaxRows} rows and 5 MB per import.");
        Line("      \u2022 Column headers may be written in English or Thai; case and spacing do not matter.");
        Line("      \u2022 Columns are matched by their header text, not their position — you may reorder");
        Line("        them, and delete any column your collateral type does not use.");
        Line("      \u2022 The Collateral Type Code column and both address blocks cannot be removed:");
        Line("        every row needs them whatever its type.");
        row += 1;

        Line("3. Both addresses are required", bold: true);
        Line("      \u2022 \"Title Address\" uses the Land Department master.");
        Line("      \u2022 \"DOPA Address\" uses the Department of Provincial Administration master.");
        Line("      \u2022 They are different data - never copy one into the other. Some localities exist in only one master.");
        Line("      \u2022 Type the Thai name (for example Silom / Bang Rak / Bangkok) or the 6-digit sub-district code.");
        Line("      \u2022 The postcode is filled in for you; leave it out.");
        row += 1;

        Line("4. Codes are listed on the Reference sheet", bold: true);
        Line("      \u2022 Collateral type, title type, building type and so on take the CODE, not the name.");
        Line("      \u2022 Rawang applies to title deeds (DEED); Sheet Number applies to NS3K. They are separate columns.");
        row += 1;

        Line("5. Upload, review, then confirm", bold: true);
        Line("      \u2022 Rows that pass are imported; rows that fail are listed with their row number and reason.");
        Line("      \u2022 Nothing is saved until you save the request itself.");
        row += 1;

        Line("Tip: if the data is already in Excel, copy the cell range including the header row and use", bold: true);
        Line("the \"Paste from Excel\" tab on screen instead of uploading a file.");

        ws.Column(1).Width = 110;
    }

    // Dapper matches a positional record by constructor arity/order, so the SELECT must project
    // exactly these three columns in this order — SeqNo is for ORDER BY only, never selected.
    private sealed record ParameterCode(string Group, string Code, string Description);

    private async Task<IReadOnlyList<ParameterCode>> LoadParameterCodesAsync(CancellationToken cancellationToken)
    {
        var groups = TitleImportCatalog.ParameterGroups().ToList();
        if (groups.Count == 0) return [];

        // Thai first, English as the fallback: a handful of groups are only seeded in EN, and a blank
        // description in the reference sheet is worse than an English one.
        const string sql = """
            SELECT p.[Group]       AS [Group],
                   p.[Code]        AS [Code],
                   COALESCE(th.[Description], p.[Description]) AS [Description]
            FROM parameter.Parameters p
            OUTER APPLY (
                SELECT TOP 1 t.[Description]
                FROM parameter.Parameters t
                WHERE t.[Group] = p.[Group] AND t.[Code] = p.[Code]
                  AND t.[Language] = 'TH' AND t.[IsActive] = 1
            ) th
            WHERE p.[Group] IN @Groups
              AND p.[IsActive] = 1
              AND p.[Language] = (
                    SELECT TOP 1 x.[Language] FROM parameter.Parameters x
                    WHERE x.[Group] = p.[Group] AND x.[Code] = p.[Code] AND x.[IsActive] = 1
                    ORDER BY CASE x.[Language] WHEN 'TH' THEN 0 WHEN 'EN' THEN 1 ELSE 2 END
              )
            ORDER BY p.[Group], p.[SeqNo], p.[Code]
            """;

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<ParameterCode>(
            new CommandDefinition(sql, new { Groups = groups }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
