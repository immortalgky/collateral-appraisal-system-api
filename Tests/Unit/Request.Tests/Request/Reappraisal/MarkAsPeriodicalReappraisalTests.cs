using Request.Domain.Requests;
using Shared.Models;

namespace Request.Tests.Request.Reappraisal;

/// <summary>
/// The Auto Update flag Initiate freezes on the request: only "Y"/"N" survive (any case), and the
/// book and collateral id stay as before.
/// </summary>
public class MarkAsPeriodicalReappraisalTests
{
    private static Domain.Requests.Request NewRequest() =>
        Domain.Requests.Request.Create(new RequestData(
            "03", "SIBS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));

    [Theory]
    [InlineData("Y", "Y")]
    [InlineData("n", "N")]
    [InlineData(" y ", "Y")]
    [InlineData("X", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void Stores_only_a_Y_or_N_flag_normalised(string? flag, string? expected)
    {
        var request = NewRequest();

        request.MarkAsPeriodicalReappraisal("G-1", "62A00645", "60000006", flag);

        Assert.Equal(expected, request.ReappraisalAutoUpdate);
        Assert.Equal("G-1", request.GroupTag);
        Assert.Equal("62A00645", request.ReappraisalBookNumber);
        Assert.Equal("60000006", request.ReappraisalCollateralId);
    }
}
