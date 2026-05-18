using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Workflow.Tasks.Features.GetMonitorFilterOptions;

public record GetMonitorFilterOptionsResponse(
    List<string> TaskTypes,
    List<string> AppraisalStatuses);

public class GetMonitorFilterOptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/monitor/filter-options",
                async (
                    ISqlConnectionFactory connectionFactory,
                    CancellationToken cancellationToken) =>
                {
                    using var connection = connectionFactory.GetOpenConnection();

                    // Return ALL distinct task descriptions from currently-pending person-assigned
                    // tasks, regardless of the supervisor's monitored scope. The filter is meant
                    // to feel familiar (matches all task types they may encounter) even if some
                    // selections produce zero results for this supervisor.
                    const string sql = """
                        SELECT DISTINCT TaskDescription
                        FROM workflow.PendingTasks
                        WHERE TaskDescription IS NOT NULL
                          AND AssignedType = '1'
                        ORDER BY TaskDescription
                        """;

                    var taskTypes = (await connection.QueryAsync<string>(sql)).ToList();

                    // AppraisalStatus enum is small and well-known; hardcode rather than scanning the table.
                    var statuses = new List<string>
                    {
                        "Pending", "Assigned", "InProgress", "UnderReview", "Completed", "Cancelled"
                    };

                    return Results.Ok(new GetMonitorFilterOptionsResponse(taskTypes, statuses));
                }
            )
            .WithName("GetMonitorFilterOptions")
            .Produces<GetMonitorFilterOptionsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Filter dropdown options for the task monitor")
            .WithDescription(
                "Returns distinct task-type descriptions (scoped to the supervisor's monitored groups) and the " +
                "fixed list of appraisal statuses for the drill-down filter card.")
            .WithTags("Tasks")
            .RequireAuthorization("task-monitor.view");
    }
}
