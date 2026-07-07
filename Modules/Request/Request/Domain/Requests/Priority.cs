namespace Request.Domain.Requests;

public class Priority : ValueObject
{
    public string Code { get; }

    private Priority(string code)
    {
        Code = code;
    }

    public static Priority Normal => new("Normal");
    public static Priority High => new("High");

    /// <summary>
    /// Parses fresh (user-supplied) input into the canonical Pascal-case form. Null/empty falls
    /// back to Normal; anything outside the known set throws — use this on the write path only.
    /// </summary>
    public static Priority FromString(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Normal;

        return code.Trim().ToLowerInvariant() switch
        {
            "normal" => Normal,
            "high" => High,
            _ => throw new ArgumentException($"Invalid priority: {code}")
        };
    }

    /// <summary>
    /// True when <see cref="FromString"/> would succeed: null/empty (falls back to Normal) or a
    /// known code. Use in validators so out-of-set values fail as a 400 instead of throwing in Save.
    /// </summary>
    public static bool IsValid(string? code) =>
        string.IsNullOrWhiteSpace(code) ||
        code.Trim().ToLowerInvariant() is "normal" or "high";

    /// <summary>
    /// Rehydrates a persisted value. Normalizes known casings to canonical form but preserves any
    /// legacy/out-of-set value as-is so reads never throw (the DB is the source of truth on read).
    /// </summary>
    public static Priority? FromDatabase(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        return code.Trim().ToLowerInvariant() switch
        {
            "normal" => Normal,
            "high" => High,
            _ => new Priority(code.Trim())
        };
    }

    public override string ToString()
    {
        return Code;
    }

    public static implicit operator string?(Priority? priority)
    {
        return priority?.Code;
    }
}
