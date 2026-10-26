using ClosedXML.Excel;

namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>
/// Builds the price-list workbook: headers of <see cref="UnitPriceImportLayout"/>, one row per unit
/// (sold included, units without a key excepted) with the stored prices filled in, no totals row. Downloading it and uploading it
/// again always matches every unit.
/// </summary>
public static class UnitPriceTemplateBuilder
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(
        UnitPriceImportLayout layout,
        IEnumerable<UnitSnapshot> units,
        IReadOnlyDictionary<Guid, PriceSnapshot> pricesByUnitId)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Unit prices");

        var headers = layout.AllHeaders;
        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        // A unit with neither key cannot be matched on upload (and a keyless row with prices reads as the
        // sheet's totals row), so it is left out.
        var keyed = units.Where(u => layout.Key1Of(u) is not null || layout.Key2Of(u) is not null);

        foreach (var unit in keyed.OrderBy(u => u.SequenceNumber))
        {
            var col = 1;
            ws.Cell(row, col++).Value = ToCellValue(layout.Key1Of(unit));
            ws.Cell(row, col++).Value = ToCellValue(layout.Key2Of(unit));

            foreach (var compare in layout.CompareColumns)
                ws.Cell(row, col++).Value = ToCellValue(compare.Read(unit));

            pricesByUnitId.TryGetValue(unit.Id, out var price);
            var priceValues = new List<decimal?> { price?.StandardPrice, price?.AppraisalValue, price?.ForceSellingPrice };
            if (layout.CoverageHeader is not null) priceValues.Add(price?.CoverageAmount);
            foreach (var value in priceValues)
            {
                var cell = ws.Cell(row, col++);
                cell.Value = ToCellValue(value);
                cell.Style.NumberFormat.Format = "#,##0.00";
            }

            row++;
        }

        ws.Columns(1, headers.Count).AdjustToContents(1, Math.Min(row, 50));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        string s => s,
        int i => i,
        decimal d => d,
        _ => Blank.Value
    };
}
