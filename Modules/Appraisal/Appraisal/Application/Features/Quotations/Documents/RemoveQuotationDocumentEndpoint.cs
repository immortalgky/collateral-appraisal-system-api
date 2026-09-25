using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Quotations;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.Documents;

public class RemoveQuotationDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/quotations/{id:guid}/documents/{documentId:guid}", async (
                Guid id,
                Guid documentId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RemoveQuotationDocumentCommand(id, documentId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveQuotationDocument")
            .WithTags("Quotation")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record RemoveQuotationDocumentCommand(Guid QuotationRequestId, Guid DocumentId)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

public class RemoveQuotationDocumentCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUserService,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<RemoveQuotationDocumentCommand>
{
    public async Task<Unit> Handle(RemoveQuotationDocumentCommand command, CancellationToken ct)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUserService);

        var quotation = await quotationRepository.GetByIdWithDocumentsAsync(command.QuotationRequestId, ct)
            ?? throw new NotFoundException($"Quotation {command.QuotationRequestId} not found");

        quotation.RemoveDocument(command.DocumentId);

        // Publish unlink event so the Document module decrements ReferenceCount.
        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(
                RequestId: command.QuotationRequestId,   // owner id — quotation id reuses the RequestId field
                DocumentId: command.DocumentId),
            correlationId: command.QuotationRequestId.ToString());

        return Unit.Value;
    }
}
