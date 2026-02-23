using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.RemoveAppendixDocument;

public class RemoveAppendixDocumentCommandHandler(
    IAppraisalAppendixRepository repository
) : ICommandHandler<RemoveAppendixDocumentCommand, RemoveAppendixDocumentResult>
{
    public async Task<RemoveAppendixDocumentResult> Handle(
        RemoveAppendixDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var appendix = await repository.GetByIdWithDocumentsAsync(command.AppendixId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalAppendix), command.AppendixId);

        appendix.RemoveDocument(command.DocumentId);
        await repository.UpdateAsync(appendix, cancellationToken);

        return new RemoveAppendixDocumentResult(true);
    }
}
