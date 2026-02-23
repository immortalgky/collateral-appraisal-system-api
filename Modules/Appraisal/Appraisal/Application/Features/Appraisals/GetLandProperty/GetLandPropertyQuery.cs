using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Query to get a land property by ID
/// </summary>
public record GetLandPropertyQuery(Guid AppraisalId, Guid PropertyId) : IQuery<GetLandPropertyResult>;
