namespace Shared.Exceptions;

public class ConflictException : Exception
{
    /// <summary>
    /// Optional machine-readable discriminator for callers that need to distinguish between
    /// multiple 409 cases without matching on <see cref="Exception.Message"/>. Null for the
    /// existing constructors so current call sites are unaffected.
    /// </summary>
    public string? Code { get; }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string name, object key)
        : base($"{name} ({key}) already exists.")
    {
    }

    public ConflictException(string message, string code) : base(message)
    {
        Code = code;
    }
}
