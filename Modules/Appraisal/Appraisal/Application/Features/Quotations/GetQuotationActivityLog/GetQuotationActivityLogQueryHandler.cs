using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.GetQuotationActivityLog;

public class GetQuotationActivityLogQueryHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetQuotationActivityLogQuery, List<QuotationActivityLogRow>>
{
    public async Task<List<QuotationActivityLogRow>> Handle(
        GetQuotationActivityLogQuery query,
        CancellationToken cancellationToken)
    {
        // ── Access control — mirror GetQuotationByIdQueryHandler ─────────────
        var quotation = await quotationRepository.GetByIdAsync(query.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{query.QuotationRequestId}' not found.");

        QuotationAccessPolicy.EnsureCanViewQuotation(quotation, quotation.RmUserId, currentUser);

        // ── Build query — ext-company callers see only their own company rows ─
        var isExtCaller = currentUser.IsInRole("ExtAdmin") || currentUser.IsInRole("ExtAppraisalChecker");

        string sql;
        var parameters = new DynamicParameters();
        parameters.Add("QuotationRequestId", query.QuotationRequestId);

        if (isExtCaller)
        {
            // Ext-company users see only rows where CompanyId matches their company
            // (RFQ-level rows with CompanyId IS NULL are excluded for ext users)
            sql = """
                SELECT Id, QuotationRequestId, CompanyQuotationId, CompanyId,
                       ActivityName, ActionAt, ActionBy, ActionByRole, Remark
                FROM appraisal.vw_QuotationActivityLog
                WHERE QuotationRequestId = @QuotationRequestId
                  AND CompanyId = @CallerCompanyId
                ORDER BY ActionAt ASC
                """;
            parameters.Add("CallerCompanyId", currentUser.CompanyId!.Value);
        }
        else
        {
            sql = """
                SELECT Id, QuotationRequestId, CompanyQuotationId, CompanyId,
                       ActivityName, ActionAt, ActionBy, ActionByRole, Remark
                FROM appraisal.vw_QuotationActivityLog
                WHERE QuotationRequestId = @QuotationRequestId
                ORDER BY ActionAt ASC
                """;
        }

        using var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<QuotationActivityLogRow>(sql, parameters);
        return rows.AsList();
    }
}
