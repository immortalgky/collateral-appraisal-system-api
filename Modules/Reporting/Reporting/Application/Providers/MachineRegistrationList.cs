using System.Text.RegularExpressions;
using Reporting.Application.Formatting;

namespace Reporting.Application.Providers;

/// <summary>
/// The letter's เลขทะเบียนเครื่องจักร entries. Numbers are trimmed, a blank or dash-only value is dropped and a repeat is
/// kept once, in sequence order. Consecutive numbers (the same text before the last numeric segment, the last segments
/// consecutive integers of the same width) become one entry: "51-414-701-0001 ถึง 0029". Anything that does not parse that
/// way is its own entry, as written.
/// </summary>
internal static partial class MachineRegistrationList
{
    [GeneratedRegex(@"^(?<prefix>.*?)(?<digits>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LastSegment();

    public static IReadOnlyList<string> Build(IEnumerable<string?> registrations)
    {
        var numbers = registrations
            .Select(ThaiAddressFormatter.Stated)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var entries = new List<string>();
        for (var i = 0; i < numbers.Count;)
        {
            var first = Parse(numbers[i]);
            var last = i;
            if (first is not null)
            {
                while (last + 1 < numbers.Count
                       && Parse(numbers[last + 1]) is { } next
                       && next.Prefix == first.Value.Prefix
                       && next.Digits.Length == first.Value.Digits.Length
                       && next.Value == Parse(numbers[last])!.Value.Value + 1)
                    last++;
            }

            entries.Add(last > i ? $"{numbers[i]} ถึง {Parse(numbers[last])!.Value.Digits}" : numbers[i]);
            i = last + 1;
        }
        return entries;
    }

    private static (string Prefix, string Digits, long Value)? Parse(string number)
    {
        var m = LastSegment().Match(number);
        return m.Success && long.TryParse(m.Groups["digits"].Value, out var value)
            ? (m.Groups["prefix"].Value, m.Groups["digits"].Value, value)
            : null;
    }
}
