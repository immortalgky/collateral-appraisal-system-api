using ClosedXML.Excel;
using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Integration.FileInterface.Format.RegulatoryExport;

namespace Collateral.Tests.RegulatoryExport;

/// <summary>
/// Verifies the human-readable Excel companion (<see cref="RegulatoryExcelWriter"/>): a header row, one
/// data row per master, and friendly decoded values (money as a real decimal, dates as real dates,
/// coded fields as text) — the opposite of the fixed-width file's implied-decimal encoding.
/// </summary>
public class RegulatoryExcelWriterTests
{
    private static RegulatoryExportRow SampleRow() => new(
        PreviousAppraisalNumber: "6800100",
        LatestAppraisalNumber: "6800123",
        CollateralType: CollateralTypes.LandWithBuilding,
        HostCollateralId: "6702522",
        LatestAppraisalType: "ReAppraisal",
        IsUnderConstruction: false,
        ConstructionProgressPercent: null,
        LatestAppraisalValue: 2_000_000.00m,
        EarliestAppraisalValue: 1_000_000.00m,
        NumberOfFloors: 5,
        BuildingAge: 12,
        LatestAppraisalDate: new DateTime(2025, 1, 21),
        LatestProgressiveAppraisalDate: null,
        EarliestAppraisalDate: new DateTime(2020, 1, 21),
        LatestAppraisalCompanyId: Guid.NewGuid(),
        DopaCode: "103004",
        LandAreaSqWa: 80.00m,
        BuildingArea: 150.00m,
        BuildingTypeCode: "01",
        BuildingTypeDescription: "House");

    private static IXLWorksheet BuildAndOpen(params RegulatoryExportRow[] rows)
    {
        var bytes = new RegulatoryExcelWriter().Build(new DateOnly(2025, 1, 31), rows);

        // Valid xlsx = a ZIP archive (starts with the "PK" signature).
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        using var ms = new MemoryStream(bytes);
        var wb = new XLWorkbook(ms);
        return wb.Worksheet("Regulatory");
    }

    [Fact]
    public void HasCaption_AndHeaderRow()
    {
        var ws = BuildAndOpen(SampleRow());

        Assert.Contains("Effective 31/01/2025", ws.Cell(1, 1).GetString());
        Assert.Contains("1 record", ws.Cell(1, 1).GetString());

        // Header row is row 2 (row 1 is the caption).
        Assert.Equal("Collateral Type", ws.Cell(2, 1).GetString());
        Assert.Equal("Latest Valuation Date", ws.Cell(2, 26).GetString());
    }

    [Fact]
    public void OneDataRowPerMaster()
    {
        var ws = BuildAndOpen(SampleRow(), SampleRow());

        // Data starts at row 3 → two rows means the last used row is 4.
        Assert.Equal(4, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void FriendlyValues_AreDecoded()
    {
        var ws = BuildAndOpen(SampleRow());

        // Money is a real decimal, NOT the implied-decimal ×100 the fixed-width file writes.
        Assert.Equal(2_000_000.00m, ws.Cell(3, 7).GetValue<decimal>());   // Appraisal Value as Completed
        // Date is a real date cell.
        Assert.Equal(new DateTime(2025, 1, 21), ws.Cell(3, 12).GetDateTime()); // Valuation Date
        // Coded fields are readable text.
        Assert.Equal("External (1)", ws.Cell(3, 15).GetString());          // Appraiser Type
        Assert.Equal("Completed (N)", ws.Cell(3, 5).GetString());          // Under Construction
        Assert.Equal("6702522", ws.Cell(3, 4).GetString());               // HOST Collateral ID
        Assert.Equal("House", ws.Cell(3, 22).GetString());                // Building Name
    }

    [Fact]
    public void Condo_GatesFields_LikeFixedWidthWriter()
    {
        // Condo mirrors RegulatoryFileWriter: no Land Area, no Building Type ID/Name,
        // but Area Utilization (BuildingArea) IS populated.
        var ws = BuildAndOpen(SampleRow() with { CollateralType = CollateralTypes.Condo });

        Assert.True(ws.Cell(3, 19).IsEmpty());                             // Land Area (Sq.Wa) — blank
        Assert.Equal(150.00m, ws.Cell(3, 20).GetValue<decimal>());        // Area Utilization — populated
        Assert.Equal("", ws.Cell(3, 21).GetString());                     // Building Type ID — blank
        Assert.Equal("", ws.Cell(3, 22).GetString());                     // Building Name — blank
    }

    [Fact]
    public void BareLand_GatesFields_LikeFixedWidthWriter()
    {
        // Bare land mirrors RegulatoryFileWriter: Land Area populated, but no Area Utilization
        // and no Building Type ID/Name.
        var ws = BuildAndOpen(SampleRow() with { CollateralType = CollateralTypes.Land });

        Assert.Equal(80.00m, ws.Cell(3, 19).GetValue<decimal>());         // Land Area (Sq.Wa) — populated
        Assert.True(ws.Cell(3, 20).IsEmpty());                            // Area Utilization — blank
        Assert.Equal("", ws.Cell(3, 21).GetString());                     // Building Type ID — blank
        Assert.Equal("", ws.Cell(3, 22).GetString());                     // Building Name — blank
    }
}
