using Dapper;
using Shared.Data;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// The one place that decides what a free-text search term matches.
///
/// Two callers share it so the navbar quick-search and the appraisal list's search box can never
/// disagree about what a term finds: <see cref="GetAppraisals.AppraisalFilterBuilder"/> for the
/// list, and the QuickSearch handler for the dropdown.
///
/// Shape: one arm per searchable column, unioned, each producing
/// <c>(AppraisalId, Rnk, Fld, Val)</c>. Grouping to one row per appraisal and ranking happens in
/// the caller — the list only needs the ids, while the dropdown also needs to say *why* each row
/// matched.
///
/// Why it reads base tables and never <c>vw_AppraisalList</c>: the view resolves the latest
/// assignment, first land location, customer and latest appointment per row. Filtering it by a text
/// column means every one of those APPLYs runs before the predicate can reject anything. Measured on
/// 105k appraisals, the same count went 738 ms through the view's own columns versus 39 ms through
/// <c>Id IN (this)</c>.
/// </summary>
internal static class AppraisalSearchPredicate
{
    /// <summary>
    /// Below this a term is not selective enough to be worth running. Every appraisal number in the
    /// system shares its first two digits, so a 2-character query matches essentially every row.
    /// </summary>
    public const int MinTermLength = 3;

    /// <summary>
    /// Rows each arm may contribute in the dropdown. A term like <c>"690"</c> is a prefix of nearly
    /// every appraisal number, and without a cap the union materialises the whole table before the
    /// caller's TOP can discard it.
    ///
    /// It is <b>only</b> for the dropdown, which shows a handful of rows and is re-issued on every
    /// keystroke. Anything that presents a complete result set — the appraisal list, its export, the
    /// quotation-eligible query — must run uncapped: a cap there silently drops rows, and because
    /// the count, the page and the facets are three separate executions of the same union with no
    /// ORDER BY inside the TOP, each could keep a different subset.
    /// </summary>
    public const int DropdownArmCap = 200;

    // Rank orders the groups a user sees. Document numbers are what people paste, so they come
    // first; property identifiers last because a title deed is usually a deliberate lookup rather
    // than a guess. Exactness is layered on top by the caller (an exact hit beats a prefix hit).
    private const int RankDocument = 10;
    private const int RankCustomer = 20;
    private const int RankProperty = 30;

    /// <summary>
    /// Whether a scope can reach the address arms at all. Callers use this to skip the
    /// <see cref="IAddressNameSearch"/> round trip whose answer the scope filter would discard —
    /// this change exists to cut per-keystroke cost, so paying for a probe that cannot matter
    /// would work against its own point.
    /// </summary>
    public static bool ScopeCanMatchAddress(string? scope) =>
        string.IsNullOrEmpty(scope)
        || scope.Equals("all", StringComparison.OrdinalIgnoreCase)
        || scope.Equals("properties", StringComparison.OrdinalIgnoreCase);

    /// <summary>Field groups the caller can restrict the search to.</summary>
    public static readonly IReadOnlySet<string> Scopes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "all", "documents", "customers", "properties" };

    /// <summary>
    /// Which address master an arm resolves names against, or <see cref="AddressLevel.None"/> for
    /// an arm that is always in play. Only the levels the term actually names are emitted — see
    /// the remarks on <see cref="Build"/>.
    /// </summary>
    private sealed record Arm(string Scope, int Rank, string Field, string Sql,
        AddressLevel Level = AddressLevel.None);

    /// <summary>
    /// Every arm. <c>{P}</c> is replaced by the parameter name so the same text can be reused with
    /// a different pattern; <c>@Cap</c> bounds each arm.
    ///
    /// All of these join back through <c>RequestId</c>, which every table here is indexed on, and
    /// filter <b>both</b> <c>a.IsDeleted = 0</c> and <c>r.IsDeleted = 0</c>. Both are needed: an
    /// appraisal can be soft-deleted on its own, and a soft-deleted request whose appraisal row is
    /// still live would otherwise leak its customer names, phone numbers and title deeds back into
    /// search results.
    /// </summary>
    private static readonly Arm[] AllArms =
    [
        // ── Document numbers ────────────────────────────────────────────────────────────────
        new("documents", RankDocument, "appraisalNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'appraisalNumber' AS Fld, a.AppraisalNumber AS Val
            FROM appraisal.Appraisals a
            WHERE a.IsDeleted = 0 AND a.AppraisalNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "requestNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'requestNumber' AS Fld, r.RequestNumber AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.RequestNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "loanApplicationNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'loanApplicationNumber' AS Fld, d.LoanApplicationNumber AS Val
            FROM request.RequestDetails d
            JOIN request.Requests r ON r.Id = d.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.LoanApplicationNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "prevAppraisalNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'prevAppraisalNumber' AS Fld, d.PrevAppraisalNumber AS Val
            FROM request.RequestDetails d
            JOIN request.Requests r ON r.Id = d.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.PrevAppraisalNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "externalCaseKey", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'externalCaseKey' AS Fld, r.ExternalCaseKey AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.ExternalCaseKey LIKE {P} ESCAPE '\'
            """),

        // ── People ──────────────────────────────────────────────────────────────────────────
        // Deliberately not vw_AppraisalList.CustomerName: the view exposes only the FIRST customer
        // per request (TOP 1), so a second customer on the same request would be unfindable.
        new("customers", RankCustomer, "customerName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'customerName' AS Fld, c.Name AS Val
            FROM request.RequestCustomers c
            JOIN request.Requests r ON r.Id = c.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = c.RequestId AND a.IsDeleted = 0
            WHERE c.Name LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'contactNumber' AS Fld, c.ContactNumber AS Val
            FROM request.RequestCustomers c
            JOIN request.Requests r ON r.Id = c.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = c.RequestId AND a.IsDeleted = 0
            WHERE c.ContactNumber LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactPersonName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'contactPersonName' AS Fld, d.ContactPersonName AS Val
            FROM request.RequestDetails d
            JOIN request.Requests r ON r.Id = d.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.ContactPersonName LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactPersonPhone", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'contactPersonPhone' AS Fld, d.ContactPersonPhone AS Val
            FROM request.RequestDetails d
            JOIN request.Requests r ON r.Id = d.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.ContactPersonPhone LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "requestorName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'requestorName' AS Fld, r.RequestorName AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.RequestorName LIKE {P} ESCAPE '\'
            """),

        // ── Collateral ──────────────────────────────────────────────────────────────────────
        // request.RequestTitles is table-per-hierarchy: one row populates only its own branch's
        // columns, which is why each of these has its own filtered index rather than one wide one.
        new("properties", RankProperty, "titleNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'titleNumber' AS Fld, t.TitleNumber AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.TitleNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "landParcelNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'landParcelNumber' AS Fld, t.LandParcelNumber AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.LandParcelNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "roomNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'roomNumber' AS Fld, t.RoomNumber AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.RoomNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "licensePlateNumber", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'licensePlateNumber' AS Fld, t.LicensePlateNumber AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.LicensePlateNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "ownerName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'ownerName' AS Fld, t.OwnerName AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.OwnerName LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "projectName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'projectName' AS Fld, t.ProjectName AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.ProjectName LIKE {P} ESCAPE '\'
            """),
        // ── Property location ───────────────────────────────────────────────────────────────
        // Typed as a NAME ("กาญจนบุรี", "Kanchanaburi"), matched against a CODE column. The
        // address columns hold TIS-1099 geocodes, so the name is resolved to codes inside a
        // subquery over the master and the outer predicate stays an equality the optimizer can
        // seek — a LIKE joined straight onto the master costs 3.3x more (552 ms vs 168 ms
        // uncapped on 105k appraisals). It also means a term that is not an address name at all,
        // which is nearly every term, resolves to an empty code list and the arm returns in 0 ms
        // without touching the appraisal tables.
        //
        // Both master families are searched. They have genuinely diverged: 3,715 sub-district
        // codes exist only in Title, 7 only in Dopa, and 6 Thai sub-district names are Dopa-only —
        // searching one family alone would make those unfindable.
        //
        // Read from the appraisal's own detail rows, not request.RequestTitles, so that what is
        // searched is what the result row displays. Thai names only — nobody searches these by
        // their English name.
        //
        // Three sources, unioned inside one arm rather than split into three arms, because the
        // arm count is what the statement size — and therefore the per-execution compile cost —
        // tracks:
        // UNION, not UNION ALL: the pair (appraisal, geocode) is deduped, so an appraisal with
        // several parcels in the SAME area contributes one row instead of one per parcel —
        // duplicate badges on the client, and that many slots eaten out of TOP(@Cap). Parcels in
        // DIFFERENT areas that both match a prefix term still contribute a row each, which is
        // wanted: they are genuinely distinct matches and deserve distinct badges. So this is
        // one row per (appraisal, matched area), NOT one row per appraisal.
        //
        //   • LandAppraisalDetails  — land parcels; the overwhelming majority.
        //   • CondoAppraisalDetails — condo units carry their OWN address and are NOT reachable
        //     through the land table. A condo-only appraisal has no land row at all, so before
        //     this it could not be found by address name.
        //
        // NOT covered yet: block/project appraisals, which hold their address on
        // appraisal.Projects and appraisal.ProjectLands and have ZERO AppraisalProperties. Those
        // are Title-mastered too (the block form captures them with addressSource 'title', and
        // ProjectLands holds code 100907, which exists in parameter.TitleSubDistricts and in no
        // DOPA table) and neither table has Dopa* columns, so they would join the deed arms only.
        // Deliberately deferred — the header address is frequently NULL while the parcel carries
        // one, so covering them properly means reading both tables, and that is its own change.
        //
        // Building/Machinery/Vehicle/Vessel details carry no address columns: a building's
        // location is the parcel it stands on, which the land arm already covers.
        // Deed address (Title-mastered) and DOPA address (Dopa-mastered), three levels each.
        // Built from one template: the six differ only in field name, column, master pair and
        // which family resolves the display label — writing them out six times is how the
        // deed/DOPA COALESCE order drifted apart in the first place.
        AddressArm("province",        "Province",        "Provinces",    AddressLevel.Province,    dopaSourced: false),
        AddressArm("district",        "District",        "Districts",    AddressLevel.District,    dopaSourced: false),
        AddressArm("subDistrict",     "SubDistrict",     "SubDistricts", AddressLevel.SubDistrict, dopaSourced: false),
        AddressArm("dopaProvince",    "DopaProvince",    "Provinces",    AddressLevel.Province,    dopaSourced: true),
        AddressArm("dopaDistrict",    "DopaDistrict",    "Districts",    AddressLevel.District,    dopaSourced: true),
        AddressArm("dopaSubDistrict", "DopaSubDistrict", "SubDistricts", AddressLevel.SubDistrict, dopaSourced: true),



        new("properties", RankProperty, "condoName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'condoName' AS Fld, t.CondoName AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.CondoName LIKE {P} ESCAPE '\'
            """),
    ];

    /// <summary>
    /// One address arm. The six are structurally identical — same sources, same join, same
    /// predicate shape — and differ only in which column they read, which master pair resolves the
    /// name, and which family wins the display label. Emitting them from a template keeps that last
    /// difference explicit: a geocode is resolved against the master the capturing form used, so
    /// the deed columns read Title-first and the DOPA columns read Dopa-first. 102 district and 31
    /// sub-district codes present in the data carry a different NameTh in each family, so getting
    /// this backwards badges an address with a name no other consumer of it uses.
    ///
    /// The WHERE resolves the typed name to codes through a subquery rather than joining the
    /// master directly: the code list is tiny and the IN turns into a seek, where a LIKE joined
    /// onto the master costs 3.3x more (552 ms vs 168 ms uncapped on 105k appraisals). It also
    /// means a term that is not an address name at all resolves to an empty list and the arm
    /// returns without touching the appraisal tables — though Build now drops such arms outright.
    ///
    /// Both master families are searched in the WHERE regardless of which one owns the column.
    /// They have diverged: 3,715 sub-district codes exist only in Title, 7 only in Dopa, and a
    /// handful of Thai names are Dopa-only, so a prefix search for a name like 'นบพิตำ' reaches
    /// its deed rows only through the DOPA spelling.
    /// </summary>
    private static Arm AddressArm(
        string field, string column, string master, AddressLevel level, bool dopaSourced)
    {
        var (first, second) = dopaSourced ? ("Dopa", "Title") : ("Title", "Dopa");

        // $$ so that {TOP}/{R}/{P} stay literal placeholders for Build to substitute, and {{...}}
        // is the interpolation. A raw literal also leaves ESCAPE '\' alone — in a regular literal
        // that backslash would need doubling, and getting it wrong silently produces ESCAPE ''.
        return new Arm("properties", RankProperty, field, $$"""
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, '{{field}}' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.{{first}}{{master}} WHERE Code = lad.{{column}}),
                            (SELECT TOP 1 NameTh FROM parameter.{{second}}{{master}} WHERE Code = lad.{{column}}),
                            lad.{{column}}) AS Val
            FROM (SELECT ap.AppraisalId, l.{{column}}
                  FROM appraisal.LandAppraisalDetails l
                  JOIN appraisal.AppraisalProperties ap ON ap.Id = l.AppraisalPropertyId
                  UNION
                  SELECT ap.AppraisalId, c.{{column}}
                  FROM appraisal.CondoAppraisalDetails c
                  JOIN appraisal.AppraisalProperties ap ON ap.Id = c.AppraisalPropertyId
                  ) lad
            JOIN appraisal.Appraisals a ON a.Id = lad.AppraisalId AND a.IsDeleted = 0
            JOIN request.Requests r ON r.Id = a.RequestId AND r.IsDeleted = 0
            WHERE lad.{{column}} IN (
                SELECT Code FROM parameter.Title{{master}} WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.Dopa{{master}} WHERE NameTh LIKE {P} ESCAPE '\')
            """, level);
    }

    /// <summary>
    /// The UNION ALL of every arm in <paramref name="scope"/>, and the parameters it binds.
    /// Returns <c>null</c> when the term is too short to search on.
    /// </summary>
    /// <param name="armCap">
    /// Rows each arm may contribute, or <c>null</c> for no cap. Pass <see cref="DropdownArmCap"/>
    /// from the quick-search only; every caller that presents a complete result set must leave it
    /// null. See the remarks on <see cref="DropdownArmCap"/>.
    /// </param>
    /// <param name="address">
    /// Which address levels the term names, from <see cref="IAddressNameSearch"/>. Defaults to
    /// "none", which drops all six address arms — so a caller that does not resolve gets exactly
    /// the pre-address behaviour rather than a silent half-search.
    /// </param>
    public static (string Sql, DynamicParameters Parameters)? Build(
        string? term, string scope = "all", int? armCap = null, AddressNameMatch address = default)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinTermLength) return null;

        var arms = AllArms
            .Where(a => scope.Equals("all", StringComparison.OrdinalIgnoreCase)
                        || a.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase))
            // Address arms are emitted only when the term actually names a province/district/
            // sub-district. Every statement here carries OPTION (RECOMPILE), so it is re-compiled
            // on each keystroke and compilation cost tracks the size of the text: leaving all six
            // arms in unconditionally cost +86..119 ms on EVERY search, including "REQ-105" and
            // "691054", which can never match an address name. Measured three ways side by side
            // (7 arms / 10 / 13) on the same host, interleaved.
            .Where(a => address.Includes(a.Level))
            .ToList();
        if (arms.Count == 0) return null;

        var top = armCap.HasValue ? "TOP(@Cap) " : "";
        var sql = string.Join("\n            UNION ALL\n",
            arms.Select(a => a.Sql
                .Replace("{TOP}", top)
                .Replace("{P}", "@SearchPattern")
                .Replace("{R}", a.Rank.ToString())));

        var parameters = new DynamicParameters();
        if (armCap.HasValue) parameters.Add("Cap", armCap.Value);
        // Prefix by default (term%), substring only when the user types '*'. This is what lets the
        // filtered indexes on RequestTitles and RequestCustomers seek instead of scan.
        parameters.Add("SearchPattern", LikePattern.Build(trimmed));
        return (sql, parameters);
    }

    /// <summary>
    /// A predicate usable in a WHERE clause against either <c>appraisal.Appraisals</c> or
    /// <c>appraisal.vw_AppraisalList</c> — both expose <c>Id</c>. Written as <c>Id IN (…)</c>
    /// rather than a correlated EXISTS because the union already produces distinct appraisal ids
    /// and an EXISTS body would need every arm correlated separately.
    /// </summary>
    public static (string Sql, DynamicParameters Parameters)? BuildIdFilter(
        string? term, string scope = "all", AddressNameMatch address = default)
    {
        // Uncapped on purpose — see DropdownArmCap.
        var built = Build(term, scope, armCap: null, address);
        if (built is null) return null;
        var (sql, parameters) = built.Value;
        return ($"Id IN (SELECT DISTINCT m.AppraisalId FROM (\n{sql}\n) m)", parameters);
    }
}
