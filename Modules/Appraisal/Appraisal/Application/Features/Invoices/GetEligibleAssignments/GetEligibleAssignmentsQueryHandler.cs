using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public class GetEligibleAssignmentsQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetEligibleAssignmentsQuery, IEnumerable<EligibleAssignmentDto>>
{
    public async Task<IEnumerable<EligibleAssignmentDto>> Handle(
        GetEligibleAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();

        // External (company) callers are pinned to their own company regardless of what the
        // request payload says; bank callers continue to pass the target company explicitly
        // (e.g. when composing an invoice on behalf of a specific company).
        var effectiveCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser) ?? request.CompanyId;

        var conditions = new List<string> { "v.AssigneeCompanyId = @CompanyId" };
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", effectiveCompanyId);
        parameters.Add("CurrentInvoiceId", request.CurrentInvoiceId);

        if (!string.IsNullOrWhiteSpace(request.SearchAppraisalNo))
        {
            conditions.Add("v.AppraisalNumber LIKE @SearchAppraisalNo");
            parameters.Add("SearchAppraisalNo", $"%{request.SearchAppraisalNo}%");
        }

        if (request.SubmittedDateFrom.HasValue)
        {
            conditions.Add("v.SubmittedDate >= @SubmittedDateFrom");
            parameters.Add("SubmittedDateFrom", request.SubmittedDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (request.SubmittedDateTo.HasValue)
        {
            conditions.Add("v.SubmittedDate <= @SubmittedDateTo");
            parameters.Add("SubmittedDateTo", request.SubmittedDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // Exclude assignments already on another invoice. The current draft (CurrentInvoiceId)
        // is exempt so its items stay visible/checked while editing.
        conditions.Add("""
            NOT EXISTS (
                SELECT 1 FROM appraisal.InvoiceItems ii
                WHERE ii.AssignmentId = v.AssignmentId
                  AND (@CurrentInvoiceId IS NULL OR ii.InvoiceId <> @CurrentInvoiceId)
            )
            """);

        var where = "WHERE " + string.Join(" AND ", conditions);
        var sql = $"""
            SELECT v.AssignmentId,
                   v.AppraisalFeeId,
                   v.AppraisalNumber,
                   v.CustomerName,
                   v.ProductType,
                   v.FeePaymentType,
                   v.FeeBeforeVAT,
                   v.VATRate,
                   v.VATAmount,
                   v.TotalFeeAfterVAT,
                   v.BankAbsorbAmount,
                   v.PayPartialAmount,
                   v.RemainingFee,
                   v.SubmittedDate,
                   v.LastPaymentDate
            FROM appraisal.vw_EligibleAssignments v
            {where}
            ORDER BY v.SubmittedDate DESC
            """;

        return await connection.QueryAsync<EligibleAssignmentDto>(sql, parameters);
    }
}
