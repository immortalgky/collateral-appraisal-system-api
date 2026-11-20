namespace Reporting.Application.Formatting;

/// <summary>A list in running Thai text: "A", "A และ B", "A, B และ C" (", " between the items, " และ " before the last).</summary>
public static class ThaiTextList
{
    public static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " และ " + items[^1]
    };
}
