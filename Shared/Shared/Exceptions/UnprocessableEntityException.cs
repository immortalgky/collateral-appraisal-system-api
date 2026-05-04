namespace Shared.Exceptions;

/// <summary>
/// Stable shape for surfacing business-rule violations on the wire. Lives in Shared
/// so both the global ProblemDetails handler and concrete module exceptions can use it
/// without taking a dependency on each other.
/// </summary>
public sealed record RuleViolationInfo(string Code, string Message, Guid? PropertyId = null);

/// <summary>
/// Thrown by command handlers when a request is well-formed but fails one or more
/// business preconditions (HTTP 422 Unprocessable Entity). Subclass per use case so
/// each module owns the exception type while sharing the wire format.
/// </summary>
public class UnprocessableEntityException : DomainException
{
    public IReadOnlyList<RuleViolationInfo> Violations { get; }

    public UnprocessableEntityException(string message, IReadOnlyList<RuleViolationInfo> violations)
        : base(message)
    {
        Violations = violations;
    }
}
