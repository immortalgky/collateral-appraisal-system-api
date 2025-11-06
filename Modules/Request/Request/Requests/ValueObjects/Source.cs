namespace Request.Requests.ValueObjects;

public class Source : ValueObject
{
    public string? Channel { get; }
    public DateTime? RequestDate { get; }
    public long? RequestedBy { get; }

    private Source(string? channel, long? requestedBy)
    {
        Channel = channel;
        RequestDate = DateTime.Now;
        RequestedBy = requestedBy;
    }

    public static Source Create(long? requestedBy, string? channel="Manual") => new(channel, requestedBy);
}
