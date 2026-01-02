using Shared.Messaging.Values;

namespace Shared.Messaging.Commands;

public record NotifyAssignment
{
    public Guid CorrelationId { get; init; }
    public TaskName TaskName { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
    public string NotifiedTo { get; init; } = default!;
}