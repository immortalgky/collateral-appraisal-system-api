using Collateral.CollateralMasters.CollateralResult;

namespace Collateral.Tests.CollateralResult;

/// <summary>
/// Covers <see cref="CollateralResultQuery.ToInternalValuerCode"/>.
///
/// The AS400 InternalValuerCode field (positions 107-110) is 4 characters while
/// <c>auth.AspNetUsers.EmployeeId</c> is 5, almost always zero-padded. The writer truncates
/// left-aligned fields silently, so an id that will not fit has to be dropped rather than shortened —
/// <c>81018</c> cut to <c>8101</c> would name a different employee in the bank's core system.
/// </summary>
public class InternalValuerCodeTests
{
    [Theory]
    [InlineData("06327", "6327")]   // the common shape: 4 digits with a zero pad
    [InlineData("00123", "123")]
    [InlineData("123", "123")]      // already short enough
    [InlineData(" 06327 ", "6327")] // surrounding whitespace is not significant
    public void StripsLeadingZerosWhenTheResultFits(string employeeId, string expected)
        => Assert.Equal(expected, CollateralResultQuery.ToInternalValuerCode(employeeId));

    [Theory]
    [InlineData("81018")]  // 5 significant digits — no amount of trimming helps
    [InlineData("90378")]
    [InlineData("123456")]
    public void ReturnsNullWhenTheCodeStillDoesNotFit(string employeeId)
        => Assert.Null(CollateralResultQuery.ToInternalValuerCode(employeeId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]      // trims away to nothing
    [InlineData("0000")]
    public void ReturnsNullForAbsentOrAllZeroIds(string? employeeId)
        => Assert.Null(CollateralResultQuery.ToInternalValuerCode(employeeId));

    [Fact]
    public void NeverReturnsAValueWiderThanTheField()
    {
        string[] samples = ["06327", "81018", "123", "0000", "99999", "000000001"];

        foreach (var sample in samples)
        {
            var code = CollateralResultQuery.ToInternalValuerCode(sample);
            Assert.True(code is null || code.Length <= 4,
                $"'{sample}' produced '{code}', which the writer would silently truncate.");
        }
    }
}
