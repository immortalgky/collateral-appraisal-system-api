using Shared.CQRS;

namespace Integration.Application.Features.Appraisals.GetAppraisalStatus;

public record GetAppraisalStatusQuery(string AppraisalNumber) : IQuery<GetAppraisalStatusResponse?>;

public record GetAppraisalStatusResponse(string AppraisalNumber, string Status, DateTime LastUpdatedAt);
