using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;

namespace Common.Application.Features.Logs.GetLogById;

public class GetLogByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetLogByIdQuery, LogDetailDto>
{
    public async Task<LogDetailDto> Handle(GetLogByIdQuery query, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    Id, TimeStamp, Level, Message, Exception, CorrelationId, EntityId, AppraisalId, RequestId,
    WorkflowInstanceId, CollateralId, DocumentId, MachineName, UserName,
    COALESCE(SourceContext, JSON_VALUE(Properties, '$.Properties.SourceContext')) AS SourceContext,
    COALESCE(RequestPath, JSON_VALUE(Properties, '$.Properties.RequestPath')) AS RequestPath,
    MessageTemplate, Properties
FROM dbo.Logs
WHERE Id = @Id";

        var connection = connectionFactory.GetOpenConnection();
        var log = await connection.QueryFirstOrDefaultAsync<LogDetailDto>(
            new CommandDefinition(sql, new { query.Id }, cancellationToken: cancellationToken));

        return log ?? throw new NotFoundException($"Log entry {query.Id} was not found.");
    }
}
