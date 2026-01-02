namespace Request.Domain.Requests;

public class Source : ValueObject
{
    public string? Channel { get; }
    public DateTime? RequestDate { get; }
    public string? RequestedBy { get; }
    public string? RequestedByName { get; }

    private Source(string? channel, string? requestedBy, string? requestedByName)
    {
        Channel = channel;
        RequestDate = DateTime.Now;
        RequestedBy = requestedBy;
        RequestedByName = requestedByName;
    }

    public static Source Create(string? requestedBy, string? requestedByName, string? channel="Manual") => new(channel, requestedBy, requestedByName);
}
