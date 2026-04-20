using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.Dashboard.ReconcileAppraisalCounts;

public class ReconcileAppraisalCountsCommandHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<ReconcileAppraisalCountsCommand, ReconcileAppraisalCountsResult>
{
    public async Task<ReconcileAppraisalCountsResult> Handle(
        ReconcileAppraisalCountsCommand command,
        CancellationToken cancellationToken)
    {
        var today = dateTimeProvider.Today;
        var from = command.FromDate ?? today.AddDays(-30);
        var to = command.ToDate ?? today;

        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("From", from.ToDateTime(TimeOnly.MinValue));
        parameters.Add("To", to.ToDateTime(TimeOnly.MaxValue));

        var reconciledDays = await connection.ExecuteAsync("""
            MERGE common.DailyAppraisalCounts AS target
            USING (
                SELECT Date, CreatedCount, CompletedCount
                FROM common.vw_DailyAppraisalCountsFromSource
                WHERE Date >= @From AND Date <= @To
            ) AS source
            ON target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET
                    CreatedCount   = source.CreatedCount,
                    CompletedCount = source.CompletedCount,
                    LastUpdatedAt  = GETUTCDATE()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (Date, CreatedCount, CompletedCount, LastUpdatedAt)
                VALUES (source.Date, source.CreatedCount, source.CompletedCount, GETUTCDATE());
            """,
            parameters);

        return new ReconcileAppraisalCountsResult(reconciledDays, from, to);
    }
}
