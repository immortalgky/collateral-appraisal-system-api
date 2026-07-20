using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Appraisals.RemoveAppraisalDocument;

public class RemoveAppraisalDocumentCommandHandler(
    IAppraisalDocumentRepository repository,
    IIntegrationEventOutbox outbox
) : ICommandHandler<RemoveAppraisalDocumentCommand, RemoveAppraisalDocumentResult>
{
    public async Task<RemoveAppraisalDocumentResult> Handle(
        RemoveAppraisalDocumentCommand command,
        CancellationToken cancellationToken)
    {
        // Load by BOTH id AND appraisalId — the appendix endpoints have an IDOR bug where the
        // appraisalId route segment is unvalidated; this deliberately does not repeat that.
        var document = await repository.GetByIdAndAppraisalIdAsync(command.Id, command.AppraisalId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalDocument), command.Id);

        await repository.DeleteAsync(document, cancellationToken);

        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(command.AppraisalId, document.DocumentId));

        return new RemoveAppraisalDocumentResult(true);
    }
}
