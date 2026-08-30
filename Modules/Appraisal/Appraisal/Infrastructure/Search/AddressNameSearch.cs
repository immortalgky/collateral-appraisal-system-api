using Appraisal.Application.Features.Appraisals.Shared;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Shared.Data;

namespace Appraisal.Infrastructure.Search;

/// <summary>
/// Resolves a search term against the six address master tables with a single six-EXISTS probe.
///
/// Why a round trip instead of matching the master names in memory: the answer has to agree
/// exactly with the <c>LIKE … ESCAPE '\'</c> the arms themselves run, and that is decided by the
/// column's collation (case- and accent-insensitivity included). A C# <c>StartsWith</c> would
/// drift from it silently, and the drift shows up as an address that cannot be found rather than
/// as an error.
///
/// The probe reads only small, indexed reference tables and returns three bits, so it is far
/// cheaper than the six arms it decides the fate of. Repeated terms are served from memory —
/// a debounced burst of typing re-sends the same prefixes.
/// </summary>
public class AddressNameSearch(ISqlConnectionFactory connectionFactory, IMemoryCache cache)
    : IAddressNameSearch
{
    /// <summary>
    /// Short enough that an edit in the address-master admin screen shows up in search within a
    /// couple of minutes, long enough to collapse a burst of keystrokes onto one probe. Entries
    /// are three booleans; the window also bounds how much a stream of junk terms can accumulate.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// One statement, six EXISTS. Deliberately NOT marked OPTION (RECOMPILE): unlike the arms,
    /// this is a fixed-shape query over tiny tables and benefits from a cached plan.
    /// </summary>
    private const string Sql = """
        SELECT
            CAST(CASE WHEN EXISTS (SELECT 1 FROM parameter.TitleProvinces    WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                        OR EXISTS (SELECT 1 FROM parameter.DopaProvinces     WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                      THEN 1 ELSE 0 END AS bit) AS Province,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM parameter.TitleDistricts    WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                        OR EXISTS (SELECT 1 FROM parameter.DopaDistricts     WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                      THEN 1 ELSE 0 END AS bit) AS District,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM parameter.TitleSubDistricts WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                        OR EXISTS (SELECT 1 FROM parameter.DopaSubDistricts  WHERE NameTh LIKE @SearchPattern ESCAPE '\')
                      THEN 1 ELSE 0 END AS bit) AS SubDistrict;
        """;

    public async Task<AddressNameMatch> MatchAsync(string? term, CancellationToken cancellationToken = default)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < AppraisalSearchPredicate.MinTermLength)
            return AddressNameMatch.None;

        var pattern = LikePattern.Build(trimmed);
        if (cache.TryGetValue(CacheKey(pattern), out AddressNameMatch cached)) return cached;

        var connection = connectionFactory.GetOpenConnection();
        var row = await connection.QuerySingleAsync<ProbeRow>(new CommandDefinition(
            Sql, new { SearchPattern = pattern }, cancellationToken: cancellationToken));

        var match = new AddressNameMatch(row.Province, row.District, row.SubDistrict);
        cache.Set(CacheKey(pattern), match, CacheDuration);
        return match;
    }

    private static string CacheKey(string pattern) => $"addr-name-match:{pattern}";

    /// <summary>
    /// A mutable class rather than binding <see cref="AddressNameMatch"/> directly, so Dapper maps
    /// these by NAME. A positional record would bind by position, and three same-typed columns
    /// would swap silently if the SELECT list were ever reordered.
    /// </summary>
    private sealed class ProbeRow
    {
        public bool Province { get; set; }
        public bool District { get; set; }
        public bool SubDistrict { get; set; }
    }
}
