namespace Collateral.RequestCollaterals.Models;

public class RequestCollateral : Entity<long>
{
    public long ReqId { get; private set; }

    private RequestCollateral() { }

    private RequestCollateral(long collatId, long reqId)
    {
        RuleCheck
            .Valid()
            .AddErrorIf(
                reqId < 1,
                "Cannot create request collateral with request ID smaller than 1."
            )
            .ThrowIfInvalid();

        Id = collatId;
        ReqId = reqId;
    }

    public static RequestCollateral Create(long collatId, long reqId)
    {
        return new RequestCollateral(collatId, reqId);
    }
}
