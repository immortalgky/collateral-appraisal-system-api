using Shared.DDD;

namespace Workflow.Sla.Models;

public class BusinessHoursConfig : Entity<Guid>
{
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string TimeZone { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private BusinessHoursConfig() { }

    public static BusinessHoursConfig Create(TimeOnly startTime, TimeOnly endTime, string timeZone)
    {
        return new BusinessHoursConfig
        {
            Id = Guid.CreateVersion7(),
            StartTime = startTime,
            EndTime = endTime,
            TimeZone = timeZone,
            IsActive = true
        };
    }

    public void Update(TimeOnly startTime, TimeOnly endTime, string timeZone, bool isActive)
    {
        StartTime = startTime;
        EndTime = endTime;
        TimeZone = timeZone;
        IsActive = isActive;
    }
}
