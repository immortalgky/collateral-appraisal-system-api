using Shared.Data;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksCommandHandler(ISqlConnectionFactory connection)
    : ICommandHandler<GetTasksCommand, PaginatedResult<TaskItem>>
{
    public async Task<PaginatedResult<TaskItem>> Handle(GetTasksCommand command,
        CancellationToken cancellationToken)
    {
        var sql = """
                      SELECT * FROM workflow.vw_TaskList
                  """;

        var result = await connection.QueryPaginatedAsync<TaskItem>(
            sql,
            "RequestedAt DESC",
            new PaginationRequest(0, 10)
        );

        return result;
    }
}