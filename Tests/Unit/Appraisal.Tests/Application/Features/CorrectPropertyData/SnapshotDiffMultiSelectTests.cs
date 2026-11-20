using System.Text.Json.Nodes;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;

namespace Appraisal.Tests.Application.Features.CorrectPropertyData;

/// <summary>
/// Data Correction audit: a multi-select (ObligationType, LandUseType...) is a set of codes. The same codes
/// in another order, or a missing list against an empty one, must not show up as a change nobody made.
/// </summary>
public class SnapshotDiffMultiSelectTests
{
    private static JsonObject Land(params string[]? codes)
        => new() { ["Land"] = new JsonObject { ["ObligationType"] = codes is null ? null : new JsonArray(codes.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()) } };

    [Fact]
    public void Same_codes_in_another_order_is_not_a_change()
        => Assert.Empty(SnapshotDiff.Compare(Land("02", "03"), Land("03", "02")));

    [Fact]
    public void Identical_lists_and_empty_against_missing_are_not_a_change()
    {
        Assert.Empty(SnapshotDiff.Compare(Land("02"), Land("02")));
        Assert.Empty(SnapshotDiff.Compare(Land(), Land(null)));
    }

    [Fact]
    public void A_changed_code_set_is_one_entry_showing_both_sides()
    {
        var changes = SnapshotDiff.Compare(Land("02"), Land("02", "03"));

        var entry = Assert.Single(changes);
        Assert.Equal("Land.ObligationType", entry.Key);
    }
}
