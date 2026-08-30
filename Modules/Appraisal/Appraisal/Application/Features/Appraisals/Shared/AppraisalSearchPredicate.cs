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
    /// Rows each arm may contribute. A term like <c>"69"</c> is a prefix of nearly every appraisal
    /// number, and without a cap the union materialises the whole table before the caller's TOP can
    /// discard it. Ranking stays correct for any term selective enough to be worth typing; for one
    /// that is not, the user is going to refine it anyway.
    /// </summary>
    private const int ArmCap = 200;

    // Rank orders the groups a user sees. Document numbers are what people paste, so they come
    // first; property identifiers last because a title deed is usually a deliberate lookup rather
    // than a guess. Exactness is layered on top by the caller (an exact hit beats a prefix hit).
    private const int RankDocument = 10;
    private const int RankCustomer = 20;
    private const int RankProperty = 30;

    /// <summary>Field groups the caller can restrict the search to.</summary>
    public static readonly IReadOnlySet<string> Scopes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "all", "documents", "customers", "properties" };

    private sealed record Arm(string Scope, int Rank, string Field, string Sql);

    /// <summary>
    /// Every arm. <c>{P}</c> is replaced by the parameter name so the same text can be reused with
    /// a different pattern; <c>@Cap</c> bounds each arm.
    ///
    /// All of these join back through <c>RequestId</c>, which every table here is indexed on, and
    /// filter <c>a.IsDeleted = 0</c> so a deleted appraisal cannot surface through any of them.
    /// </summary>
    private static readonly Arm[] AllArms =
    [
        // ── Document numbers ────────────────────────────────────────────────────────────────
        new("documents", RankDocument, "appraisalNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'appraisalNumber' AS Fld, a.AppraisalNumber AS Val
            FROM appraisal.Appraisals a
            WHERE a.IsDeleted = 0 AND a.AppraisalNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "requestNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'requestNumber' AS Fld, r.RequestNumber AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.RequestNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "loanApplicationNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'loanApplicationNumber' AS Fld, d.LoanApplicationNumber AS Val
            FROM request.RequestDetails d
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.LoanApplicationNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "prevAppraisalNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'prevAppraisalNumber' AS Fld, d.PrevAppraisalNumber AS Val
            FROM request.RequestDetails d
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.PrevAppraisalNumber LIKE {P} ESCAPE '\'
            """),
        new("documents", RankDocument, "externalCaseKey", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'externalCaseKey' AS Fld, r.ExternalCaseKey AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.ExternalCaseKey LIKE {P} ESCAPE '\'
            """),

        // ── People ──────────────────────────────────────────────────────────────────────────
        // Deliberately not vw_AppraisalList.CustomerName: the view exposes only the FIRST customer
        // per request (TOP 1), so a second customer on the same request would be unfindable.
        new("customers", RankCustomer, "customerName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'customerName' AS Fld, c.Name AS Val
            FROM request.RequestCustomers c
            JOIN appraisal.Appraisals a ON a.RequestId = c.RequestId AND a.IsDeleted = 0
            WHERE c.Name LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'contactNumber' AS Fld, c.ContactNumber AS Val
            FROM request.RequestCustomers c
            JOIN appraisal.Appraisals a ON a.RequestId = c.RequestId AND a.IsDeleted = 0
            WHERE c.ContactNumber LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactPersonName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'contactPersonName' AS Fld, d.ContactPersonName AS Val
            FROM request.RequestDetails d
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.ContactPersonName LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "contactPersonPhone", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'contactPersonPhone' AS Fld, d.ContactPersonPhone AS Val
            FROM request.RequestDetails d
            JOIN appraisal.Appraisals a ON a.RequestId = d.RequestId AND a.IsDeleted = 0
            WHERE d.ContactPersonPhone LIKE {P} ESCAPE '\'
            """),
        new("customers", RankCustomer, "requestorName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'requestorName' AS Fld, r.RequestorName AS Val
            FROM request.Requests r
            JOIN appraisal.Appraisals a ON a.RequestId = r.Id AND a.IsDeleted = 0
            WHERE r.IsDeleted = 0 AND r.RequestorName LIKE {P} ESCAPE '\'
            """),

        // ── Collateral ──────────────────────────────────────────────────────────────────────
        // request.RequestTitles is table-per-hierarchy: one row populates only its own branch's
        // columns, which is why each of these has its own filtered index rather than one wide one.
        new("properties", RankProperty, "titleNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'titleNumber' AS Fld, t.TitleNumber AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.TitleNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "landParcelNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'landParcelNumber' AS Fld, t.LandParcelNumber AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.LandParcelNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "roomNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'roomNumber' AS Fld, t.RoomNumber AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.RoomNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "licensePlateNumber", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'licensePlateNumber' AS Fld, t.LicensePlateNumber AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.LicensePlateNumber LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "ownerName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'ownerName' AS Fld, t.OwnerName AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.OwnerName LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "projectName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'projectName' AS Fld, t.ProjectName AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.ProjectName LIKE {P} ESCAPE '\'
            """),
        new("properties", RankProperty, "condoName", """
            SELECT TOP(@Cap) a.Id AS AppraisalId, {R} AS Rnk, 'condoName' AS Fld, t.CondoName AS Val
            FROM request.RequestTitles t
            JOIN appraisal.Appraisals a ON a.RequestId = t.RequestId AND a.IsDeleted = 0
            WHERE t.CondoName LIKE {P} ESCAPE '\'
            """),
    ];

    /// <summary>
    /// The UNION ALL of every arm in <paramref name="scope"/>, and the parameters it binds.
    /// Returns <c>null</c> when the term is too short to search on.
    /// </summary>
    public static (string Sql, DynamicParameters Parameters)? Build(string? term, string scope = "all")
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinTermLength) return null;

        var arms = AllArms
            .Where(a => scope.Equals("all", StringComparison.OrdinalIgnoreCase)
                        || a.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (arms.Count == 0) return null;

        var sql = string.Join("\n            UNION ALL\n",
            arms.Select(a => a.Sql.Replace("{P}", "@SearchPattern").Replace("{R}", a.Rank.ToString())));

        var parameters = new DynamicParameters();
        // Prefix by default (term%), substring only when the user types '*'. This is what lets the
        // filtered indexes on RequestTitles and RequestCustomers seek instead of scan.
        parameters.Add("SearchPattern", LikePattern.Build(trimmed));
        parameters.Add("Cap", ArmCap);
        return (sql, parameters);
    }

    /// <summary>
    /// A predicate usable in a WHERE clause against either <c>appraisal.Appraisals</c> or
    /// <c>appraisal.vw_AppraisalList</c> — both expose <c>Id</c>. Written as <c>Id IN (…)</c>
    /// rather than a correlated EXISTS because the union already produces distinct appraisal ids
    /// and an EXISTS body would need every arm correlated separately.
    /// </summary>
    public static (string Sql, DynamicParameters Parameters)? BuildIdFilter(string? term, string scope = "all")
    {
        var built = Build(term, scope);
        if (built is null) return null;
        var (sql, parameters) = built.Value;
        return ($"Id IN (SELECT DISTINCT m.AppraisalId FROM (\n{sql}\n) m)", parameters);
    }
}
