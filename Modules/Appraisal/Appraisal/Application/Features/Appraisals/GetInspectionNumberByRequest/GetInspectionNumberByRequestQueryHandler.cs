using Appraisal.Contracts.Appraisals;

namespace Appraisal.Application.Features.Appraisals.GetInspectionNumberByRequest;

/// <summary>
/// Answers <see cref="GetInspectionNumberByRequestQuery"/>: one projected column, no aggregate load.
/// </summary>
public class GetInspectionNumberByRequestQueryHandler(AppraisalDbContext dbContext)
    : IRequestHandler<GetInspectionNumberByRequestQuery, int?>
{
    public async Task<int?> Handle(GetInspectionNumberByRequestQuery query, CancellationToken cancellationToken)
    {
        // Hoisted so EF parameterises the code.
        var cancelled = AppraisalStatus.Cancelled.Code;

        return await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.RequestId == query.RequestId && a.Status.Code != cancelled)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.InspectionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
