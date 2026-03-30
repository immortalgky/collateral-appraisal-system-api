namespace Workflow.Domain.Committees;

public class CommitteeThreshold : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    private CommitteeThreshold() { }

    internal static CommitteeThreshold Create(Guid committeeId, decimal? minValue, decimal? maxValue,
        int priority)
    {
        return new CommitteeThreshold
        {
            //Id = Guid.CreateVersion7(),
            CommitteeId = committeeId,
            MinValue = minValue,
            MaxValue = maxValue,
            Priority = priority,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;
}
