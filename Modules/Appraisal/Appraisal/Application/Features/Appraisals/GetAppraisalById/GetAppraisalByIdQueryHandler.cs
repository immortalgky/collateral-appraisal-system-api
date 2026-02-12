using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Handler for getting an Appraisal by ID.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetAppraisalByIdQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalByIdQuery, GetAppraisalByIdResult>
{
    public async Task<GetAppraisalByIdResult> Handle(
        GetAppraisalByIdQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM appraisal.vw_AppraisalDetail WHERE Id = @Id";

        var result = await connectionFactory.QueryFirstOrDefaultAsync<GetAppraisalByIdResult>(
            sql,
            new { query.Id });

        if (result is null)
            throw new AppraisalNotFoundException(query.Id);

        return result;
    }
}
