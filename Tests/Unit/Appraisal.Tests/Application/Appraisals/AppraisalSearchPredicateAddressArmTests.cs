using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Tests.Application.Appraisals;

/// <summary>
/// Pins that the six address arms are emitted only for the levels a term actually names.
///
/// This is a performance gate with teeth. Every statement the predicate builds carries
/// OPTION (RECOMPILE), so it is compiled afresh on each keystroke and compilation cost tracks the
/// size of the text. Leaving all six address arms in unconditionally measured +86..119 ms on
/// EVERY search — including "REQ-105" and "691054", terms that can never match an address name.
/// Measured side by side on one host, interleaved, 7 arms vs 13 vs this design.
///
/// The failure mode if this regresses is invisible: results stay correct and the endpoint just
/// gets slower, which is exactly how the cost went unnoticed the first time.
/// </summary>
public class AppraisalSearchPredicateAddressArmTests
{
    private static string SqlFor(AddressNameMatch match) =>
        AppraisalSearchPredicate.Build("term", "properties", armCap: null, match)!.Value.Sql;

    private static readonly string[] AddressFields =
        ["'province'", "'district'", "'subDistrict'", "'dopaProvince'", "'dopaDistrict'", "'dopaSubDistrict'"];

    [Fact]
    public void Default_match_emits_no_address_arm()
    {
        var sql = SqlFor(default);

        Assert.All(AddressFields, f => Assert.DoesNotContain(f, sql));
        // The non-address arms are untouched — this is a narrowing of the address arms only.
        Assert.Contains("'titleNumber'", sql);
    }

    [Fact]
    public void None_is_the_same_as_default()
    {
        Assert.Equal(SqlFor(default), SqlFor(AddressNameMatch.None));
    }

    [Theory]
    // Each level gates BOTH families: the deed column and the DOPA column read the same masters.
    [InlineData(true, false, false, "'province'", "'dopaProvince'")]
    [InlineData(false, true, false, "'district'", "'dopaDistrict'")]
    [InlineData(false, false, true, "'subDistrict'", "'dopaSubDistrict'")]
    public void A_matched_level_emits_its_deed_and_dopa_arm_and_no_other(
        bool province, bool district, bool subDistrict, string deedField, string dopaField)
    {
        var sql = SqlFor(new AddressNameMatch(province, district, subDistrict));

        Assert.Contains(deedField, sql);
        Assert.Contains(dopaField, sql);
        foreach (var other in AddressFields.Where(f => f != deedField && f != dopaField))
            Assert.DoesNotContain(other, sql);
    }

    [Fact]
    public void All_levels_matched_emits_all_six()
    {
        var sql = SqlFor(new AddressNameMatch(true, true, true));

        Assert.All(AddressFields, f => Assert.Contains(f, sql));
    }

    [Fact]
    public void Includes_lets_non_address_arms_through_regardless()
    {
        Assert.True(AddressNameMatch.None.Includes(AddressLevel.None));
        Assert.False(AddressNameMatch.None.Any);
        Assert.True(new AddressNameMatch(false, false, true).Any);
    }

    [Fact]
    public void Address_arms_are_dropped_for_the_documents_scope_too()
    {
        // Scope already excludes them; this pins that the two filters compose rather than one
        // resurrecting arms the other removed.
        var sql = AppraisalSearchPredicate
            .Build("term", "documents", armCap: null, new AddressNameMatch(true, true, true))!.Value.Sql;

        Assert.All(AddressFields, f => Assert.DoesNotContain(f, sql));
    }
}
