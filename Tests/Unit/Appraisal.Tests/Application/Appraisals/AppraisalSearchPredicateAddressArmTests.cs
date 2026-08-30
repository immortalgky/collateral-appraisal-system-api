using System.Text.RegularExpressions;
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

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("all", true)]
    [InlineData("properties", true)]
    [InlineData("PROPERTIES", true)]
    [InlineData("documents", false)]
    [InlineData("customers", false)]
    public void ScopeCanMatchAddress_tells_callers_when_the_probe_is_pointless(string? scope, bool expected)
    {
        // Callers skip the IAddressNameSearch round trip on a false. If this ever answers true for
        // a scope Build filters out, the dropdown pays a DB call per keystroke for nothing.
        Assert.Equal(expected, AppraisalSearchPredicate.ScopeCanMatchAddress(scope));
    }

    [Fact]
    public void Address_arms_emit_one_row_per_matched_area_not_per_property()
    {
        // UNION, not UNION ALL: the (appraisal, geocode) pair is deduped, so an appraisal with
        // several parcels in the same area contributes one row instead of one per parcel —
        // duplicate badges on the client, and that many slots eaten out of the arm's TOP(@Cap).
        // Measured on the dev database, one province term dropped from 2,947 rows to 2,882.
        //
        // Parcels in DIFFERENT matching areas still contribute a row each, on purpose: they are
        // distinct matches. This is NOT a guarantee of one row per appraisal.
        var sql = SqlFor(new AddressNameMatch(true, true, true));

        // Scoped to the land/condo source union: Build joins the arms to each other with a
        // top-level UNION ALL, which is correct and must stay.
        var normalized = Regex.Replace(sql, @"\s+", " ");

        Assert.DoesNotContain("UNION ALL SELECT ap.AppraisalId", normalized);
        Assert.Equal(6, Regex.Matches(normalized, "UNION SELECT ap.AppraisalId").Count);
    }

    [Fact]
    public void Address_arms_exclude_soft_deleted_requests_like_every_other_arm()
    {
        // AllArms documents this as an invariant: an appraisal can outlive a soft-deleted request,
        // and the view does not re-apply the filter. Without it the same row is findable by its
        // province but not by its request number.
        var sql = SqlFor(new AddressNameMatch(true, true, true));

        Assert.Equal(6, sql.Split("JOIN request.Requests r ON r.Id = a.RequestId AND r.IsDeleted = 0").Length - 1);
    }

    [Theory]
    [InlineData("DopaProvince", "DopaProvinces", "TitleProvinces")]
    [InlineData("DopaDistrict", "DopaDistricts", "TitleDistricts")]
    [InlineData("DopaSubDistrict", "DopaSubDistricts", "TitleSubDistricts")]
    public void Dopa_arms_resolve_their_label_against_the_dopa_master_first(
        string column, string firstMaster, string secondMaster)
    {
        // A geocode is resolved against the master the capturing form used. 102 district and 31
        // sub-district codes present in the data carry a different NameTh in each family, so the
        // wrong order badges a DOPA address with a name no other consumer of it uses.
        var sql = SqlFor(new AddressNameMatch(true, true, true));

        var first = sql.IndexOf($"FROM parameter.{firstMaster} WHERE Code = lad.{column}", StringComparison.Ordinal);
        var second = sql.IndexOf($"FROM parameter.{secondMaster} WHERE Code = lad.{column}", StringComparison.Ordinal);

        Assert.True(first >= 0 && second >= 0, "both masters should appear in the COALESCE");
        Assert.True(first < second, $"{firstMaster} must be consulted before {secondMaster} for {column}");
    }

    [Fact]
    public void Deed_arms_still_resolve_their_label_against_the_title_master_first()
    {
        var sql = SqlFor(new AddressNameMatch(true, false, false));

        var title = sql.IndexOf("FROM parameter.TitleProvinces WHERE Code = lad.Province", StringComparison.Ordinal);
        var dopa = sql.IndexOf("FROM parameter.DopaProvinces  WHERE Code = lad.Province", StringComparison.Ordinal);

        Assert.True(title >= 0 && dopa >= 0);
        Assert.True(title < dopa);
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
