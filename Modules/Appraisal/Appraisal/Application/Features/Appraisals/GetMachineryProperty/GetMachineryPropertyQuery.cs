using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetMachineryProperty;

/// <summary>
/// Query to get a machinery property with its detail
/// </summary>
public record GetMachineryPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetMachineryPropertyResult>;
