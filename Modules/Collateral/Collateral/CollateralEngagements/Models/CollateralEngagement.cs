namespace Collateral.CollateralEngagements.Models;

public class CollateralEngagement : Entity<long>
{
    public long ReqId { get; private set; }
    public DateTime? LinkedAt { get; private set; }
    public DateTime? UnlinkedAt { get; private set; }
    public bool IsActive { get; private set; }

    private CollateralEngagement() { }

    private CollateralEngagement(long collatId, long reqId, bool isActive)
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
        IsActive = isActive;
        if (IsActive)
        {
            LinkedAt = DateTime.Now;
        }
    }

    public static CollateralEngagement Create(long collatId, long reqId)
    {
        return new CollateralEngagement(collatId, reqId, false);
    }

    public void Activate()
    {
        IsActive = true;
        LinkedAt = DateTime.Now;
        UnlinkedAt = null;
    }

    public void Deactivate()
    {
        IsActive = false;
        UnlinkedAt = DateTime.Now;
    }
}
