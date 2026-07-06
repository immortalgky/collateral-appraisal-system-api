namespace Appraisal.Domain.Appraisals;

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
    /// Rehydrates a persisted (or request-propagated) value. Normalizes known casings to canonical
    /// form but preserves any legacy/out-of-set value as-is so reads and appraisal creation never
    /// throw. Null/empty falls back to Normal (the column is required).
    /// </summary>
    public static Priority FromDatabase(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Normal;

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

    public static implicit operator string(Priority priority)
    {
        return priority.Code;
    }
}
