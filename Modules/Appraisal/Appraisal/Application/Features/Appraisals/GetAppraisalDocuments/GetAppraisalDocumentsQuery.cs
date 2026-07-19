using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public record GetAppraisalDocumentsQuery(
    Guid AppraisalId
) : IQuery<GetAppraisalDocumentsResult>;
