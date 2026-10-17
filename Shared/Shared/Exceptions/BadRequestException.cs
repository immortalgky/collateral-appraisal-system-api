namespace Shared.Exceptions;

public class BadRequestException : Exception
{
    public string? Details { get; }

    /// <summary>
    /// Optional machine-readable discriminator, like <see cref="ConflictException.Code"/>, for clients that
    /// must tell one 400 from another without matching on the message.
    /// </summary>
    public string? Code { get; }

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

    public BadRequestException(string message, string? details, string code) : base(message)
    {
        Details = details;
        Code = code;
    }
    
    public BadRequestException(string message, IReadOnlyDictionary<string, object?> extensions) : base(message)
    {
        Extensions = extensions;
    }
}
