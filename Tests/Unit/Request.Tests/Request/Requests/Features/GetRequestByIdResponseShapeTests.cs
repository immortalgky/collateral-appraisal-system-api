using Request.Application.Features.Requests.GetRequestById;

namespace Request.Tests.Request.Requests.Features;

/// <summary>
/// <see cref="GetRequestByIdResponse"/> is what both GET /requests/{id} (the SPA) and GET /api/v1/requests/{id}
/// (LOS) return. The LOS route needs the "Integration" policy, which the shared test host cannot satisfy, so the
/// shape is pinned here: the Initiate fact is exposed, never the book number or collateral id behind it.
/// </summary>
public class GetRequestByIdResponseShapeTests
{
    [Fact]
    public void ExposesIsPeriodicalReappraisal_AndNeitherTheBookNumberNorTheCollateralId()
    {
        var names = typeof(GetRequestByIdResponse).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains(nameof(GetRequestByIdResponse.IsPeriodicalReappraisal), names);
        Assert.DoesNotContain(names, n => n.Contains("BookNumber") || n.Contains("CollateralId"));
    }
}
