using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.Dashboard.ReconcileCompanyAppraisalSummaries;

public class ReconcileCompanyAppraisalSummariesCommandHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<ReconcileCompanyAppraisalSummariesCommand, ReconcileCompanyAppraisalSummariesResult>
{
    public async Task<ReconcileCompanyAppraisalSummariesResult> Handle(
        ReconcileCompanyAppraisalSummariesCommand command,
        CancellationToken cancellationToken)
    {
        var today = dateTimeProvider.Today;
        var from = command.FromDate ?? today.AddDays(-30);
        var to = command.ToDate ?? today;

        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("From", from.ToDateTime(TimeOnly.MinValue));
        parameters.Add("To", to.ToDateTime(TimeOnly.MaxValue));

        var reconciledRows = await connection.ExecuteAsync("""
            MERGE common.CompanyAppraisalSummaries AS target
            USING (
                SELECT CompanyId, [Date], CompanyName, AssignedCount, CompletedCount
                FROM common.vw_CompanyAppraisalSummariesFromSource
                WHERE [Date] >= @From AND [Date] <= @To
            ) AS source
            ON target.CompanyId = source.CompanyId AND target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET
                    CompanyName    = source.CompanyName,
                    AssignedCount  = source.AssignedCount,
                    CompletedCount = source.CompletedCount,
                    LastUpdatedAt  = GETUTCDATE()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (CompanyId, Date, CompanyName, AssignedCount, CompletedCount, LastUpdatedAt)
                VALUES (source.CompanyId, source.Date, source.CompanyName, source.AssignedCount, source.CompletedCount, GETUTCDATE())
            WHEN NOT MATCHED BY SOURCE AND target.Date >= @From AND target.Date <= @To THEN
                DELETE;
            """,
            parameters);

        return new ReconcileCompanyAppraisalSummariesResult(reconciledRows, from, to);
    }
}
