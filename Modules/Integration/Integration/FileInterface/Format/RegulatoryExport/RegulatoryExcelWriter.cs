using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using ClosedXML.Excel;

namespace Integration.FileInterface.Format.RegulatoryExport;

/// <summary>
/// Writes a human-readable Excel companion to the CAS-AS400-Regulatory interface file. Same fields and
/// order as <see cref="RegulatoryFileWriter"/>, but with friendly column headers and values non-IT users
/// can read: real decimals (not implied-decimal ×100), dd/MM/yyyy dates, and code+description text.
///
/// Uses the same <see cref="RegulatoryExportRow"/> data as the fixed-width writer, so the two cannot drift.
/// </summary>
public sealed class RegulatoryExcelWriter
{
    private const string MoneyFormat = "#,##0.00";
    private const string PercentFormat = "0.00";
    private const string DateFormat = "dd/MM/yyyy";
    private const string ProgressiveAppraisalType = "Progressive";

    // Friendly column headers, in interface-file field order (Record Type is omitted; Collateral Type is
    // added up front so a reader can tell what each row is).
    private static readonly string[] Headers =
    [
        "Collateral Type",
        "Application Id (Appraisal No.)",
        "Newest Application Id (Appraisal No.)",
        "HOST Collateral ID",
        "Under Construction",
        "Construction Progress (%)",
        "Appraisal Value as Completed",
        "Appraisal Value at Origination",
        "Number of Floors",
        "Building Age (yrs)",
        "Market Selling Price",
        "Valuation Date",
        "Valuation Price (Baht)",
        "Mortgage Value",
        "Appraiser Type",
        "Registration Flag",
        "Land Ownership Flag",
        "DOPA Location",
        "Land Area (Sq.Wa)",
        "Area Utilization",
        "Building Type ID",
        "Building Name",
        "Expected Completion Date",
        "Construction Review Date",
        "First Valuation Date",
        "Latest Valuation Date",
    ];

    public byte[] Build(DateOnly effectiveDate, IReadOnlyList<RegulatoryExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Regulatory");

        // Caption echoing the file Header/Trailer (effective date + record count).
        ws.Cell(1, 1).Value =
            $"CAS-AS400-Regulatory — Effective {effectiveDate:dd/MM/yyyy} — {rows.Count} record(s)";
        ws.Range(1, 1, 1, Headers.Length).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;

        // Header row.
        for (var i = 0; i < Headers.Length; i++)
            ws.Cell(2, i + 1).Value = Headers[i];
        var headerRow = ws.Row(2);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var r = 3;
        foreach (var row in rows)
        {
            WriteRow(ws, r, row);
            r++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteRow(IXLWorksheet ws, int r, RegulatoryExportRow row)
    {
        var c = 1;

        ws.Cell(r, c++).Value = row.CollateralType;
        // Both Application Id and Newest Application Id carry the latest appraisal number (matches the file).
        ws.Cell(r, c++).Value = row.LatestAppraisalNumber ?? "";
        ws.Cell(r, c++).Value = row.LatestAppraisalNumber ?? "";
        ws.Cell(r, c++).Value = row.HostCollateralId ?? "";
        ws.Cell(r, c++).Value = UnderConstructionText(row);
        Percent(ws.Cell(r, c++), ConstructionProgress(row));
        Money(ws.Cell(r, c++), row.LatestAppraisalValue);
        Money(ws.Cell(r, c++), OriginationValue(row));
        Number(ws.Cell(r, c++), row.NumberOfFloors);
        Number(ws.Cell(r, c++), row.BuildingAge);
        c++; // Market Selling Price — not yet sourced
        Date(ws.Cell(r, c++), row.LatestAppraisalDate);
        Money(ws.Cell(r, c++), row.LatestAppraisalValue);
        c++; // Mortgage Value — not yet sourced
        ws.Cell(r, c++).Value = row.LatestAppraisalCompanyId.HasValue ? "External (1)" : "Internal (2)";
        c++; // Registration Flag — not yet sourced
        c++; // Land Ownership Flag — not yet sourced
        ws.Cell(r, c++).Value = row.DopaCode ?? "";
        // Collateral-type gating mirrors RegulatoryFileWriter so the .xlsx companion cannot drift
        // from the fixed-width .txt: Land Area only for land types; Area Utilization for building
        // types + condo; Building Type ID/Name only for building types.
        var isLandType = IsLandType(row);
        var isBuildingType = IsBuildingType(row);
        var isCondo = IsCondo(row);
        Money(ws.Cell(r, c++), isLandType ? row.LandAreaSqWa : null);
        Money(ws.Cell(r, c++), (isBuildingType || isCondo) ? row.BuildingArea : null);
        ws.Cell(r, c++).Value = isBuildingType ? (row.BuildingTypeCode ?? "") : "";
        ws.Cell(r, c++).Value = isBuildingType ? (row.BuildingTypeDescription ?? "") : "";
        c++; // Expected Completion Date — not yet sourced
        Date(ws.Cell(r, c++), row.LatestProgressiveAppraisalDate);
        Date(ws.Cell(r, c++), row.EarliestAppraisalDate);
        Date(ws.Cell(r, c++), row.LatestAppraisalDate);
    }

    // Collateral-type predicates — bodies match RegulatoryFileWriter exactly so the two writers
    // gate the same fields identically and cannot drift.
    private static bool IsLandType(RegulatoryExportRow row) =>
        row.CollateralType is CollateralTypes.Land
                           or CollateralTypes.LandWithBuilding
                           or CollateralTypes.Leasehold
                           or CollateralTypes.LeaseholdBuilding
                           or CollateralTypes.LeaseholdWithBuilding;

    private static bool IsBuildingType(RegulatoryExportRow row) =>
        row.CollateralType is CollateralTypes.LandWithBuilding
                           or CollateralTypes.LeaseholdBuilding
                           or CollateralTypes.LeaseholdWithBuilding;

    private static bool IsCondo(RegulatoryExportRow row) =>
        row.CollateralType == CollateralTypes.Condo;

    private static bool IsBareLand(RegulatoryExportRow row) =>
        row.CollateralType is CollateralTypes.Land or CollateralTypes.Leasehold;

    // Mirrors RegulatoryFileWriter's Under Construction rule (Y/N/L/blank), rendered as readable text.
    // Only land / building / land&building types are in-group; condo (and everything else) → blank.
    private static string UnderConstructionText(RegulatoryExportRow row)
    {
        if (IsBareLand(row))
            return "Vacant land (L)";
        if (IsBuildingType(row))
            return row.IsUnderConstruction ? "Under construction (Y)" : "Completed (N)";
        return "";
    }

    // Mirrors RegulatoryFileWriter's Construction Progress rule (condo and other non-land/building → 0.00).
    private static decimal? ConstructionProgress(RegulatoryExportRow row)
    {
        if (IsBareLand(row))
            return 100m;
        if (IsBuildingType(row))
            return row.ConstructionProgressPercent ?? 0m;
        return 0m;
    }

    // Mirrors RegulatoryFileWriter's origination-value rule.
    private static decimal? OriginationValue(RegulatoryExportRow row) =>
        string.Equals(row.LatestAppraisalType, ProgressiveAppraisalType, StringComparison.Ordinal)
            ? row.EarliestAppraisalValue
            : row.LatestAppraisalValue;

    private static void Money(IXLCell cell, decimal? value)
    {
        if (value is null) return;
        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    private static void Percent(IXLCell cell, decimal? value)
    {
        if (value is null) return;
        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = PercentFormat;
    }

    private static void Number(IXLCell cell, int? value)
    {
        if (value is null) return;
        cell.Value = value.Value;
    }

    private static void Date(IXLCell cell, DateTime? value)
    {
        if (value is null) return;
        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = DateFormat;
    }
}
