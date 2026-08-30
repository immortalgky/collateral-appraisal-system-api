namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>The three levels of the Thai address hierarchy that can be searched by name.</summary>
public enum AddressLevel
{
    /// <summary>Not an address arm — always in play.</summary>
    None = 0,
    Province,
    District,
    SubDistrict
}

/// <summary>
/// Which address levels a search term names. Both the Title (กรมที่ดิน) and Dopa families are
/// consulted per level, because the two have diverged: some codes exist only in one, and a few
/// Thai sub-district names are Dopa-only.
/// </summary>
/// <remarks>
/// The default value matches nothing, so a caller that forgets to resolve loses the address arms
/// entirely rather than silently searching half the masters.
/// </remarks>
public readonly record struct AddressNameMatch(bool Province, bool District, bool SubDistrict)
{
    /// <summary>Matches no level. Same as <c>default</c>; named for readability at call sites.</summary>
    public static AddressNameMatch None => default;

    public bool Any => Province || District || SubDistrict;

    /// <summary>
    /// Whether an arm at <paramref name="level"/> should be emitted. Non-address arms
    /// (<see cref="AddressLevel.None"/>) always are.
    /// </summary>
    public bool Includes(AddressLevel level) => level switch
    {
        AddressLevel.Province => Province,
        AddressLevel.District => District,
        AddressLevel.SubDistrict => SubDistrict,
        _ => true
    };
}

/// <summary>
/// Answers "does this search term name a province, a district or a sub-district?" so that
/// <see cref="AppraisalSearchPredicate"/> can leave the address arms out of the statement whenever
/// they cannot possibly match.
/// </summary>
public interface IAddressNameSearch
{
    Task<AddressNameMatch> MatchAsync(string? term, CancellationToken cancellationToken = default);
}
