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
        // Read from appraisal.LandAppraisalDetails, not request.RequestTitles, so that what is
        // searched is what the result row displays. Thai names only — nobody searches these by
        // their English name.
        new("properties", RankProperty, "province", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'province' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleProvinces WHERE Code = lad.Province),
                            (SELECT TOP 1 NameTh FROM parameter.DopaProvinces  WHERE Code = lad.Province),
                            lad.Province) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.Province IN (
                SELECT Code FROM parameter.TitleProvinces WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaProvinces  WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.Province),
        new("properties", RankProperty, "district", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'district' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleDistricts WHERE Code = lad.District),
                            (SELECT TOP 1 NameTh FROM parameter.DopaDistricts  WHERE Code = lad.District),
                            lad.District) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.District IN (
                SELECT Code FROM parameter.TitleDistricts WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaDistricts  WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.District),
        new("properties", RankProperty, "subDistrict", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'subDistrict' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleSubDistricts WHERE Code = lad.SubDistrict),
                            (SELECT TOP 1 NameTh FROM parameter.DopaSubDistricts  WHERE Code = lad.SubDistrict),
                            lad.SubDistrict) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.SubDistrict IN (
                SELECT Code FROM parameter.TitleSubDistricts WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaSubDistricts  WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.SubDistrict),

        // The same three against the DOPA address, which is a different address held on the same
        // row. Separate arms rather than an OR: each predicate stays a clean equality, and the
        // match badge can say which of the two addresses actually matched.
        new("properties", RankProperty, "dopaProvince", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'dopaProvince' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleProvinces WHERE Code = lad.DopaProvince),
                            (SELECT TOP 1 NameTh FROM parameter.DopaProvinces WHERE Code = lad.DopaProvince),
                            lad.DopaProvince) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.DopaProvince IN (
                SELECT Code FROM parameter.TitleProvinces WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaProvinces WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.Province),
        new("properties", RankProperty, "dopaDistrict", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'dopaDistrict' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleDistricts WHERE Code = lad.DopaDistrict),
                            (SELECT TOP 1 NameTh FROM parameter.DopaDistricts WHERE Code = lad.DopaDistrict),
                            lad.DopaDistrict) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.DopaDistrict IN (
                SELECT Code FROM parameter.TitleDistricts WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaDistricts WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.District),
        new("properties", RankProperty, "dopaSubDistrict", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'dopaSubDistrict' AS Fld,
                   COALESCE((SELECT TOP 1 NameTh FROM parameter.TitleSubDistricts WHERE Code = lad.DopaSubDistrict),
                            (SELECT TOP 1 NameTh FROM parameter.DopaSubDistricts WHERE Code = lad.DopaSubDistrict),
                            lad.DopaSubDistrict) AS Val
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
            WHERE lad.DopaSubDistrict IN (
                SELECT Code FROM parameter.TitleSubDistricts WHERE NameTh LIKE {P} ESCAPE '\'
                UNION
                SELECT Code FROM parameter.DopaSubDistricts WHERE NameTh LIKE {P} ESCAPE '\')
            """, AddressLevel.SubDistrict),

        new("properties", RankProperty, "condoName", """
            SELECT {TOP}a.Id AS AppraisalId, {R} AS Rnk, 'condoName' AS Fld, t.CondoName AS Val
            FROM request.RequestTitles t
            JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.CondoName LIKE {P} ESCAPE '\'
            """),
    ];

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
