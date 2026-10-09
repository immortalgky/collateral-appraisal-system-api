using Appraisal.Contracts.Appraisals;
using Shared.CQRS;

namespace Integration.Application.Features.Appraisals.GetAppraisalDocuments;

public record GetAppraisalDocumentsQuery(string AppraisalNumber) : IQuery<CarryForwardDocumentsResult?>;
