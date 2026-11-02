using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Data;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Features.GetEligibleAssignees;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Integration.Assignees;

/// <summary>
/// The workload shown beside each candidate in the assign dialog is one grouped count over workflow.PendingTasks, run
/// against the real schema so a wrong column or table name fails here. Rows are inserted raw and every user name is
/// unique per run, because the shared database is never cleaned between tests.
/// </summary>
[Collection("Integration")]
public class EligibleAssigneeTaskCountTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Each_candidate_carries_the_number_of_pending_tasks_assigned_to_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = Guid.NewGuid().ToString("N")[..8];
        string busy = $"busy{run}", light = $"light{run}", idle = $"idle{run}", other = $"other{run}";

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var connection = factory.GetOpenConnection();
        foreach (var user in new[] { busy, busy, busy, light, other, other })
            await SeedPendingTaskAsync(connection, user);

        var result = await Handler(factory, busy, light, idle)
            .Handle(new GetEligibleAssigneesQuery(Guid.NewGuid(), "appraisal-checker"), ct);

        Assert.Equal([3, 1, 0], result.EligibleAssignees.Select(a => a.ActiveTaskCount));
        Assert.Equal([busy, light, idle], result.EligibleAssignees.Select(a => a.UserName));
    }

    private static GetEligibleAssigneesQueryHandler Handler(ISqlConnectionFactory factory, params string[] candidates)
    {
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowInstance.Create(Guid.NewGuid(), "test-workflow", null, "system"));
        var pipeline = Substitute.For<IAssignmentPipeline>();
        pipeline.GetEligibleAssigneesAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(new AssignmentPipelineContext
            {
                CandidatePool = candidates.Select(c => new TeamMemberInfo(c, c, "team-1", [])).ToList()
            });

        return new GetEligibleAssigneesQueryHandler(
            instances, Substitute.For<IWorkflowDefinitionRepository>(), pipeline, Substitute.For<ITeamService>(), factory);
    }

    private static Task<int> SeedPendingTaskAsync(System.Data.IDbConnection connection, string assignedTo) =>
        connection.ExecuteAsync(
            """
            INSERT INTO workflow.PendingTasks
                (Id, CorrelationId, TaskName, TaskStatus, AssignedTo, AssignedType, AssignedAt, AssigneeAssignedAt,
                 WorkflowInstanceId, ActivityId, Movement)
            VALUES (@Id, @CorrelationId, N'Test Task', N'Assigned', @AssignedTo, N'1', SYSDATETIME(), SYSDATETIME(),
                    @WorkflowInstanceId, N'appraisal-checker', N'F')
            """,
            new { Id = Guid.CreateVersion7(), CorrelationId = Guid.NewGuid(), AssignedTo = assignedTo, WorkflowInstanceId = Guid.NewGuid() });
}
