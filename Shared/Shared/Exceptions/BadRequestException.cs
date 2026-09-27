namespace Shared.Exceptions;

public class BadRequestException : Exception
{
    public string? Details { get; }

    /// <summary>
    /// Optional machine-readable payload (e.g. an error code and the offending items) surfaced as
    /// ProblemDetails extensions so the client can act on it without parsing <see cref="Exception.Message"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; }

    public BadRequestException(string message) : base(message)
    {
    }

    public BadRequestException(string message, string details) : base(message)
    {
        Details = details;
    }

    public BadRequestException(string message, IReadOnlyDictionary<string, object?> extensions) : base(message)
    {
        Extensions = extensions;
    }
}