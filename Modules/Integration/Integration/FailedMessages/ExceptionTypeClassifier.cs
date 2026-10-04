namespace Integration.FailedMessages;

/// <summary>
/// Decides <c>isNonTransient</c> for a failed message (docs/failed-messages/api-contract.md
/// "Non-transient exception match"). Compares the exception type's SIMPLE name — the segment after the
/// last '+' (nested type) or '.', after stripping generic arguments, any assembly qualification and a
/// '`' arity marker — never the full namespace-qualified name.
/// </summary>
public static class ExceptionTypeClassifier
{
    private static readonly HashSet<string> NonTransientSimpleNames =
        new(StringComparer.Ordinal) { "ConflictException", "MissingIdentityKeyException" };

    public static bool IsNonTransient(string exceptionType) =>
        NonTransientSimpleNames.Contains(SimpleName(exceptionType));

    public static string SimpleName(string exceptionType)
    {
        var name = exceptionType;

        // Generic arguments ("Foo`1[[System.String, System.Private.CoreLib, ...]]") come first and carry
        // their own commas/dots, so they go before the assembly qualification (", Assembly, Version=1.0.0.0").
        var bracket = name.IndexOf('[');
        if (bracket >= 0)
            name = name[..bracket];

        var comma = name.IndexOf(',');
        if (comma >= 0)
            name = name[..comma];

        // A nested type's own name is what follows the LAST '+' ("App.Foo+ConflictException").
        name = name[(name.LastIndexOf('+') + 1)..];
        name = name[(name.LastIndexOf('.') + 1)..];

        var backtick = name.IndexOf('`');
        if (backtick >= 0)
            name = name[..backtick];

        return name.Trim();
    }
}
