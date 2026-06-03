using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetGroupReferences;

/// <summary>
/// Query to list ALL market-reference analyses belonging to a property group, i.e. every reference
/// PricingAnalysis whose HostMethodId is one of the methods in the group's PricingAnalysis
/// (identified by <paramref name="PricingAnalysisId"/>). Powers the group-level References section.
/// </summary>
public record GetGroupReferencesQuery(
    Guid PricingAnalysisId
) : IQuery<GetGroupReferencesResult>;
