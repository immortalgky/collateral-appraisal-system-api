namespace Assignment.Domain.Events;

public record RequestSubmitted
{
    public Guid CorrelationId { get; init; }
    public long RequestId { get; init; }
}