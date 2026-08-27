using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Integration.FileInterface.Format.RegulatoryExport;

namespace Collateral.Tests.RegulatoryExport;

/// <summary>
/// Pins <see cref="RegulatoryFileWriter"/> to the 300-char CAS-AS400-Regulatory layout, focusing on the
/// corrected sourcing rules:
///   • Field 2 (ApplicationId) = the LATEST appraisal number, same as field 3 (bank always sends latest).
///   • Field 8 (AppraisalValueOrigination) = the EARLIEST appraisal's value, always. Appraisal type
///     does not enter into it — see the field-8 tests for the history of this rule.
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
        // Not under construction → no part-built value, so field #7 falls back to LatestAppraisalValue.
        CurrentValue: null,
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

    // ── Field 5: under construction (Y / N / L / blank) ───────────────────────
    // Field 5 covers every REAL-ESTATE type, condo and legacy (UNK) included: the business rule is
    // "all real estate", and the bank's own 2026-08-02 file sends N for all 7,716 condo and all 1,209
    // legacy rows. Only non-real-estate (machinery, PRJ) stays blank.

    [Theory]
    [InlineData(CollateralTypes.Condo)]           // "U"
    [InlineData(CollateralTypes.LeaseholdCondo)]  // "LSU"
    [InlineData(CollateralTypes.Unidentified)]    // "UNK" — the legacy 99 series
    [InlineData(CollateralTypes.LandWithBuilding)]
    public void Field5_UnderConstruction_IsN_ForEveryCompletedRealEstateType(string collateralType)
    {
        var row = SampleRow() with { CollateralType = collateralType, IsUnderConstruction = false };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal('N', line[40]);                       // pos 41 UnderConstruction
    }

    [Theory]
    [InlineData(CollateralTypes.Condo)]
    [InlineData(CollateralTypes.Unidentified)]
    [InlineData(CollateralTypes.LandWithBuilding)]
    public void Field5_UnderConstruction_IsY_WhenPartBuilt(string collateralType)
    {
        var row = SampleRow() with { CollateralType = collateralType, IsUnderConstruction = true };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal('Y', line[40]);
    }

    [Fact]
    public void Field5_UnderConstruction_IsL_ForBareLand()
    {
        var row = SampleRow() with { CollateralType = CollateralTypes.Land };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal('L', line[40]);
    }

    [Theory]
    [InlineData(CollateralTypes.Machine)]
    [InlineData(CollateralTypes.Project)]
    public void Field5_UnderConstruction_IsBlank_ForNonRealEstate(string collateralType)
    {
        var row = SampleRow() with { CollateralType = collateralType };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal(' ', line[40]);
    }

    // ── Field 6: construction progress ────────────────────────────────────────
    // The value itself is computed in vw_RegulatoryExport; the writer only formats it (×100, 5 chars,
    // left zero-filled). These tests pin the formatting, not the rule.

    [Theory]
    [InlineData(100, "10000")]   // completed real estate (incl. condo / UNK)
    [InlineData(40, "04000")]    // part-built
    [InlineData(0, "00000")]     // bare land, machinery
    public void Field6_ConstructionProgress_IsImpliedDecimal(int percent, string expected)
    {
        var row = SampleRow() with { ConstructionProgressPercent = percent };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal(expected, line[41..46]);              // pos 42-46
    }

    // ── Field 8: the value at origination — the FIRST appraisal ───────────────
    // Restated by the business on 2026-08-20, reversing an earlier instruction to always send the
    // latest value. The field exists to say what the collateral was worth when the bank first took it
    // on; the current value the bank reads out of AS400 itself by collateral id.

    [Theory]
    [InlineData("ReAppraisal")]
    [InlineData("Progressive")]
    [InlineData("New")]
    public void Field8_Origination_IsEarliestValue_RegardlessOfAppraisalType(string appraisalType)
    {
        var row = SampleRow() with { LatestAppraisalType = appraisalType };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        // Origination (61..76) = earliest 1,000,000 ×100, not the latest 2,000,000. Appraisal type
        // must not enter into it — the old rule only switched to the earliest value for Progressive.
        Assert.Equal("100000000".PadLeft(15, '0'), line[61..76]);
    }

    [Fact]
    public void Field8_IsOrigination_While_Field13_IsTheLatestValue()
    {
        // The two read opposite ends of the history and must not be collapsed into one figure:
        // #8 (origination) is the FIRST appraisal, #13 (valuation price) the LATEST. They were briefly
        // both the first — reversed 2026-08-24 — so this pins them apart on purpose.
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        // AppraisalValueOrigination is field #8 at 62-76 → index 61..76; earliest 1,000,000 ×100.
        Assert.Equal("100000000".PadLeft(15, '0'), line[61..76]);
        // ValuationPrice is field #13 at 106-120 → index 105..120; latest 2,000,000 ×100.
        Assert.Equal("200000000".PadLeft(15, '0'), line[105..120]);
    }

    [Fact]
    public void Fields12And13_ComeFromTheSameAppraisal()
    {
        // Date and Price are a matching pair whichever end they read — the bank's own file never mixes
        // a first-appraisal date with a latest-appraisal price. Both must be the latest here.
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        Assert.Equal("20250121", line[97..105]);                            // #12 ValuationDate
        Assert.Equal("200000000".PadLeft(15, '0'), line[105..120]);         // #13 ValuationPrice
        Assert.Equal(line[97..105], line[292..300]);                        // == LatestValuationDate
        Assert.NotEqual(line[284..292], line[97..105]);                     // != FirstValuationDate
    }

    [Fact]
    public void BothEndsOfTheHistory_AreStillReported_InTheirOwnFields()
    {
        // Whichever appraisal #12/#13 point at, first and latest each keep a dedicated field at the end
        // of the record, so neither is ever lost.
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        Assert.Equal("20200121", line[284..292]);   // FirstValuationDate
        Assert.Equal("20250121", line[292..300]);   // LatestValuationDate
    }

    // ── Field 7: current (progress-adjusted) value ────────────────────────────

    [Fact]
    public void Field7_Completed_FallsBackToLatestValue_WhenNothingUnderConstruction()
    {
        // CurrentValue is null on SampleRow → nothing was part-built, so the as-completed value stands.
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow());

        Assert.Equal("200000000".PadLeft(15, '0'), line[46..61]);
    }

    [Fact]
    public void Field7_Completed_UsesCurrentValue_WhenUnderConstruction()
    {
        // Land 6,000,000 + building 4,000,000 at 50% = 8,000,000, computed upstream and frozen on the
        // engagement. Field 7 must report that, while field 8 reports the ORIGINATION value —
        // 1,000,000 on the sample row. The two are unrelated numbers: one is what the collateral is
        // worth part-built today, the other what it was worth when the bank first took it on.
        var row = SampleRow() with
        {
            IsUnderConstruction = true,
            ConstructionProgressPercent = 50m,
            CurrentValue = 8_000_000.00m
        };

        var line = new RegulatoryFileWriter().BuildDetail(row);

        Assert.Equal("800000000".PadLeft(15, '0'), line[46..61]);
        Assert.Equal("100000000".PadLeft(15, '0'), line[61..76]);
    }

    // ── Field 24: construction review date ────────────────────────────────────

    [Fact]
    public void Field24_ConstructionReviewDate_IsLatestAppraisalDate_WhenUnderConstruction()
    {
        // Any appraisal that reviewed the construction counts — not only a Progressive one. The sample
        // row's LatestProgressiveAppraisalDate is null, so a Progressive-only rule would blank this.
        var row = SampleRow() with { IsUnderConstruction = true };
        var line = new RegulatoryFileWriter().BuildDetail(row);

        // pos 277-284 → index 276..284. Detail dates are YYYYMMDD (only the Header uses ddMMyyyy).
        Assert.Equal("20250121", line[276..284]);
    }

    [Fact]
    public void Field24_ConstructionReviewDate_IsBlank_WhenNotUnderConstruction()
    {
        var line = new RegulatoryFileWriter().BuildDetail(SampleRow()); // IsUnderConstruction = false

        Assert.Equal("        ", line[276..284]);
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

        // pos 98-105 (index 97..105), 8-char date, YYYYMMDD. The value is the LATEST appraisal
        // (SampleRow: 2025-01-21), not the earliest (2020-01-21) — see the field 12/13 tests for why.
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
