namespace Auth.Domain.Groups;

public class GroupMonitoring
{
    public Guid MonitorGroupId { get; set; }
    public Group MonitorGroup { get; set; } = default!;
    public Guid MonitoredGroupId { get; set; }
    public Group MonitoredGroup { get; set; } = default!;
}
