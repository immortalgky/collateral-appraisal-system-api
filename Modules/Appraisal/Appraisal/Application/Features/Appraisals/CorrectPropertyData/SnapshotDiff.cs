using System.Text;
using System.Text.Json.Nodes;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Field-level diff of two <see cref="PropertySnapshot"/>s. Keys are dotted paths (<c>Land.OwnerName</c>),
/// values <c>{ from, to }</c>; a missing member, an empty string and an empty list all read the same as null.
///
/// Child rows are paired one of two ways, because a save does not treat them alike:
/// - <see cref="ByRowId"/> collections update rows in place, so a row keeps its Id across the save and is
///   paired on it. This is what lets a changed title number show up as a change of that title rather than
///   a removed row plus an added one. The path names the row by its business key (<c>Titles[#1234]</c>,
///   otherwise its 1-based position).
/// - everything else (depreciation periods, work items, rental entries) is deleted and re-created on every
///   save, so its Ids are new each time. Those rows are paired by position.
/// A row present on one side only is a single entry whose from/to is a one-line summary of the row.
/// </summary>
internal static class SnapshotDiff
{
    private static readonly HashSet<string> ByRowId =
        ["Titles", "Deductions", "DepreciationDetails", "Surfaces", "AreaDetails"];

    private const int SummaryLimit = 300;

    public static Dictionary<string, object?> Compare(JsonObject before, JsonObject after)
    {
        var changes = new Dictionary<string, object?>();
        CompareMembers(before, after, "", changes);
        return changes;
    }

    private static void CompareMembers(JsonObject? before, JsonObject? after, string path, Dictionary<string, object?> changes)
    {
        foreach (var name in Names(before, after))
        {
            if (name == "Id")
                continue; // identity for pairing rows, never a change in itself

            var childPath = path.Length == 0 ? name : $"{path}.{name}";
            var b = before?[name];
            var a = after?[name];

            if (b is JsonObject || a is JsonObject)
                CompareMembers(b as JsonObject, a as JsonObject, childPath, changes);
            else if (IsRows(b) || IsRows(a))
                CompareRows(name, b as JsonArray, a as JsonArray, childPath, changes);
            else if (!Same(b, a))
                Record(changes, childPath, Display(b), Display(a));
        }
    }

    private static void CompareRows(
        string collection, JsonArray? before, JsonArray? after, string path, Dictionary<string, object?> changes)
    {
        var b = Rows(before);
        var a = Rows(after);

        if (ByRowId.Contains(collection))
        {
            var afterById = a
                .Select((row, i) => (row, i))
                .Where(x => RowId(x.row) is not null)
                .ToDictionary(x => RowId(x.row)!.Value, x => x);
            var paired = new HashSet<Guid>();

            for (var i = 0; i < b.Count; i++)
            {
                var id = RowId(b[i]);
                if (id is not null && afterById.TryGetValue(id.Value, out var match))
                {
                    paired.Add(id.Value);
                    CompareMembers(b[i], match.row, $"{path}[{RowLabel(collection, b[i], i)}]", changes);
                }
                else
                {
                    Record(changes, $"{path}[{RowLabel(collection, b[i], i)}]", Summary(b[i]), null);
                }
            }

            for (var i = 0; i < a.Count; i++)
                if (RowId(a[i]) is not { } id || !paired.Contains(id))
                    Record(changes, $"{path}[{RowLabel(collection, a[i], i)}]", null, Summary(a[i]));

            if (collection == "Titles" && TitleOrderChanged(b, a, paired))
            {
                var (from, to) = TitleOrder(b, a);
                if (from != to) // e.g. 111 removed and a new 111 added in its place: the row entries already say it
                    Record(changes, path[..^"Titles".Length] + "TitleOrder", from, to);
            }

            return;
        }

        for (var i = 0; i < Math.Max(b.Count, a.Count); i++)
        {
            var rowPath = $"{path}[{i + 1}]";
            if (i >= a.Count)
                Record(changes, rowPath, Summary(b[i]), null);
            else if (i >= b.Count)
                Record(changes, rowPath, null, Summary(a[i]));
            else
                CompareMembers(b[i], a[i], rowPath, changes);
        }
    }

    // Titles carry a stored position (SequenceNumber, left out of the per-row diff), so a reorder is one entry,
    // not a change on every row it moved. It counts when the titles present on both sides changed their
    // relative order, or a new title sits ahead of an existing one. Additions at the end and removals are
    // already their own entries and do not count.
    private static bool TitleOrderChanged(List<JsonObject> before, List<JsonObject> after, HashSet<Guid> paired)
    {
        var keptBefore = before.Select(RowId).Where(id => id is { } v && paired.Contains(v));
        var keptAfter = after.Select(RowId).Where(id => id is { } v && paired.Contains(v));
        if (!keptBefore.SequenceEqual(keptAfter))
            return true;

        var lastKept = after.FindLastIndex(row => RowId(row) is { } id && paired.Contains(id));
        return after.Take(Math.Max(lastKept, 0)).Any(row => RowId(row) is not { } id || !paired.Contains(id));
    }

    // The order as deed numbers. "from" numbers the 2nd, 3rd... occurrence of a repeated number "(2)", "(3)" in the
    // before order. "to" is the after numbers: a kept title whose number did not change carries its "from" label (so
    // swapping two "111"s still reads "111, 111 (2)" -> "111 (2), 111"); a renumbered or new title shows its own
    // number, with the next suffix that the "to" list does not already use.
    private static (string From, string To) TitleOrder(List<JsonObject> before, List<JsonObject> after)
    {
        // "—" for a title without a number, as the confirm dialog shows it (formDiff.ts orderLabels).
        static string Number(JsonObject row) => row["TitleNumber"]?.ToString() is { Length: > 0 } n ? n : "—";

        var seen = new Dictionary<string, int>();
        var kept = new Dictionary<Guid, (string Number, string Label)>();
        var from = before.Select(row =>
        {
            var number = Number(row);
            seen[number] = seen.GetValueOrDefault(number) + 1;
            var label = seen[number] == 1 ? number : $"{number} ({seen[number]})";
            if (RowId(row) is { } id) kept[id] = (number, label);
            return label;
        }).ToList();

        string?[] carried = after
            .Select(row => RowId(row) is { } id && kept.TryGetValue(id, out var was) && was.Number == Number(row) ? was.Label : null)
            .ToArray();
        var used = carried.OfType<string>().ToHashSet();
        var to = after.Select((row, i) =>
        {
            if (carried[i] is { } label)
                return label;
            var number = Number(row);
            label = number;
            for (var n = 2; !used.Add(label); n++)
                label = $"{number} ({n})";
            return label;
        });
        return (string.Join(", ", from), string.Join(", ", to));
    }

    private static bool IsRows(JsonNode? node) =>
        node is JsonArray array && array.Any(item => item is JsonObject);

    private static List<JsonObject> Rows(JsonArray? array) =>
        array?.OfType<JsonObject>().ToList() ?? [];

    private static Guid? RowId(JsonObject row) =>
        Guid.TryParse(row["Id"]?.GetValue<string>(), out var id) && id != Guid.Empty ? id : null;

    // Titles are known by their deed number; every other row by where it sits in the list.
    private static string RowLabel(string collection, JsonObject row, int index) =>
        collection == "Titles" && row["TitleNumber"] is { } number ? $"#{number.GetValue<string>()}" : (index + 1).ToString();

    private static IEnumerable<string> Names(JsonObject? before, JsonObject? after) =>
        (before?.Select(p => p.Key) ?? []).Concat(after?.Select(p => p.Key) ?? []).Distinct();

    private static bool Same(JsonNode? before, JsonNode? after)
    {
        if (before is null || after is null)
            return before is null && after is null || Empty(before) && Empty(after);

        if (before is JsonValue b && after is JsonValue a
            && b.TryGetValue<decimal>(out var bd) && a.TryGetValue<decimal>(out var ad))
            return bd == ad; // 100 and 100.00 are the same figure

        return before.ToJsonString() == after.ToJsonString();
    }

    // A missing list and an empty one are the same thing to the page, and so are a null and an empty
    // string or a null and false: the real form turns every null into "" (text) or false (flags) before
    // it posts, so without this each correction would log a null → "" / null → false entry for every
    // blank field it did not touch.
    private static bool Empty(JsonNode? node) =>
        node is null or JsonArray { Count: 0 }
        || node is JsonValue value
        && (value.TryGetValue<string>(out var text) && text.Length == 0
            || value.TryGetValue<bool>(out var flag) && !flag);

    private static JsonNode? Display(JsonNode? node) =>
        node is JsonArray array ? JsonValue.Create(string.Join(", ", array.Select(item => item?.ToString()))) : node?.DeepClone();

    // "111, DEED, 1, 0, 0": a row's scalar members in declaration order, so one glance says which row.
    private static string Summary(JsonObject row)
    {
        var text = new StringBuilder();
        foreach (var (name, value) in row)
        {
            if (name == "Id" || value is null or JsonObject or JsonArray)
                continue;
            if (text.Length > 0)
                text.Append(", ");
            text.Append(value);
        }

        return text.Length <= SummaryLimit ? text.ToString() : text.ToString(0, SummaryLimit) + "…";
    }

    private static void Record(Dictionary<string, object?> changes, string path, object? from, object? to)
    {
        var key = path;
        for (var n = 2; changes.ContainsKey(key); n++)
            key = $"{path} ({n})"; // two rows sharing a label (duplicate deed numbers) must not overwrite each other
        changes[key] = new { from, to };
    }
}
