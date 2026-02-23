using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.RemoveAppendixDocument;

public record RemoveAppendixDocumentCommand(
    Guid AppendixId,
    Guid DocumentId
) : ICommand<RemoveAppendixDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
