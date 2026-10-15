using Appraisal.Application.Features.Appraisals.GetAppraisalGroups;
using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Appraisals;

public class GetAppraisalGroupsQueryHandlerTests
{
    [Theory]
    [InlineData("status", "Status", false)]
    [InlineData("slaStatus", "SLAStatus", false)]
    [InlineData("priority", "Priority", false)]
    [InlineData("province", "Province", true)]
    [InlineData("assignmentType", "AssignmentType", true)]
    [InlineData("STATUS", "Status", false)]      // case-insensitive
    [InlineData(" province ", "Province", true)] // trimmed
    public void GroupBy_maps_each_whitelisted_name_to_its_literal_column(string groupBy, string column, bool needsView)
    {
        Assert.Equal((column, needsView), GetAppraisalGroupsQueryHandler.ResolveGrouping(groupBy));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("customerName")]
    [InlineData("CreatedAt")]
    [InlineData("Status; DROP TABLE x")]
    [InlineData("Status, Priority")]
    [InlineData("1")]
    public void GroupBy_outside_the_whitelist_is_a_400(string? groupBy)
    {
        var ex = Assert.Throws<BadRequestException>(() => GetAppraisalGroupsQueryHandler.ResolveGrouping(groupBy));

        Assert.Contains("status", ex.Message);
    }

    [Fact]
    public void Base_table_columns_with_no_view_only_filter_read_the_base_table()
    {
        var filter = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Priority: "Normal"));

        var sql = GetAppraisalGroupsQueryHandler.BuildSql("Status", needsView: false, filter);

        Assert.Contains("FROM appraisal.Appraisals t WHERE Priority = @Priorities AND IsDeleted = 0", sql);
        Assert.DoesNotContain("vw_AppraisalList", sql);
    }

    [Fact]
    public void No_filter_at_all_still_excludes_deleted_appraisals()
    {
        var sql = GetAppraisalGroupsQueryHandler.BuildSql(
            "Status", needsView: false, AppraisalFilterBuilder.BuildFilter(null));

        Assert.Contains("FROM appraisal.Appraisals t WHERE IsDeleted = 0", sql);
    }

    [Fact]
    public void A_view_only_grouping_column_reads_the_view()
    {
        var sql = GetAppraisalGroupsQueryHandler.BuildSql(
            "Province", needsView: true, AppraisalFilterBuilder.BuildFilter(null));

        Assert.Contains("FROM appraisal.vw_AppraisalList v", sql);
        Assert.DoesNotContain("appraisal.Appraisals t", sql);
    }

    [Fact]
    public void A_view_only_filter_reads_the_view_even_for_a_base_table_grouping()
    {
        var filter = AppraisalFilterBuilder.BuildFilter(new GetAppraisalsFilterRequest(Province: "10"));

        var sql = GetAppraisalGroupsQueryHandler.BuildSql("Status", needsView: false, filter);

        Assert.Contains("FROM appraisal.vw_AppraisalList v WHERE Province = @Provinces", sql);
    }

    [Fact]
    public void Company_scope_forces_the_view_and_binds_the_company_as_a_parameter()
    {
        var companyId = Guid.NewGuid();
        var filter = AppraisalFilterBuilder.BuildFilter(null, companyId);

        var sql = GetAppraisalGroupsQueryHandler.BuildSql("Status", needsView: false, filter);

        Assert.Contains("vw_AppraisalList", sql);
        Assert.DoesNotContain(companyId.ToString(), sql);
    }

    [Fact]
    public void Where_clause_is_the_lists_own_so_the_counts_match_the_list()
    {
        var request = new GetAppraisalsFilterRequest(
            Status: "Completed", CreatedFrom: new DateTime(2026, 1, 1)) { AppraisalDateTo = new DateTime(2026, 6, 30) };
        var filter = AppraisalFilterBuilder.BuildFilter(request);

        var sql = GetAppraisalGroupsQueryHandler.BuildSql("Status", needsView: false, filter);

        Assert.Contains(filter.WhereClause, sql);
    }

    [Fact]
    public void Key_folds_null_and_empty_into_one_group_and_the_order_is_count_then_key()
    {
        var sql = GetAppraisalGroupsQueryHandler.BuildSql(
            "SLAStatus", needsView: false, AppraisalFilterBuilder.BuildFilter(null));

        Assert.Contains("ISNULL(NULLIF(SLAStatus, N''), N'') AS [Key]", sql);
        Assert.Contains("GROUP BY ISNULL(NULLIF(SLAStatus, N''), N'')", sql);
        Assert.Contains("ORDER BY COUNT(*) DESC, ISNULL(NULLIF(SLAStatus, N''), N'')", sql);
        Assert.Contains("COUNT(*) AS [Count]", sql);
    }

    [Fact]
    public void Breached_and_at_risk_skip_closed_appraisals_but_count_does_not()
    {
        var sql = GetAppraisalGroupsQueryHandler.BuildSql(
            "Status", needsView: false, AppraisalFilterBuilder.BuildFilter(null));

        Assert.Contains(
            "SUM(CASE WHEN SLAStatus = 'Breached' AND Status NOT IN ('Completed', 'Cancelled') THEN 1 ELSE 0 END) AS Breached", sql);
        Assert.Contains(
            "SUM(CASE WHEN SLAStatus = 'AtRisk' AND Status NOT IN ('Completed', 'Cancelled') THEN 1 ELSE 0 END) AS AtRisk", sql);
        Assert.Contains("COUNT(*) AS [Count]", sql); // every row in the group, closed or not
    }
}
