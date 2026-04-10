using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.RegularExpressions;

namespace Shared.Data.Extensions;

/// <summary>
/// EF Core value converter that stores enum values as SCREAMING_SNAKE_CASE strings.
/// E.g. <c>WorkflowStatus.InProgress</c> → <c>"IN_PROGRESS"</c>.
/// </summary>
public class ScreamingSnakeEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    // Compiled once — matches the boundary between a lowercase and uppercase letter
    private static readonly Regex _wordBoundary = new("([a-z])([A-Z])", RegexOptions.Compiled);

    public ScreamingSnakeEnumConverter()
        : base(
            v => ToScreamingSnake(v.ToString()!),
            s => ParseFromScreamingSnake(s))
    {
    }

    private static string ToScreamingSnake(string name)
        => _wordBoundary.Replace(name, "$1_$2").ToUpperInvariant();

    private static TEnum ParseFromScreamingSnake(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(
                $"Cannot convert null or empty string to enum type '{typeof(TEnum).Name}'.");

        // Try direct parse first (handles already-PascalCase stored values during migration window)
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var direct))
            return direct;

        // Convert SCREAMING_SNAKE → PascalCase: "IN_PROGRESS" → "InProgress"
        var pascal = string.Concat(
            value.Split('_')
                 .Select(w => w.Length > 0
                     ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()
                     : w));

        if (Enum.TryParse<TEnum>(pascal, ignoreCase: true, out var result))
            return result;

        throw new InvalidOperationException(
            $"Cannot convert '{value}' to enum type '{typeof(TEnum).Name}'.");
    }
}
