using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalMapPins;

public record GetAppraisalMapPinsQuery(Guid AppraisalId) : IQuery<GetAppraisalMapPinsResult>;
