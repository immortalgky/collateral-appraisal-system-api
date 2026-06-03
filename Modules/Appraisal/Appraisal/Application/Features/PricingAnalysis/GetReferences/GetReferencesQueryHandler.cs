using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetReferences;

public class GetReferencesQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetReferencesQuery, GetReferencesResult>
{
    public async Task<GetReferencesResult> Handle(
        GetReferencesQuery query,
        CancellationToken cancellationToken)
    {
        var analyses = await repository.GetReferencesByAnchorAsync(
            query.SubjectType,
            query.AnchorId,
            query.AnchorRefKey,
            cancellationToken);

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

        return new GetReferencesResult(dtos);
    }
}
