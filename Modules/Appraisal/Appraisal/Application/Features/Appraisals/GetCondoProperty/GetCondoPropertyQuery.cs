using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetCondoProperty;

/// <summary>
/// Query to get a condo property with its detail
/// </summary>
public record GetCondoPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetCondoPropertyResult>;
