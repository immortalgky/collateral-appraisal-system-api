using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Integration.FileInterface.Format.RegulatoryExport;

namespace Collateral.Tests.RegulatoryExport;

/// <summary>
/// Pins <see cref="RegulatoryFileWriter"/> to the 300-char CAS-AS400-Regulatory layout, focusing on the
/// corrected sourcing rules:
///   • Field 2 (ApplicationId) = the LATEST appraisal number, same as field 3 (bank always sends latest).
///   • Field 8 (AppraisalValueOrigination) = earliest value when the latest engagement is a Progressive
///     (construction) inspection; otherwise the latest value.
///   • Field 10 (BuildingAge) = zero-filled building age (now sourced for all building types + condo).
///   • Field 18 (DopaLocation) = the resolved DOPA sub-district code.
/// Money is implied-decimal (value ×100, no point, left zero-filled to 15).
///
/// Detail column ranges (0-based, end-exclusive) derived from the DetailFields widths:
///   ApplicationId 1..11 · AppraisalValueCompleted 46..61 · AppraisalValueOrigination 61..76 ·
///   BuildingAge 79..82 · DopaLocation 138..144.
/// </summary>
public class RegulatoryFileWriterTests
{
    private static RegulatoryExportRow SampleRow() => new(
        LatestAppraisalNumber: "6800123",
        CollateralType: CollateralTypes.LandWithBuilding, // "LB" → building fields populate
        HostCollateralId: "6702522",
        LatestAppraisalType: "ReAppraisal",
        IsUnderConstruction: false,
        ConstructionProgressPercent: 100m, // view-computed final value (completed LB → 100)
        LatestAppraisalValue: 2_000_000.00m,
        EarliestAppraisalValue: 1_000_000.00m,
        SellingPrice: 3_000_000.00m,
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

    [Fact]
    public void Detail_Is300Chars_AndStartsWithD()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        Assert.Equal(300, line.Length);
        Assert.Equal('D', line[0]);
    }

    [Fact]
    public void Field2And3_BothCarryLatestAppraisalNumber()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 2-11 (index 1..11) ApplicationId and pos 12-21 (index 11..21) NewestApplicationId
        // both left-aligned with the LATEST appraisal number.
        Assert.Equal("6800123".PadRight(10), line[1..11]);
        Assert.Equal("6800123".PadRight(10), line[11..21]);
    }

    [Fact]
    public void Field5_UnderConstruction_IsBlank_ForCondo()
    {
        // Condo is NOT in the land/building/land&building group → Under Construction blank (writer rule).
        var row = SampleRow() with { CollateralType = CollateralTypes.Condo };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal(' ', line[40]);                       // pos 41 UnderConstruction → blank
    }

    [Fact]
    public void Field8_Origination_UsesEarliestValue_WhenLatestIsProgressive()
    {
        var row = SampleRow() with { LatestAppraisalType = "Progressive" };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        // Origination (61..76) = earliest 1,000,000 ×100 = 100000000, zero-filled to 15.
        Assert.Equal("100000000".PadLeft(15, '0'), line[61..76]);
        // Completed (46..61) is always the latest value, 2,000,000 ×100.
        Assert.Equal("200000000".PadLeft(15, '0'), line[46..61]);
    }

    [Fact]
    public void Field8_Origination_UsesLatestValue_WhenLatestIsNotProgressive()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow()); // ReAppraisal

        // Origination (61..76) = latest 2,000,000 ×100 = 200000000.
        Assert.Equal("200000000".PadLeft(15, '0'), line[61..76]);
    }

    [Fact]
    public void Field9_NumberOfFloors_IsZeroFilled_WhenPresent()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 77-79 (index 79..82 is BuildingAge; floors are 77-79 → index 76..79): 5 → "005".
        Assert.Equal("005", line[76..79]);
    }

    [Fact]
    public void Field9_NumberOfFloors_IsZeros_WhenNull()
    {
        // The view returns NULL for condo/land (gated to building types); the writer renders it as "000".
        var row = SampleRow() with { NumberOfFloors = null };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal("000", line[76..79]);
    }

    [Fact]
    public void Field10_BuildingAge_IsZeroFilled()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 80-82 (index 79..82): age 12 → "012".
        Assert.Equal("012", line[79..82]);
    }

    [Fact]
    public void Field4_HostCollateralId_IsZeroFilled_WhenPresent()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 22-40 (index 21..40), 19-char HOST collateral id, right-aligned zero-filled.
        Assert.Equal("6702522".PadLeft(19, '0'), line[21..40]);
    }

    [Fact]
    public void Field4_HostCollateralId_IsZeros_WhenNull()
    {
        // Column is NULL until the inbound host-mapping feed populates it → all zeros.
        var row = SampleRow() with { HostCollateralId = null };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal(new string('0', 19), line[21..40]);
    }

    [Fact]
    public void Field6_ConstructionProgress_FormatsViewComputedValue()
    {
        // The 0 / 100 / progress% rule now lives in vw_RegulatoryExport; the writer only implied-decimal-
        // formats the value. SampleRow carries 100 (a completed building, as the view would emit).
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());
        Assert.Equal("10000", line[41..46]);                              // 100.00 ×100

        var partial = new RegulatoryFileWriter().BuildDetail(SampleRow() with { ConstructionProgressPercent = 75.00m });
        Assert.Equal("07500", partial[41..46]);                           // 75.00 ×100
    }

    [Fact]
    public void Field11_MarketSellingPrice_IsSellingPrice()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 83-97 (index 82..97) = SellingPrice 3,000,000 ×100 = 300000000, zero-filled to 15.
        Assert.Equal("300000000".PadLeft(15, '0'), line[82..97]);
    }

    [Fact]
    public void Field12_ValuationDate_IsYyyyMMdd()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 98-105 (index 97..105), 8-char date, YYYYMMDD (2025-01-21 → "20250121").
        Assert.Equal("20250121", line[97..105]);
    }

    [Fact]
    public void Field18_DopaLocation_IsResolvedCode()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // pos 139-144 (index 138..144), 6-char DOPA code, left-aligned.
        Assert.Equal("103004", line[138..144]);
    }

    [Fact]
    public void Header_And_Trailer_Are300Chars()
    {
        var writer = new RegulatoryFileWriter();

        var header = writer.BuildHeader(new DateOnly(2025, 1, 31));
        Assert.Equal(300, header.Length);
        Assert.StartsWith("H31012025", header);

        var trailer = writer.BuildTrailer(5);
        Assert.Equal(300, trailer.Length);
        Assert.StartsWith("T" + "000000005", trailer);
    }

    [Fact]
    public void BuildContent_HasHeaderDetailsTrailer_AllRecords300()
    {
        var writer = new RegulatoryFileWriter();
        var rows = new[] { SampleRow(), SampleRow() };

        var content = writer.BuildContent(new DateOnly(2025, 1, 31), rows);
        var lines = content.TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal(4, lines.Length); // H + 2 D + T
        Assert.All(lines, l => Assert.Equal(300, l.Length));
        Assert.StartsWith("H", lines[0]);
        Assert.StartsWith("D", lines[1]);
        Assert.StartsWith("T" + "000000002", lines[3]);
    }
}
