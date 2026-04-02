namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreement;

public record GetLeaseAgreementQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetLeaseAgreementResult>;
