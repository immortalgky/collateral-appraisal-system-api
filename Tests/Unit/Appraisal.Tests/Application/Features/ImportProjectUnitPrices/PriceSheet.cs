using ClosedXML.Excel;

using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

/// <summary>Builds small in-memory price-list workbooks and project units for the import tests.</summary>
internal static class PriceSheet
{
    public static MemoryStream Build(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Condo header order of the template: keys, compare-only, prices.
    public static string[] CondoHeaders => UnitPriceImportLayout.Condo.AllHeaders.ToArray();
    public static string[] LbHeaders => UnitPriceImportLayout.LandAndBuilding.AllHeaders.ToArray();

    /// <summary>A Condo sheet row: room, regNo, floor, tower, model, area, selling, std, appraisal, force, coverage.</summary>
    public static object?[] CondoRow(
        object? room, object? regNo = null, object? appraisal = null, object? floor = null, object? tower = null,
        object? model = null, object? area = null, object? selling = null, object? std = null, object? force = null,
        object? coverage = null) =>
        // Forced-sale and insurance are required once a row is priced: default them to 0 unless a test says otherwise.
        [room, regNo, floor, tower, model, area, selling, std, appraisal,
            force ?? (appraisal is null ? null : 0), coverage ?? (appraisal is null ? null : 0)];

    public static ProjectUnit CondoUnit(
        int seq, string? room, string? regNo = null, int? floor = null, string? tower = null,
        string? model = null, decimal? area = null, decimal? selling = null) =>
        ProjectUnit.CreateCondo(Guid.NewGuid(), Guid.Empty, seq, floor, tower, regNo, room, model, area, selling);

    public static ProjectUnit LbUnit(
        int seq, string? plot, string? house = null, string? model = null, decimal? land = null,
        decimal? area = null, decimal? selling = null) =>
        ProjectUnit.CreateLandAndBuilding(Guid.NewGuid(), Guid.Empty, seq, plot, house, model, null, land, area, selling);

    public static UnitPriceSheet Parse(MemoryStream stream, UnitPriceImportLayout layout) =>
        UnitPriceExcelParser.Parse(stream, layout);

    public static UnitPriceImportAnalysis Analyze(
        UnitPriceSheet sheet, IReadOnlyList<ProjectUnit> units,
        IReadOnlyDictionary<Guid, decimal?>? current = null) =>
        AnalyzeWith("prices.xlsx", sheet, UnitPriceImportLayout.Condo, units, current ?? new Dictionary<Guid, decimal?>());

    /// <summary>The analyzer works on read-side snapshots; the tests build entities.</summary>
    public static UnitPriceImportAnalysis AnalyzeWith(
        string fileName, UnitPriceSheet sheet, UnitPriceImportLayout layout, IReadOnlyList<ProjectUnit> units,
        IReadOnlyDictionary<Guid, decimal?> current) =>
        UnitPriceImportAnalyzer.Analyze(fileName, sheet, layout, units.Select(UnitSnapshot.From).ToList(), current);

    public static byte[] Template(
        UnitPriceImportLayout layout, IEnumerable<ProjectUnit> units, IReadOnlyDictionary<Guid, ProjectUnitPrice> prices) =>
        UnitPriceTemplateBuilder.Build(
            layout, units.Select(UnitSnapshot.From),
            prices.ToDictionary(kv => kv.Key, kv => PriceSnapshot.From(kv.Value)));
}
