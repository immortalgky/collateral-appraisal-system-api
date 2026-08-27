using Integration.FileInterface.Format.CollateralResult;

namespace Collateral.Tests.CollateralResult;

/// <summary>
/// Covers the two conversions the outbound query still does in C# after the move to
/// <c>vw_CollateralResultExport</c>. Everything else — the chain walk, the collateral match, the
/// area and age rules — now lives in the view and is exercised against a database.
///
/// These two stayed in code because both encode a decision about what to send when the value does
/// not fit, and that decision is easier to state and to test here than in SQL.
/// </summary>
public class CollateralResultExportQueryTests
{
    /// <summary>
    /// The field is 4 characters and employee ids are 5, almost all zero-padded, so the padding comes
    /// off first.
    /// </summary>
    [Theory]
    [InlineData("06327", "6327")]
    [InlineData("00042", "42")]
    [InlineData(" 06327 ", "6327")]
    [InlineData("1234", "1234")]
    public void StripsLeadingZerosToFitTheFourCharacterField(string employeeId, string expected)
    {
        Assert.Equal(expected, CollateralResultExportQuery.ToInternalValuerCode(employeeId));
    }

    /// <summary>
    /// An id with five significant digits cannot fit, and truncating it would name a DIFFERENT member
    /// of staff in the bank's core system. Blank is wrong; a wrong person is worse. Around 21 staff on
    /// the current data land here.
    /// </summary>
    [Theory]
    [InlineData("81018")]
    [InlineData("90378")]
    public void SendsBlankRatherThanTruncatingAnIdThatDoesNotFit(string employeeId)
    {
        Assert.Null(CollateralResultExportQuery.ToInternalValuerCode(employeeId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000")]
    public void TreatsAbsentOrAllZeroIdsAsNoCode(string? employeeId)
    {
        Assert.Null(CollateralResultExportQuery.ToInternalValuerCode(employeeId));
    }

    /// <summary>Machinery life span is stored with decimals; the host field carries whole years.</summary>
    [Theory]
    [InlineData(10.0, 10)]
    [InlineData(10.4, 10)]
    [InlineData(10.5, 11)]
    [InlineData(0.0, 0)]
    public void RoundsMachineryLifeToWholeYears(double lifeSpan, int expected)
    {
        Assert.Equal(expected, CollateralResultExportQuery.ToLifeYear((decimal)lifeSpan));
    }

    /// <summary>
    /// Out of range returns null, which the writer renders as zeros. One implausible machine must not
    /// abort a run that is otherwise fine.
    /// </summary>
    [Theory]
    [InlineData(1000.0)]
    [InlineData(-1.0)]
    public void RejectsALifeSpanTheFieldCannotHold(double lifeSpan)
    {
        Assert.Null(CollateralResultExportQuery.ToLifeYear((decimal)lifeSpan));
    }

    [Fact]
    public void NoLifeSpanMeansNoValue()
    {
        Assert.Null(CollateralResultExportQuery.ToLifeYear(null));
    }
}
