namespace Appraisal.Domain.Committees;

/// <summary>
/// Value-based threshold for routing appraisals to the correct committee.
/// </summary>
public class CommitteeThreshold : Entity<Guid>
{
    public Guid CommitteeId { get; private set; }
    public decimal MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public int Priority { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CommitteeThreshold()
    {
    }

    public static CommitteeThreshold Create(
        Guid committeeId,
        decimal minValue,
        decimal? maxValue,
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
}
