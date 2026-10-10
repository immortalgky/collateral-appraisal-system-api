using Appraisal.Contracts.Appraisals;
using Shared.CQRS;

namespace Integration.Application.Features.Appraisals.GetCarryForwardDocumentsByNumber;

public record GetCarryForwardDocumentsByNumberQuery(string AppraisalNumber) : IQuery<CarryForwardDocumentsResult?>;
