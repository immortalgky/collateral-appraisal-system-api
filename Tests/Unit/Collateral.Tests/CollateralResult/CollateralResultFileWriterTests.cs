using Collateral.Contracts.FileInterface;
using Integration.FileInterface.Format.CollateralResult;

namespace Collateral.Tests.CollateralResult;

/// <summary>
/// Pins <see cref="CollateralResultFileWriter"/> to the 198-char vendor layout with implied-decimal
/// numerics (value ×100, NO decimal point, left zero-filled; null/zero → all zeros) and a zero-padded
/// trailer count. Every record is exactly 198 chars; H/D/T present; fields land in their column ranges.
/// </summary>
public class CollateralResultFileWriterTests
{
    private static CollateralResultRow SampleRow() => new(
        AppraisalId: Guid.NewGuid(),
        CollateralId: "25909",
        AppraisalReportNumber: "6800123",
        AppraisalValue: 4500000.00m,
        LandValue: 1500000.00m,
        BuildingValue: 3000000.00m,
        ForceSaleValue: 4400000.00m,
        CurrentAppraisalDate: new DateOnly(2025, 1, 21),
        NextAppraisalDate: new DateOnly(2028, 1, 21),
        InternalValuerCode: null,
        InternalValuerName: "Somchai Jaidee",
        ExternalValuerCode: null,
        ExternalValuerName: "บริษัท เคแทค แอพเพรซัล แอนด์ เซอร์วิส",
        LifeYear: null,
        AppraisalStatus: "A");

    [Fact]
    public void Header_Is198Chars_AndStartsWithHAndDate()
    {
        var writer = new CollateralResultFileWriter();
        var header = writer.BuildHeader(new DateOnly(2025, 1, 21));

        Assert.Equal(198, header.Length);
        Assert.StartsWith("H21012025", header);
        Assert.Equal(new string(' ', 198 - 9), header[9..]); // rest is filler
    }

    [Fact]
    public void Trailer_Is198Chars_WithZeroPaddedCount()
    {
        var writer = new CollateralResultFileWriter();
        var trailer = writer.BuildTrailer(3);

        Assert.Equal(198, trailer.Length);
        Assert.StartsWith("T" + "000000003", trailer); // 9-char zero-padded count
    }

    [Fact]
    public void Detail_Is198Chars_AndFieldsLandInSpecPositions()
    {
        var writer = new CollateralResultFileWriter();
        var line = writer.BuildDetail(SampleRow());

        Assert.Equal(198, line.Length);
        Assert.Equal('D', line[0]);

        // CollateralId pos 2-20 (index 1..20), implied-decimal id, left zero-filled.
        Assert.Equal("25909".PadLeft(19, '0'), line[1..20]);
        // Appraisal Report Number pos 21-30 (index 20..30), left-aligned.
        Assert.Equal("6800123".PadRight(10), line[20..30]);
        // Appraisal Value pos 31-45 (index 30..45): 4500000.00 ×100 = 450000000, zero-filled to 15.
        Assert.Equal("450000000".PadLeft(15, '0'), line[30..45]);
        // Land Value pos 46-60.
        Assert.Equal("150000000".PadLeft(15, '0'), line[45..60]);
        // Building Value pos 61-75.
        Assert.Equal("300000000".PadLeft(15, '0'), line[60..75]);
        // Force Sale Value pos 76-90.
        Assert.Equal("440000000".PadLeft(15, '0'), line[75..90]);
        // Current Appraisal Date pos 91-98 (DDMMYYYY).
        Assert.Equal("21012025", line[90..98]);
        // Next Appraisal Date pos 99-106.
        Assert.Equal("21012028", line[98..106]);
        // Appraisal Status pos 198 (last char).
        Assert.Equal('A', line[197]);
    }

    [Fact]
    public void Detail_NullNumericFields_AreAllZeros()
    {
        var writer = new CollateralResultFileWriter();
        var row = SampleRow() with
        {
            LandValue = null,
            BuildingValue = null,
            ForceSaleValue = null,
            LifeYear = null,
            InternalValuerName = null,
        };

        var line = writer.BuildDetail(row);

        Assert.Equal(198, line.Length);
        Assert.Equal(new string('0', 15), line[45..60]);   // Land Value null → all zeros
        Assert.Equal(new string('0', 15), line[60..75]);   // Building Value null → all zeros
        Assert.Equal(new string(' ', 4), line[106..110]);  // Internal Valuer Code always blank (alpha)
        Assert.Equal(new string(' ', 40), line[110..150]); // Internal Valuer Name blank (alpha)
        Assert.Equal("000", line[194..197]);               // Life Year null → all zeros
    }

    [Fact]
    public void LifeYear_ZeroFilled_WhenPresent()
    {
        var writer = new CollateralResultFileWriter();
        var line = writer.BuildDetail(SampleRow() with { LifeYear = 20 });

        Assert.Equal("020", line[194..197]); // pos 195-197, zero-filled
    }

    [Fact]
    public void BuildContent_HasHeaderDetailsTrailer_AllRecords198()
    {
        var writer = new CollateralResultFileWriter();
        var rows = new[] { SampleRow(), SampleRow() };

        var content = writer.BuildContent(new DateOnly(2025, 1, 21), rows);
        var lines = content.TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal(4, lines.Length);            // H + 2 D + T
        Assert.All(lines, l => Assert.Equal(198, l.Length));
        Assert.StartsWith("H", lines[0]);
        Assert.StartsWith("D", lines[1]);
        Assert.StartsWith("D", lines[2]);
        Assert.StartsWith("T", lines[3]);
        Assert.StartsWith("T" + "000000002", lines[3]); // count = 2, zero-padded
    }

    [Fact]
    public void Detail_Throws_WhenNumericFieldOverflowsWidth()
    {
        var writer = new CollateralResultFileWriter();
        // 99999999999999.99 ×100 = 9999999999999999 (16 digits) — exceeds the 15-char AppraisalValue field.
        // Numeric overflow must throw (not silently drop digits and corrupt the price).
        var row = SampleRow() with { AppraisalValue = 99999999999999.99m };

        Assert.Throws<InvalidOperationException>(() => writer.BuildDetail(row));
    }
}
