using Dapper;
using FluentAssertions;
using Shared.Pagination;
using Workflow.Tasks.Features.GetMyTasks;
using Workflow.Tasks.Features.Shared;
using Xunit;

namespace Workflow.Tests.Tasks.Features.Shared;

public class TaskListProcParamsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData(" , ,, ", null)]
    [InlineData("LOS", "LOS")]
    [InlineData(" LOS ", "LOS")]
    [InlineData("LOS,Manual", "LOS,Manual")]
    [InlineData(" LOS , ,Manual, ", "LOS,Manual")]
    public void NormalizeList_TrimsItemsAndDropsEmpties(string? input, string? expected) =>
        TaskListFilterBuilder.NormalizeList(input).Should().Be(expected);

    [Fact]
    public void Build_NormalizesEveryMultiValueFilter()
    {
        var filter = new GetMyTasksFilterRequest(
            ActivityId: "int-appraisal-execution, appraisal-book-verification",
            Status: "InProgress,UnderReview",
            Channel: "LOS, Manual",
            SlaStatus: "Breached,,NoSla",
            Purpose: " 01 ,02");

        var p = TaskListProcParams.Build("1", ["john"], 0, null, filter, new PaginationRequest(0, 10));
        var g = TaskGroupCountsProcParams.Build("1", ["john"], 0, null, filter, "status");

        foreach (var parameters in new[] { p, g })
        {
            parameters.Get<string>("ActivityId").Should().Be("int-appraisal-execution,appraisal-book-verification");
            parameters.Get<string>("Status").Should().Be("InProgress,UnderReview");
            parameters.Get<string>("Channel").Should().Be("LOS,Manual");
            parameters.Get<string>("SlaStatus").Should().Be("Breached,NoSla");
            parameters.Get<string>("Purpose").Should().Be("01,02");
        }
    }

    [Fact]
    public void Build_SingleValueIsUnchanged_AndBlankBecomesNull()
    {
        var filter = new GetMyTasksFilterRequest(SlaStatus: "NoSla", Channel: "LOS", Status: " ");

        var p = TaskListProcParams.Build("1", ["john"], 0, null, filter, new PaginationRequest(0, 10));

        p.Get<string>("SlaStatus").Should().Be("NoSla");
        p.Get<string>("Channel").Should().Be("LOS");
        p.Get<string?>("Status").Should().BeNull();
        p.Get<string?>("ActivityId").Should().BeNull();
    }
}
