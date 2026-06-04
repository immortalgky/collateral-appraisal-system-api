using Appraisal.Application.Features.PricingAnalysis.GetReferences;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetGroupReferences;

public class GetGroupReferencesQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetGroupReferencesQuery, GetGroupReferencesResult>
{
    public async Task<GetGroupReferencesResult> Handle(
        GetGroupReferencesQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the group PA's method ids (its approaches → methods).
        var methodIds = await repository.GetMethodIdsByAnalysisIdAsync(
            query.PricingAnalysisId, cancellationToken);

        if (methodIds.Count == 0)
            return new GetGroupReferencesResult([]);

        // 2. Load every reference hosted by one of those methods (eager-loaded methods + final value).
        var analyses = await repository.GetReferencesByHostMethodIdsAsync(methodIds, cancellationToken);

        var dtos = analyses.Select(pa =>
        {
            var methods = pa.Approaches
                .SelectMany(a => a.Methods)
                .Select(m => new ReferenceMethodDto(
                    m.Id,
                    m.MethodType,
                    m.FinalValue?.FinalValueRounded,
                    m.FinalValue?.FinalValueAdjusted
                ))
                .ToList();

            return new ReferenceDto(
                pa.Id,
                pa.SubjectType,
                pa.AnchorId!.Value,
                pa.AnchorRefKey,
                pa.HostMethodId,
                pa.Status,
                methods
            );
        }).ToList();

        return new GetGroupReferencesResult(dtos);
    }
}
