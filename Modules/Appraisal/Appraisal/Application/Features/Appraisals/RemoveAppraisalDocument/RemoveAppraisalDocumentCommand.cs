using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.RemoveAppraisalDocument;

public record RemoveAppraisalDocumentCommand(
    Guid AppraisalId,
    Guid Id
) : ICommand<RemoveAppraisalDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
