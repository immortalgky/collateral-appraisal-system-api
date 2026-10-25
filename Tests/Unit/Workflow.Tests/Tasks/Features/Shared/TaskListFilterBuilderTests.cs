using Dapper;
using FluentAssertions;
using global::Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;
using Workflow.Tasks.Features.Shared;
using Xunit;

namespace Workflow.Tests.Tasks.Features.Shared;

public class TaskListFilterBuilderTests
{
    private static (List<string> Conditions, DynamicParameters Parameters) Build(GetTasksFilterRequest filter)
    {
        var parameters = new DynamicParameters();
        return (TaskListFilterBuilder.BuildConditions(filter, parameters), parameters);
    }

    [Fact]
    public void BuildConditions_NoFilter_ReturnsNothing()
    {
        Build(new GetTasksFilterRequest()).Conditions.Should().BeEmpty();
        TaskListFilterBuilder.BuildConditions(null, new DynamicParameters()).Should().BeEmpty();
    }

    [Fact]
    public void BuildConditions_MultiValueFilters_BindOneNormalizedCsvParameter()
    {
        var (conditions, p) = Build(new GetTasksFilterRequest(
            Status: "InProgress, UnderReview",
            ActivityId: "a,,b",
            Channel: "LOS,Manual",
            Purpose: " 01 "));

        conditions.Should().Contain("Status IN (SELECT value FROM STRING_SPLIT(@Status, ','))");
        conditions.Should().Contain("ActivityId IN (SELECT value FROM STRING_SPLIT(@ActivityId, ','))");
        conditions.Should().Contain("Channel IN (SELECT value FROM STRING_SPLIT(@Channel, ','))");
        conditions.Should().Contain("Purpose IN (SELECT value FROM STRING_SPLIT(@Purpose, ','))");
        p.Get<string>("Status").Should().Be("InProgress,UnderReview");
        p.Get<string>("ActivityId").Should().Be("a,b");
        p.Get<string>("Channel").Should().Be("LOS,Manual");
        p.Get<string>("Purpose").Should().Be("01");
    }

    [Fact]
    public void BuildConditions_SlaStatus_NoSlaOnly_IsNull()
    {
        var (conditions, p) = Build(new GetTasksFilterRequest(SlaStatus: "NoSla"));

        conditions.Should().ContainSingle().Which.Should().Be("SlaStatus IS NULL");
        p.ParameterNames.Should().NotContain("SlaStatus");
    }

    [Fact]
    public void BuildConditions_SlaStatus_NoSlaMixedWithRealStatus_MatchesEither()
    {
        var (conditions, p) = Build(new GetTasksFilterRequest(SlaStatus: "Breached, NoSla"));

        conditions.Should().ContainSingle().Which.Should().Contain("IN (SELECT value FROM STRING_SPLIT(@SlaStatus, ','))")
            .And.Contain("OR SlaStatus IS NULL");
        p.Get<string>("SlaStatus").Should().Be("Breached");
    }

    [Fact]
    public void BuildConditions_SlaStatus_SingleRealStatus_IsPlainIn()
    {
        var (conditions, p) = Build(new GetTasksFilterRequest(SlaStatus: "AtRisk"));

        conditions.Should().ContainSingle().Which.Should().Be("SlaStatus IN (SELECT value FROM STRING_SPLIT(@SlaStatus, ','))");
        p.Get<string>("SlaStatus").Should().Be("AtRisk");
    }

    [Fact]
    public void BuildConditions_Search_UsesEscapedGlobPattern()
    {
        var (conditions, p) = Build(new GetTasksFilterRequest(Search: "100%_x", AppraisalNumber: "*69", CustomerName: "som"));

        conditions.Should().Contain(c => c.Contains("AppraisalNumber LIKE @Search ESCAPE") && c.Contains("CustomerName LIKE @Search ESCAPE"));
        p.Get<string>("Search").Should().Be(TaskListFilterBuilder.BuildSearchPattern("100%_x"));
        p.Get<string>("AppraisalNumber").Should().Be(TaskListFilterBuilder.BuildSearchPattern("*69"));
        p.Get<string>("CustomerName").Should().Be(TaskListFilterBuilder.BuildSearchPattern("som"));
    }

    [Fact]
    public void BuildConditions_AppointmentAndRequestedAtRanges_AreApplied()
    {
        var d = new DateTime(2026, 10, 1);
        var (conditions, p) = Build(new GetTasksFilterRequest(
            AppointmentDateFrom: d, AppointmentDateTo: d, RequestedAtFrom: d, RequestedAtTo: d));

        conditions.Should().Contain("AppointmentDateTime >= @AppointmentDateFrom");
        conditions.Should().Contain("AppointmentDateTime < DATEADD(day, 1, @AppointmentDateTo)");
        conditions.Should().Contain("RequestReceivedDate >= @RequestedAtFrom");
        conditions.Should().Contain("RequestReceivedDate < DATEADD(day, 1, @RequestedAtTo)");
        p.Get<DateTime>("RequestedAtTo").Should().Be(d);
    }

    [Theory]
    [InlineData("NotStarted", "PendingTaskStatus = 'Assigned'")]
    [InlineData("InProgress", "PendingTaskStatus IN ('InProgress','Completing')")]
    [InlineData("Overdue", "SlaStatus = 'Breached'")]
    [InlineData("notstarted", "PendingTaskStatus = 'Assigned'")]
    [InlineData(" OVERDUE ", "SlaStatus = 'Breached'")]
    [InlineData("Bogus", "1 = 0")]
    public void BuildConditions_TaskStatusBucket_MirrorsTheProc(string bucket, string expectedFragment) =>
        Build(new GetTasksFilterRequest(TaskStatusBucket: bucket)).Conditions
            .Should().ContainSingle().Which.Should().Contain(expectedFragment);

    [Theory]
    [InlineData("ReportReceivedAt", "CASE WHEN ReportReceivedAt IS NULL THEN 1 ELSE 0 END, ReportReceivedAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("RequestedAt", "RequestedAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("Channel", "Channel DESC, AssignedDate DESC, Id DESC")]
    [InlineData("AssignedDate", "AssignedDate DESC, Id DESC")]
    [InlineData("ElapsedHours", "AssignedDate ASC, Id DESC")]
    [InlineData("RemainingHours", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("elapsedHours", "AssignedDate ASC, Id DESC")]
    [InlineData("remainingHours", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("DueAt", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("customerName", "CustomerName DESC, AssignedDate DESC, Id DESC")]
    [InlineData("nonsense; DROP", "AssignedDate DESC, Id DESC")]
    public void ResolveOrderBy_AllowsViewColumnsAndRejectsUnknown(string sortBy, string expected) =>
        TaskListFilterBuilder.ResolveOrderBy(new GetTasksFilterRequest(SortBy: sortBy)).Should().Be(expected);

    [Fact]
    public void ResolveOrderBy_Asc_InvertsElapsedAndKeepsDirectionForOthers()
    {
        TaskListFilterBuilder.ResolveOrderBy(new GetTasksFilterRequest(SortBy: "elapsedHours", SortDir: "asc"))
            .Should().Be("AssignedDate DESC, Id DESC");
        TaskListFilterBuilder.ResolveOrderBy(new GetTasksFilterRequest(SortBy: "priority", SortDir: "asc"))
            .Should().Be("Priority ASC, AssignedDate DESC, Id DESC");
    }

    [Theory]
    [InlineData("dueAt", "asc", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt ASC, AssignedDate DESC, Id DESC")]
    [InlineData("dueAt", "desc", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("reportReceivedAt", "asc", "CASE WHEN ReportReceivedAt IS NULL THEN 1 ELSE 0 END, ReportReceivedAt ASC, AssignedDate DESC, Id DESC")]
    [InlineData("reportReceivedAt", "desc", "CASE WHEN ReportReceivedAt IS NULL THEN 1 ELSE 0 END, ReportReceivedAt DESC, AssignedDate DESC, Id DESC")]
    [InlineData("remainingHours", "asc", "CASE WHEN DueAt IS NULL THEN 1 ELSE 0 END, DueAt ASC, AssignedDate DESC, Id DESC")]
    public void ResolveOrderBy_DueDate_SortsNullsLastInBothDirections(string sortBy, string sortDir, string expected) =>
        TaskListFilterBuilder.ResolveOrderBy(new GetTasksFilterRequest(SortBy: sortBy, SortDir: sortDir))
            .Should().Be(expected);

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("notstarted", "NotStarted")]
    [InlineData(" INPROGRESS ", "InProgress")]
    [InlineData("overdue", "Overdue")]
    [InlineData(" Bogus ", "Bogus")]
    public void NormalizeBucket_CanonicalisesKnownValuesAndKeepsUnknown(string? input, string? expected) =>
        TaskListFilterBuilder.NormalizeBucket(input).Should().Be(expected);

    [Fact]
    public void ProcParams_NormalizeTaskStatusBucket()
    {
        var filter = new GetTasksFilterRequest(TaskStatusBucket: " overdue ");

        TaskListProcParams.Build("1", ["john"], 0, null, filter, new PaginationRequest(0, 10))
            .Get<string>("TaskStatusBucket").Should().Be("Overdue");
        TaskGroupCountsProcParams.Build("1", ["john"], 0, null, filter, "status")
            .Get<string>("TaskStatusBucket").Should().Be("Overdue");
    }

    [Fact]
    public void NormalizeList_CutsItemsTooLongToBeACode_SoTheyNeverWidenResults()
    {
        var tooLong = new string('x', 201);

        TaskListFilterBuilder.NormalizeList($"LOS,{tooLong}").Should().Be($"LOS,{new string('x', 200)}");
        TaskListFilterBuilder.NormalizeList(tooLong).Should().Be(new string('x', 200));
        TaskListFilterBuilder.NormalizeList(new string('x', 200)).Should().HaveLength(200);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" , ", null)]
    [InlineData("Breached", "Breached")]
    [InlineData("OnTrack", "OnTrack")]
    [InlineData("OnTime", "OnTime,OnTrack")]
    [InlineData("ontime", "ontime,OnTrack")]
    [InlineData("OnTime,OnTrack", "OnTime,OnTrack")]
    [InlineData("OnTrack,onTime", "OnTrack,onTime")]
    [InlineData("OnTime,OnTime", "OnTime,OnTrack")]
    [InlineData("Breached, OnTime ,NoSla", "Breached,OnTime,NoSla,OnTrack")]
    public void NormalizeSlaStatus_ExpandsOnTimeToOnTrack(string? input, string? expected) =>
        TaskListFilterBuilder.NormalizeSlaStatus(input).Should().Be(expected);

    [Fact]
    public void SlaStatusOnTime_ExpandsEverywhere()
    {
        var filter = new GetTasksFilterRequest(SlaStatus: "OnTime");

        TaskListProcParams.Build("1", ["john"], 0, null, filter, new PaginationRequest(0, 10))
            .Get<string>("SlaStatus").Should().Be("OnTime,OnTrack");
        TaskGroupCountsProcParams.Build("1", ["john"], 0, null, filter, "status")
            .Get<string>("SlaStatus").Should().Be("OnTime,OnTrack");

        var (conditions, p) = Build(filter);
        conditions.Should().ContainSingle().Which.Should().Be("SlaStatus IN (SELECT value FROM STRING_SPLIT(@SlaStatus, ','))");
        p.Get<string>("SlaStatus").Should().Be("OnTime,OnTrack");
    }
}
