using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Query to get a property group by ID
/// </summary>
public record GetPropertyGroupByIdQuery(
    Guid AppraisalId,
    Guid GroupId
) : IQuery<GetPropertyGroupByIdResult>;
