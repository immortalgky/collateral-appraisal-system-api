using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Parameter.Contracts.Parameters;

namespace Parameter.Parameters.Services;

/// <summary>
/// Resolves free-text Thai address names into a canonical <see cref="AddressDto"/>.
///
/// Reads through <see cref="IAddressRepository"/>, which is decorated by CachedAddressRepository,
/// so the ~11k title rows / ~7k DOPA rows are fetched once and then served from cache.
///
/// The normalized index is cached too, not just the rows. This service is Scoped, so a per-instance
/// field would rebuild the index on every request — tens of thousands of string normalisations to
/// answer one lookup. The cache is keyed per master and expires with the rows it was built from.
/// </summary>
public class AddressLookupService(IAddressRepository addressRepository, IMemoryCache cache)
    : IAddressLookupService
{
    private const int MaxCandidates = 5;

    // Slightly under CachedAddressRepository's 5 minutes so a refreshed row list cannot be masked
    // by a longer-lived index built from the previous one.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(4);

    public async Task<AddressResolution> ResolveTitleAsync(
        string? province, string? district, string? subDistrict, CancellationToken cancellationToken)
    {
        var index = await GetIndexAsync(
            "addresses:index:title", addressRepository.GetTitleAddressesAsync, cancellationToken);
        return index.Resolve(province, district, subDistrict);
    }

    public async Task<AddressResolution> ResolveDopaAsync(
        string? province, string? district, string? subDistrict, CancellationToken cancellationToken)
    {
        var index = await GetIndexAsync(
            "addresses:index:dopa", addressRepository.GetDopaAddressesAsync, cancellationToken);
        return index.Resolve(province, district, subDistrict);
    }

    private async Task<AddressIndex> GetIndexAsync(
        string cacheKey,
        Func<CancellationToken, Task<List<AddressDto>>> load,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(cacheKey, out AddressIndex? cached) && cached is not null) return cached;

        var index = AddressIndex.Build(await load(cancellationToken));
        cache.Set(cacheKey, index, CacheDuration);
        return index;
    }

    /// <summary>
    /// One address master, pre-normalized for lookup.
    ///
    /// Two keys per name are kept on purpose:
    ///   • exact  — trimmed, internal whitespace collapsed, lower-cased. Matches the master verbatim,
    ///              which is what makes names that legitimately CONTAIN a space work
    ///              ("บ้านใหม่ บางพัง", "กิ่งอำเภอ ภู่สิงห์").
    ///   • loose  — exact, then administrative prefixes stripped, parenthesised variants dropped and
    ///              ALL whitespace removed. Catches "ต.บ้านใหม่", "แขวงสีลม", "ปากเกร็ด(ตลาดขวัญ)".
    /// Exact always wins, so the loose pass can never steal a row that matched verbatim.
    /// </summary>
    private sealed class AddressIndex
    {
        private static readonly string[] Prefixes =
        [
            "จังหวัด", "จ.", "อำเภอ", "อ.", "กิ่งอำเภอ", "ตำบล", "ต.", "เขต", "แขวง"
        ];

        // These run over spreadsheet cells, so the input is user-supplied. The patterns are simple
        // enough that catastrophic backtracking is not realistic, but an unbounded regex on
        // untrusted text is worth closing off regardless.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex ParenSuffix =
            new(@"\s*\(.*?\)\s*", RegexOptions.Compiled, MatchTimeout);

        private static readonly Regex Whitespace =
            new(@"\s+", RegexOptions.Compiled, MatchTimeout);

        private static readonly Regex SixDigits =
            new(@"^\d{6}$", RegexOptions.Compiled, MatchTimeout);

        private readonly Dictionary<string, AddressDto> _bySubDistrictCode = [];
        private readonly Dictionary<string, List<Entry>> _bySubDistrictExact = [];
        private readonly Dictionary<string, List<Entry>> _bySubDistrictLoose = [];
        private readonly List<Entry> _all = [];

        private readonly record struct Entry(
            AddressDto Dto,
            string SubDistrictExact,
            string ProvinceExact, string ProvinceLoose,
            string DistrictExact, string DistrictLoose);

        public static AddressIndex Build(IEnumerable<AddressDto> rows)
        {
            var index = new AddressIndex();
            var seenExact = new HashSet<(string Key, string Code)>();
            var seenLoose = new HashSet<(string Key, string Code)>();

            foreach (var dto in rows)
            {
                index._bySubDistrictCode.TryAdd(dto.SubDistrictCode, dto);

                var entry = new Entry(
                    dto,
                    Exact(dto.SubDistrictName),
                    Exact(dto.ProvinceName), Loose(dto.ProvinceName),
                    Exact(dto.DistrictName), Loose(dto.DistrictName));

                index._all.Add(entry);
                Add(index._bySubDistrictExact, seenExact, Exact(dto.SubDistrictName), entry);
                Add(index._bySubDistrictExact, seenExact, Exact(dto.SubDistrictNameEn), entry);
                Add(index._bySubDistrictLoose, seenLoose, Loose(dto.SubDistrictName), entry);
                Add(index._bySubDistrictLoose, seenLoose, Loose(dto.SubDistrictNameEn), entry);
            }

            return index;

            // Dedupe on (key, sub-district code), not on the entry itself: List.Contains would run a
            // structural comparison of a 10-string DTO against every entry already in the bucket,
            // for each of ~11k rows. The only duplicate it ever needs to catch is the same row
            // arriving twice because its Thai and English names normalise alike — which is why the
            // exact and loose maps must NOT share a set: for a name with no space, paren or prefix
            // the two keys are identical, and one set would let the exact insert veto the loose one.
            static void Add(
                Dictionary<string, List<Entry>> map,
                HashSet<(string Key, string Code)> seen,
                string key,
                Entry entry)
            {
                if (key.Length == 0) return;
                if (!seen.Add((key, entry.Dto.SubDistrictCode))) return;
                if (!map.TryGetValue(key, out var list)) map[key] = list = [];
                list.Add(entry);
            }
        }

        public AddressResolution Resolve(string? province, string? district, string? subDistrict)
        {
            var p = (province ?? string.Empty).Trim();
            var d = (district ?? string.Empty).Trim();
            var s = (subDistrict ?? string.Empty).Trim();

            if (p.Length == 0 && d.Length == 0 && s.Length == 0)
                return AddressResolution.Empty;

            // A raw 6-digit geocode is unambiguous — take it and ignore the name columns.
            if (SixDigits.IsMatch(s))
                return _bySubDistrictCode.TryGetValue(s, out var byCode)
                    ? AddressResolution.Found(byCode)
                    : AddressResolution.NotFound();

            // Sub-district is what pins an address down; without it we cannot produce a geocode.
            if (s.Length == 0)
                return AddressResolution.NotFound(NarrowByParents(_all, p, d)
                    .Take(MaxCandidates).Select(e => e.Dto).ToList());

            var matches = Lookup(_bySubDistrictExact, Exact(s));

            if (matches.Count == 0)
            {
                var looseKey = Loose(s);
                matches = Lookup(_bySubDistrictLoose, looseKey);

                // A loose bucket mixes the canonical name with its parenthesised variants. If one of
                // them IS the name being asked for, it wins outright — otherwise stripping "ต." off
                // an unambiguous input would report it as ambiguous between variants.
                var canonical = matches.Where(e => e.SubDistrictExact == looseKey).ToList();
                if (canonical.Count > 0) matches = canonical;
            }

            if (matches.Count == 0) return AddressResolution.NotFound();

            var narrowed = NarrowByParents(matches, p, d).ToList();

            // Parents that match nothing are more likely a typo in the file than a bad sub-district,
            // so report the sub-district's real parents as candidates rather than a bare "not found".
            if (narrowed.Count == 0)
                return AddressResolution.NotFound(matches.Take(MaxCandidates).Select(e => e.Dto).ToList());

            if (narrowed.Count == 1) return AddressResolution.Found(narrowed[0].Dto);

            // Same sub-district code repeated across rows is not a real ambiguity.
            var distinct = narrowed
                .GroupBy(e => e.Dto.SubDistrictCode)
                .Select(g => g.First())
                .ToList();

            return distinct.Count == 1
                ? AddressResolution.Found(distinct[0].Dto)
                : AddressResolution.Ambiguous(distinct.Take(MaxCandidates).Select(e => e.Dto).ToList());
        }

        private static List<Entry> Lookup(Dictionary<string, List<Entry>> map, string key)
            => key.Length > 0 && map.TryGetValue(key, out var list) ? list : [];

        private static IEnumerable<Entry> NarrowByParents(IEnumerable<Entry> source, string province, string district)
        {
            var result = source;

            if (province.Length > 0)
            {
                var pExact = Exact(province);
                var pLoose = Loose(province);
                result = result.Where(e =>
                    e.ProvinceExact == pExact ||
                    (pLoose.Length > 0 && e.ProvinceLoose == pLoose) ||
                    string.Equals(e.Dto.ProvinceCode, province, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Dto.ProvinceNameEn, province, StringComparison.OrdinalIgnoreCase));
            }

            if (district.Length > 0)
            {
                var dExact = Exact(district);
                var dLoose = Loose(district);
                result = result.Where(e =>
                    e.DistrictExact == dExact ||
                    (dLoose.Length > 0 && e.DistrictLoose == dLoose) ||
                    string.Equals(e.Dto.DistrictCode, district, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Dto.DistrictNameEn, district, StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }

        /// <summary>Trim, collapse runs of whitespace to a single space, lower-case. Keeps internal spaces.</summary>
        private static string Exact(string? value)
            => value is null ? string.Empty : Whitespace.Replace(value.Trim(), " ").ToLowerInvariant();

        /// <summary>Exact, then drop admin prefixes and parenthesised variants and remove every space.</summary>
        private static string Loose(string? value)
        {
            var text = Exact(value);
            if (text.Length == 0) return string.Empty;

            text = ParenSuffix.Replace(text, " ").Trim();

            // Repeat: "กิ่งอำเภอ" hides an "อำเภอ" behind it, and files carry "จ. กรุงเทพมหานคร" too.
            bool stripped;
            do
            {
                stripped = false;
                foreach (var prefix in Prefixes)
                {
                    if (!text.StartsWith(prefix, StringComparison.Ordinal) || text.Length <= prefix.Length) continue;
                    text = text[prefix.Length..].TrimStart();
                    stripped = true;
                    break;
                }
            } while (stripped);

            return Whitespace.Replace(text, string.Empty);
        }
    }
}
