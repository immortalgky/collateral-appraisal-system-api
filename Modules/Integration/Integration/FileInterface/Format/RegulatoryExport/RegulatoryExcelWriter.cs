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
        Percent(ws.Cell(r, c++), row.ConstructionProgressPercent ?? 0m);   // computed in vw_RegulatoryExport
        // Field #7 = current (progress-adjusted) value — the LATEST appraisal, since that is what the
        // collateral is worth today. Field #8 = the value at ORIGINATION, the FIRST appraisal.
        //
        // These two deliberately read different ends of the history, and #8 must match
        // RegulatoryFileWriter's AppraisalValueOrigination. The .xlsx is the copy people actually open,
        // so a divergence here is invisible in the .txt and gets reported as "the fix did not deploy" —
        // which is exactly what happened when #8/#12/#13 moved to the first appraisal and only the
        // fixed-width writer was updated.
        Money(ws.Cell(r, c++), row.CurrentValue ?? row.LatestAppraisalValue);
        Money(ws.Cell(r, c++), row.EarliestAppraisalValue);
        Number(ws.Cell(r, c++), row.NumberOfFloors);
        Number(ws.Cell(r, c++), row.BuildingAge);
        Money(ws.Cell(r, c++), row.SellingPrice);   // Market Selling Price (RequestDetails.TotalSellingPrice)
        // Fields #12 and #13 — the FIRST appraisal's date and price, as a matching pair, same as
        // RegulatoryFileWriter. The latest appraisal is still reported in the two columns at the end.
        Date(ws.Cell(r, c++), row.EarliestAppraisalDate);
        Money(ws.Cell(r, c++), row.EarliestAppraisalValue);
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
        // Field #24 — latest appraisal date while under construction, blank otherwise.
        Date(ws.Cell(r, c++), row.IsUnderConstruction ? row.LatestAppraisalDate : null);
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

    // LSU is a leasehold OVER a condo unit: its area and age live on CondoDetails exactly like a
    // freehold condo's, so it must gate with U and stay out of the land / building predicates.
    private static bool IsCondo(RegulatoryExportRow row) =>
        row.CollateralType is CollateralTypes.Condo or CollateralTypes.LeaseholdCondo;

    private static bool IsBareLand(RegulatoryExportRow row) =>
        row.CollateralType is CollateralTypes.Land or CollateralTypes.Leasehold;

    // Every real-estate type is in-group for field #5 — condo and legacy (UNK) included. Machinery /
    // PRJ are not. Mirrors RegulatoryFileWriter.isRealEstate; keep the two bodies identical.
    private static bool IsRealEstate(RegulatoryExportRow row) =>
        IsLandType(row) || IsBuildingType(row) || IsCondo(row)
        || row.CollateralType is CollateralTypes.Unidentified;

    // Mirrors RegulatoryFileWriter's Under Construction rule (Y/N/L/blank), rendered as readable text.
    private static string UnderConstructionText(RegulatoryExportRow row)
    {
        if (!IsRealEstate(row))
            return "";
        if (IsBareLand(row))
            return "Vacant land (L)";
        return row.IsUnderConstruction ? "Under construction (Y)" : "Completed (N)";
    }

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
