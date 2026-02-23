using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Query to get an Appraisal by ID
/// </summary>
public record GetAppraisalByIdQuery(Guid Id) : IQuery<GetAppraisalByIdResult>;