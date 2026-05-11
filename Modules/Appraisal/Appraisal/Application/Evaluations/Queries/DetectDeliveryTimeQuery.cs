using Dapper;

namespace Appraisal.Application.Evaluations.Queries;

/// <summary>
/// Auto-detects how long the external appraisal took by summing the elapsed
/// seconds across all ext-* workflow activities for the given appraisal.
/// Returns null when no relevant activity executions are found.
/// </summary>
public record DetectDeliveryTimeQuery(Guid AppraisalId)
    : IQuery<DetectDeliveryTimeResult?>;

public record DetectDeliveryTimeResult(decimal DetectedDays, int SuggestedRating);

public class DetectDeliveryTimeQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<DetectDeliveryTimeQuery, DetectDeliveryTimeResult?>
{
    private const string Sql = """
        SELECT CAST(DATEDIFF_BIG(second, MIN(wae.StartedOn), MAX(wae.CompletedOn)) AS DECIMAL(10, 6)) / 86400.0
        FROM   workflow.WorkflowActivityExecutions wae
        INNER JOIN workflow.WorkflowInstances wi ON wi.Id = wae.WorkflowInstanceId
        WHERE  wi.CorrelationId = @AppraisalId
          AND  wae.ActivityId LIKE 'ext-%'
          AND  wae.CompletedOn IS NOT NULL
          AND  wae.Status = 'Completed'
        """;

    public async Task<DetectDeliveryTimeResult?> Handle(
        DetectDeliveryTimeQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId.ToString());

        var totalDays = await connectionFactory.ExecuteScalarAsync<decimal?>(Sql, parameters);

        if (totalDays is null)
            return null;

        var days = totalDays.Value;
        var rating = days switch
        {
            < 2.0m  => 4,
            < 2.5m  => 3,
            < 3.5m  => 2,
            _       => 1
        };

        return new DetectDeliveryTimeResult(days, rating);
    }
}
