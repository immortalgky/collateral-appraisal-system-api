using Appraisal.Application.Services;
using Appraisal.Application.Features.Appraisals.ResolveLatestInAppraisalChain;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

public class GetAssignmentDefaultsQueryHandler(
    AppraisalDbContext dbContext,
    IAssignmentDefaultsProvider assignmentDefaultsProvider,
    ICompanyLookupService companyLookup
) : IQueryHandler<GetAssignmentDefaultsQuery, GetAssignmentDefaultsResult>
{
    public async Task<GetAssignmentDefaultsResult> Handle(
        GetAssignmentDefaultsQuery query,
        CancellationToken cancellationToken)
    {
        // Projection only: query handlers must not load the aggregate just to read one field.
        var appraisal = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == query.AppraisalId)
            .Select(a => new { a.AppraisalType, a.PrevAppraisalId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var defaults = DefaultAssignmentResolver.Resolve(assignmentDefaultsProvider.Current, appraisal.AppraisalType);

        var previousCompany = appraisal.AppraisalType == AppraisalTypes.Progressive && appraisal.PrevAppraisalId is { } prevId
            ? await ResolvePreviousCompanyAsync(prevId, cancellationToken)
            : null;

        return defaults with { PreviousCompany = previousCompany };
    }

    // The picked prior's external company, by the election the chain resolution (and so the workflow's forced
    // company) uses — AppraisalCompanyElection — read in the SAME query as the prior's own facts. The company lookup
    // supplies the name and the MOU window, and yields nothing for a soft-deleted company.
    private async Task<PreviousCompanyDto?> ResolvePreviousCompanyAsync(Guid priorId, CancellationToken ct)
    {
        var elected = AppraisalCompanyElection.RawCompanyId(dbContext, priorId);

        // A soft-deleted prior has no row through the global filter; nothing to show then.
        var prior = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == priorId)
            .Select(a => new { a.AppraisalNumber, a.InspectionNumber, a.CompletedAt, RawCompanyId = elected.FirstOrDefault() })
            .FirstOrDefaultAsync(ct);

        if (prior is null || !Guid.TryParse(prior.RawCompanyId, out var companyId))
            return null;

        var company = await companyLookup.GetByIdAsync(companyId, ct);
        return company is null
            ? null
            : new PreviousCompanyDto(
                company.Id, company.Name, priorId, prior.AppraisalNumber, prior.InspectionNumber, prior.CompletedAt,
                company.IsAssignable);
    }
}
