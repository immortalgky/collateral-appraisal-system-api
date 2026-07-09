using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Dashboard.GetQuotationTaskSummary;

public class GetQuotationTaskSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetQuotationTaskSummaryQuery, GetQuotationTaskSummaryResult>
{
    public async Task<GetQuotationTaskSummaryResult> Handle(
        GetQuotationTaskSummaryQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (
                    SELECT COUNT(*)
                    FROM appraisal.Appraisals
                    WHERE Status = 'Pending'
                      AND IsDeleted = 0
                ) AS PendingQuotationCreation,
                (
                    SELECT COUNT(*)
                    FROM appraisal.QuotationRequests
                    WHERE Status = 'Sent'
                ) AS WaitingCompanySubmission,
                (
                    SELECT COUNT(*)
                    FROM appraisal.QuotationRequests
                    WHERE Status = 'PendingRmSelection'
                ) AS WaitingRmSelection,
                (
                    -- One Open FeeAppointmentApproval = one pending approver task
                    -- (may bundle a fee line and/or a reschedule line).
                    SELECT COUNT(*)
                    FROM workflow.FeeAppointmentApprovals
                    WHERE Status = 'Open'
                ) AS PendingApprovals
            """;

        var connection = connectionFactory.GetOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<GetQuotationTaskSummaryResult>(sql);

        return row ?? new GetQuotationTaskSummaryResult(0, 0, 0, 0);
    }
}
