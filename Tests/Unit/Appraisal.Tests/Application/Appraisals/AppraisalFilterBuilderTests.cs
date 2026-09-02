using Appraisal.Application.Features.Appraisals.GetAppraisals;

namespace Appraisal.Tests.Application.Appraisals;

/// <summary>
/// Pins the WHERE clause the appraisal list builds, and — more importantly — the RequiresView flag.
///
/// RequiresView is a correctness gate, not a hint: when it is false the list handler counts and
/// counts straight off appraisal.Appraisals instead of the view. A filter field that reads a
/// column only the view synthesises (latest assignment, first land location, customer name,
/// latest appointment) but forgets to raise the flag would produce a WHERE referencing a column
/// the base table does not have — a runtime SQL error — or, worse, silently wrong totals if the
/// name happened to collide. These tests are the source of truth for which side each field is on.
/// </summary>
public class AppraisalFilterBuilderTests
{
    // ---------------------------------------------------------------------------
    // Fields that live on appraisal.Appraisals — the cheap path stays available
    // ---------------------------------------------------------------------------

    public static TheoryData<GetAppraisalsFilterRequest, string> BaseTableFilters => new()
    {
        { new GetAppraisalsFilterRequest(Status: "Pending"), "Status = @Statuses" },
        { new GetAppraisalsFilterRequest(Priority: "Normal"), "Priority = @Priorities" },
        { new GetAppraisalsFilterRequest(AppraisalType: "New"), "AppraisalType = @AppraisalTypes" },
        { new GetAppraisalsFilterRequest(SlaStatus: "OnTrack"), "SLAStatus = @SlaStatuses" },
        { new GetAppraisalsFilterRequest(Channel: "LOS"), "Channel = @Channel" },
        { new GetAppraisalsFilterRequest(BankingSegment: "RETAIL"), "BankingSegment = @BankingSegments" },
        { new GetAppraisalsFilterRequest(IsPma: true), "IsPma = @IsPma" },
        { new GetAppraisalsFilterRequest(CreatedFrom: new DateTime(2026, 1, 1)), "CreatedAt >= @CreatedFrom" },
        { new GetAppraisalsFilterRequest(SlaDueDateTo: new DateTime(2026, 1, 1)), "SLADueDate < DATEADD(day, 1, @SlaDueDateTo)" },
        { new GetAppraisalsFilterRequest { Purpose = "01" }, "Purpose = @Purposes" },
        { new GetAppraisalsFilterRequest { AppraisalNumber = "691" }, "AppraisalNumber LIKE '%' + @AppraisalNumber + '%'" },
        { new GetAppraisalsFilterRequest { RequestedAtFrom = new DateTime(2026, 1, 1) }, "RequestedAt >= @RequestedAtFrom" },
        { new GetAppraisalsFilterRequest { RequestedAtTo = new DateTime(2026, 1, 1) }, "RequestedAt < DATEADD(day, 1, @RequestedAtTo)" },
        // Free text is not in this table: it contributes no WHERE condition at all any more, only a
        // FROM. See Search_does_not_force_the_view below.
    };

    [Theory]
    [MemberData(nameof(BaseTableFilters))]
    public void Base_table_filters_emit_their_predicate_and_do_not_require_the_view(
        GetAppraisalsFilterRequest filter, string expectedFragment)
    {
        var result = AppraisalFilterBuilder.BuildFilter(filter);

        Assert.Contains(expectedFragment, result.WhereClause);
        Assert.False(result.RequiresView);
    }

    /// <summary>
    /// Free text used to be OR'ed against the view's CustomerName/RequestNumber and so forced the
    /// view. It joins on Id, which the base table has, so the count still runs off
    /// appraisal.Appraisals — the whole point of resolving the search over base tables.
    /// </summary>
    [Fact]
    public void Search_does_not_force_the_view()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "REQ-1"));

        Assert.False(result.RequiresView);
        Assert.Contains("JOIN appraisal.Appraisals t ON t.Id = s.AppraisalId", result.BaseTableFrom);
    }

    [Fact]
    public void PropertyType_filter_only_constrains_Id_so_it_stays_on_the_base_table()
    {
        // The semi-join reads AppraisalProperties/Projects, but its left-hand side is Id, which
        // appraisal.Appraisals has — so counting off the base table is still valid.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { PropertyType = "L,B" });

        Assert.Contains("Id IN (SELECT t.AppraisalId", result.WhereClause);
        Assert.False(result.RequiresView);
    }

    // ---------------------------------------------------------------------------
    // Fields that only the view can answer — the cheap path must be refused
    // ---------------------------------------------------------------------------

    public static TheoryData<GetAppraisalsFilterRequest> ViewOnlyFilters =>
    [
        new GetAppraisalsFilterRequest(AssignmentType: "Internal"),    // latest assignment
        new GetAppraisalsFilterRequest(AssigneeUserId: "P5229"),       // latest assignment
        new GetAppraisalsFilterRequest(AssigneeCompanyId: "acme"),     // latest assignment
        new GetAppraisalsFilterRequest(Province: "10"),                // first land location
        new GetAppraisalsFilterRequest(District: "1003"),              // first land location
        new GetAppraisalsFilterRequest(AssignedDateFrom: new DateTime(2026, 1, 1)),
        new GetAppraisalsFilterRequest(AppointmentDateTo: new DateTime(2026, 1, 1)),
        new GetAppraisalsFilterRequest { SubDistrict = "100301" },   // exact geocode, not a LIKE
    ];

    [Theory]
    [MemberData(nameof(ViewOnlyFilters))]
    public void View_only_filters_force_the_query_through_the_view(GetAppraisalsFilterRequest filter)
    {
        var result = AppraisalFilterBuilder.BuildFilter(filter);

        Assert.NotEqual(string.Empty, result.WhereClause);
        Assert.True(result.RequiresView);
    }

    [Fact]
    public void External_company_callers_are_always_scoped_and_therefore_always_need_the_view()
    {
        // AssigneeCompanyId comes off the latest assignment, so an external caller can never use
        // the base-table shortcut no matter how plain the rest of their filter is.
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest(Status: "Pending"), Guid.NewGuid());

        Assert.Contains("AssigneeCompanyId = @ScopedCompanyId", result.WhereClause);
        Assert.True(result.RequiresView);
    }

    [Fact]
    public void Caller_supplied_company_id_is_ignored_when_a_scope_is_enforced()
    {
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest(AssigneeCompanyId: "someone-elses-company"), Guid.NewGuid());

        Assert.Contains("@ScopedCompanyId", result.WhereClause);
        Assert.DoesNotContain("@AssigneeCompanyId", result.WhereClause);
    }

    // ---------------------------------------------------------------------------
    // Free-text search
    // ---------------------------------------------------------------------------

    [Fact]
    public void Search_matches_every_field_group_the_dropdown_offers()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai"));

        // The old implementation looked at three columns. Losing any of these silently makes a
        // whole class of term unfindable, which is exactly the bug this replaced.
        foreach (var field in new[]
                 {
                     "appraisalNumber", "requestNumber", "loanApplicationNumber", "prevAppraisalNumber",
                     "externalCaseKey", "customerName", "contactNumber", "contactPersonName",
                     "contactPersonPhone", "requestorName", "titleNumber", "landParcelNumber",
                     "roomNumber", "licensePlateNumber", "ownerName", "projectName", "condoName",
                 })
        {
            Assert.Contains($"'{field}'", result.SearchSource);
        }
    }

    [Fact]
    public void Search_binds_a_prefix_pattern_so_the_filtered_indexes_can_seek()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai"));

        Assert.Equal("somchai%", result.Parameters.Get<string>("SearchPattern"));
    }

    [Fact]
    public void Search_treats_a_star_as_the_users_opt_in_to_substring_matching()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "*somwong"));

        Assert.Equal("%somwong", result.Parameters.Get<string>("SearchPattern"));
    }

    [Fact]
    public void Search_escapes_LIKE_metacharacters_so_a_typed_percent_cannot_match_everything()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "100%"));

        // Without the escape this is a leading-and-trailing wildcard over every searched column.
        Assert.Equal("100\\%%", result.Parameters.Get<string>("SearchPattern"));
        Assert.Contains("ESCAPE '\\'", result.SearchSource);
    }

    [Fact]
    public void Search_shorter_than_the_minimum_matches_nothing_rather_than_everything()
    {
        // '69' is a prefix of every appraisal number in the system. Dropping the predicate would
        // return an unfiltered list that looks filtered.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "69"));

        Assert.Contains("1 = 0", result.WhereClause);
        Assert.False(result.RequiresView);
    }

    [Fact]
    public void Search_on_the_list_is_uncapped_so_the_page_and_export_agree()
    {
        // The dropdown caps each arm because it shows a handful of rows and re-runs on every
        // keystroke. The list must not: the same clause feeds /appraisals, /appraisals/export and
        // the quotation-eligible query, so a cap silently drops rows from a result set the user is
        // told is complete. Worse, the count and the page are separate executions
        // of this union with no ORDER BY inside a TOP, so each could keep a different subset —
        // totals that disagree with the page, and rows that repeat or vanish between pages.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "690"));

        Assert.DoesNotContain("TOP(", result.SearchSource);
        Assert.DoesNotContain("Cap", result.Parameters.ParameterNames);
    }

    [Fact]
    public void Search_excludes_soft_deleted_requests_as_well_as_soft_deleted_appraisals()
    {
        // An appraisal can be soft-deleted on its own, but a soft-deleted REQUEST whose appraisal
        // row is still live would otherwise leak its customer names, phone numbers and title deeds.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai"));

        var where = result.SearchSource;
        var armCount = where.Split("UNION ALL").Length;

        // Every arm checks the appraisal.
        Assert.Equal(armCount, where.Split("a.IsDeleted = 0").Length - 1);

        // Every arm that reads a request table also checks the request. The appraisal-number arm is
        // the one exception — it reads appraisal.Appraisals alone and has no request to check.
        var armsOverRequestTables = where.Split("FROM request.").Length - 1;
        Assert.Equal(armCount - 1, armsOverRequestTables);
        Assert.Equal(armsOverRequestTables, where.Split("r.IsDeleted = 0").Length - 1);
    }

    [Fact]
    public void Search_asks_for_a_per_execution_compile_and_nothing_else_does()
    {
        // The 17-way UNION of LIKE arms plans for a possible leading wildcard when compiled against
        // an unknown parameter, and scans. Every other filter is an equality or a range that caches
        // fine, so the hint is scoped to the one predicate that needs it.
        Assert.True(AppraisalFilterBuilder
            .BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai")).HasFreeTextSearch);

        Assert.False(AppraisalFilterBuilder
            .BuildFilter(new GetAppraisalsFilterRequest(Status: "Pending")).HasFreeTextSearch);

        // A term below the minimum degrades to `1 = 0`, which needs no hint either.
        Assert.False(AppraisalFilterBuilder
            .BuildFilter(new GetAppraisalsFilterRequest(Search: "69")).HasFreeTextSearch);
    }

    [Fact]
    public void Search_never_reads_the_view_so_soft_deleted_appraisals_stay_hidden()
    {
        // The view filters IsDeleted itself; the base tables do not, so every arm has to.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai"));

        Assert.DoesNotContain("vw_AppraisalList", result.SearchSource);
        Assert.DoesNotContain("a.IsDeleted = 1", result.SearchSource);
        Assert.Contains("a.IsDeleted = 0", result.SearchSource);
    }

    /// <summary>
    /// The search is a derived table joined in FRONT of the view, not an `Id IN (…)` predicate
    /// behind it, and the statement carries FORCE ORDER to keep it there.
    ///
    /// This is the one regression in this file that stays invisible: both shapes return exactly
    /// the same rows. Only the plan differs — as a sub-predicate the optimizer re-ran the 17-arm
    /// union once per view row whenever it thought the ORDER BY let it stop early, which turned a
    /// leading-wildcard search into a 10-second query (711k scans of request.RequestTitles for a
    /// 25-row page).
    /// </summary>
    [Fact]
    public void Search_joins_in_front_of_the_view_rather_than_filtering_behind_it()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Search: "somchai"));

        // The arms are out of the WHERE entirely.
        Assert.DoesNotContain("UNION ALL", result.WhereClause);
        Assert.DoesNotContain("Id IN (SELECT", result.WhereClause);

        Assert.StartsWith("(SELECT DISTINCT m.AppraisalId FROM (", result.SearchSource);
        Assert.StartsWith(result.SearchSource, result.ViewFrom);
        Assert.Contains("JOIN appraisal.vw_AppraisalList v ON v.Id = s.AppraisalId", result.ViewFrom);
        Assert.Contains("JOIN appraisal.Appraisals t ON t.Id = s.AppraisalId", result.BaseTableFrom);
        Assert.Equal(" OPTION (RECOMPILE, FORCE ORDER)", result.SearchQueryHint);
    }

    [Fact]
    public void Without_a_search_the_from_clause_is_the_bare_relation_and_carries_no_hint()
    {
        // FORCE ORDER is only correct because the search leads the join. With no search there is
        // nothing to force, and pinning a join order the optimizer never asked for would be a
        // pessimisation on every other filter.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Status: "Pending"));

        Assert.Equal("", result.SearchSource);
        Assert.Equal("appraisal.vw_AppraisalList v", result.ViewFrom);
        Assert.Equal("appraisal.Appraisals t", result.BaseTableFrom);
        Assert.Equal("", result.SearchQueryHint);
    }

    // ---------------------------------------------------------------------------
    // Shape of the clause itself
    // ---------------------------------------------------------------------------

    [Fact]
    public void No_filter_produces_an_empty_clause_that_still_excludes_deleted_rows_on_the_base_table()
    {
        var result = AppraisalFilterBuilder.BuildFilter(null);

        Assert.Equal(string.Empty, result.WhereClause);
        Assert.False(result.RequiresView);
        // The view carries `WHERE a.IsDeleted = 0`; the base table does not, so it must be added.
        Assert.Equal(" WHERE IsDeleted = 0", result.BaseTableWhereClause);
    }

    [Fact]
    public void Base_table_clause_appends_the_soft_delete_predicate_to_existing_conditions()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Status: "Pending"));

        Assert.StartsWith(" WHERE ", result.WhereClause);
        Assert.EndsWith(" AND IsDeleted = 0", result.BaseTableWhereClause);
        Assert.StartsWith(result.WhereClause, result.BaseTableWhereClause);
    }

    [Fact]
    public void Multiple_values_become_an_IN_list_and_a_single_value_stays_an_equality()
    {
        var single = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Status: "Pending"));
        var many = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Status: "Pending,Completed"));

        Assert.Contains("Status = @Statuses", single.WhereClause);
        Assert.Contains("Status IN @Statuses", many.WhereClause);
    }

    /// <summary>
    /// The filter bar lets these three be multi-selected like the enum filters. They used to be
    /// plain equalities, so this pins both halves: many values reach SQL as an IN list, and a
    /// single value still emits the equality that every existing link and saved search produces.
    /// </summary>
    [Theory]
    [InlineData("BankingSegment", "@BankingSegments", "RETAIL", "RETAIL,SME")]
    [InlineData("Province", "@Provinces", "10", "10,20")]
    [InlineData("AssigneeCompanyId", "@AssigneeCompanyIds", "acme", "acme,globex")]
    public void Segment_province_and_company_accept_several_values(
        string column, string parameter, string one, string two)
    {
        GetAppraisalsFilterRequest Filter(string value) => column switch
        {
            "BankingSegment" => new GetAppraisalsFilterRequest(BankingSegment: value),
            "Province" => new GetAppraisalsFilterRequest(Province: value),
            _ => new GetAppraisalsFilterRequest(AssigneeCompanyId: value)
        };

        var single = AppraisalFilterBuilder.BuildFilter(Filter(one));
        var many = AppraisalFilterBuilder.BuildFilter(Filter(two));

        Assert.Contains($"{column} = {parameter}", single.WhereClause);
        Assert.Contains($"{column} IN {parameter}", many.WhereClause);
    }

    [Fact]
    public void Several_provinces_still_force_the_view()
    {
        // RequiresView is raised by AddMultiValueFilter's return value now, not by a hand-written
        // if — an IN list must not silently drop back to counting off the base table.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Province: "10,20"));

        Assert.True(result.RequiresView);
    }

    /// <summary>
    /// The `*` prefix widens the ALL-FIELDS search, which is prefix-matched. The three pinned
    /// filters are already substring matches, and the value is escaped before it reaches SQL — so a
    /// `*` left in place would be searched for literally and find nothing. It is accepted and
    /// ignored instead of being explained.
    /// </summary>
    [Theory]
    [InlineData("*somchai", "somchai")]
    [InlineData("**somchai", "somchai")]
    [InlineData("  *somchai  ", "somchai")]
    [InlineData("somchai", "somchai")]
    public void Leading_widen_markers_are_stripped_from_pinned_searches(string typed, string expected)
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { CustomerName = typed });

        Assert.Contains("c.Name LIKE '%' + @CustomerName + '%'", result.WhereClause);
        Assert.Equal(expected, result.Parameters.Get<string>("CustomerName"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("***")]
    [InlineData("  *  ")]
    public void A_term_that_is_only_markers_filters_nothing(string typed)
    {
        // Not `LIKE '%%'`, which would return every row while looking like a search.
        var appraisal = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { AppraisalNumber = typed });
        var request = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { RequestNumber = typed });

        Assert.DoesNotContain("AppraisalNumber LIKE", appraisal.WhereClause);
        Assert.DoesNotContain("RequestNumber LIKE", request.WhereClause);
    }

    [Fact]
    public void Customer_search_covers_every_customer_on_the_request()
    {
        // The view surfaces one arbitrary customer per request (TOP 1, no ORDER BY). Filtering on
        // that column missed appraisals whose match is the second customer — 22 of them on the dev
        // data for "Jane" alone. The predicate must read the customer table, not the view column.
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { CustomerName = "jane" });

        Assert.Contains("request.RequestCustomers", result.WhereClause);
        Assert.DoesNotContain("CustomerName LIKE", result.WhereClause);
        Assert.False(result.RequiresView);
    }

    [Fact]
    public void Conditions_are_combined_with_AND()
    {
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest(Status: "Pending", Priority: "Normal"));

        Assert.Contains(" AND ", result.WhereClause);
    }

    // ---------------------------------------------------------------------------
    // Sorting
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("createdAt", "desc", "createdAt DESC, Id ASC")]   // the FE sends camelCase…
    [InlineData("CreatedAt", "desc", "CreatedAt DESC, Id ASC")]   // …while saved searches hold PascalCase
    [InlineData("customerName", "asc", "customerName ASC, Id ASC")]
    [InlineData(null, null, "CreatedAt DESC, Id ASC")]            // default
    [InlineData("; DROP TABLE x", null, "CreatedAt DESC, Id ASC")] // not whitelisted → default
    public void Sort_field_is_whitelisted_case_insensitively(string? sortBy, string? sortDir, string expected)
    {
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(
            new GetAppraisalsFilterRequest(SortBy: sortBy, SortDir: sortDir));

        Assert.Equal(expected, orderBy);
    }

    // ---------------------------------------------------------------------------
    // Single-column search — the fields behind the search-field selector
    // ---------------------------------------------------------------------------

    [Fact]
    public void Searching_only_the_appraisal_number_stays_on_the_base_table()
    {
        // This is the whole point of letting the caller name the column: `search` OR-s three
        // columns, two of which only the view has, so it always pays for the view. Pinning the
        // search to AppraisalNumber keeps the cheap COUNT off the base table.
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest { AppraisalNumber = "69105" });

        Assert.Contains("AppraisalNumber LIKE", result.WhereClause);
        Assert.False(result.RequiresView);
    }

    /// <summary>
    /// RequestNumber still needs the view — it lives on the LEFT JOIN to request.Requests, which
    /// only the view performs. CustomerName used to be here too and no longer is: it now reads
    /// request.RequestCustomers keyed by RequestId, a column the base table has.
    /// </summary>
    [Fact]
    public void Searching_request_number_needs_the_view()
    {
        var result = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest { RequestNumber = "REQ-1" });

        Assert.True(result.RequiresView);
    }

    // ---------------------------------------------------------------------------
    // LIKE metacharacters
    // ---------------------------------------------------------------------------

    public static TheoryData<string, string> LikeMetacharacters => new()
    {
        { "50%", @"50\%" },
        { "A_1", @"A\_1" },
        { "[x]", @"\[x]" },
        { @"back\slash", @"back\\slash" },
    };

    [Theory]
    [MemberData(nameof(LikeMetacharacters))]
    public void Single_column_search_escapes_like_metacharacters(string typed, string expected)
    {
        // Without this, looking for "50%" matches every row and "A_1" matches "A11".
        // The free-text `search` box is covered separately — it goes through
        // AppraisalSearchPredicate/LikePattern rather than building a LIKE here.
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest { CustomerName = typed });

        Assert.Equal(expected, result.Parameters.Get<string>("CustomerName"));
    }

    [Fact]
    public void SubDistrict_is_matched_exactly_because_it_holds_a_geocode()
    {
        // The column stores the 6-digit TIS-1099 code the address picker emits, not a name.
        // A substring match crosses provinces: '%1001%' hits 100101 (Bangkok) and 931001 too.
        var result = AppraisalFilterBuilder.BuildFilter(
            new GetAppraisalsFilterRequest { SubDistrict = "100101" });

        Assert.Contains("SubDistrict = @SubDistrict", result.WhereClause);
        Assert.DoesNotContain("SubDistrict LIKE", result.WhereClause);
    }

    [Theory]
    [InlineData("CustomerName")]
    [InlineData("AppraisalNumber")]
    [InlineData("RequestNumber")]
    public void Every_like_predicate_carries_an_escape_clause(string field)
    {
        // Escaping the value is only half of it — SQL Server ignores the backslash unless the
        // predicate says ESCAPE.
        var filter = field switch
        {
            "CustomerName" => new GetAppraisalsFilterRequest { CustomerName = "x" },
            "AppraisalNumber" => new GetAppraisalsFilterRequest { AppraisalNumber = "x" },
            _ => new GetAppraisalsFilterRequest { RequestNumber = "x" },
        };

        var result = AppraisalFilterBuilder.BuildFilter(filter);

        Assert.Contains("LIKE", result.WhereClause);
        Assert.Contains(@"ESCAPE '\'", result.WhereClause);
    }

    [Theory]
    // Business-time hours are not view columns; they are monotonic in the underlying timestamps.
    [InlineData("ElapsedHours", "asc", "CreatedAt DESC, Id ASC")]
    [InlineData("ElapsedHours", "desc", "CreatedAt ASC, Id ASC")]
    [InlineData("RemainingHours", "asc", "SLADueDate ASC, Id ASC")]
    [InlineData("RemainingHours", "desc", "SLADueDate DESC, Id ASC")]
    public void Computed_hour_sorts_are_translated_to_their_underlying_timestamp(
        string sortBy, string sortDir, string expected)
    {
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(
            new GetAppraisalsFilterRequest(SortBy: sortBy, SortDir: sortDir));

        Assert.Equal(expected, orderBy);
    }

    /// <summary>
    /// Every sort ends with the same unique tiebreaker. Without it the order of rows sharing a
    /// sort key is whatever the plan produces, and these columns tie heavily — rows 90-135 of
    /// SLADueDate DESC share ONE value. The same deep page requested twice came back with
    /// different rows, so paging could show a row twice and skip another.
    /// </summary>
    [Theory]
    [InlineData("SLAStatus")]
    [InlineData("Status")]
    [InlineData("Priority")]
    [InlineData("RemainingHours")]
    [InlineData(null)]
    public void Every_sort_ends_with_the_unique_tiebreaker(string? sortBy)
    {
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(
            new GetAppraisalsFilterRequest(SortBy: sortBy, SortDir: "desc"));

        Assert.EndsWith(", Id ASC", orderBy);
    }

    [Fact]
    public void Id_is_not_sortable_so_the_tiebreaker_can_never_be_repeated()
    {
        // "A column has been specified more than once in the order by list" is a runtime SQL
        // error, not a build one: if Id were ever whitelisted, `Id ASC, Id ASC` would 500 the
        // list and nothing here would catch it.
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(
            new GetAppraisalsFilterRequest(SortBy: "Id", SortDir: "asc"));

        // Falls back to the default field; the direction is still the caller's.
        Assert.Equal("CreatedAt ASC, Id ASC", orderBy);
    }
}
