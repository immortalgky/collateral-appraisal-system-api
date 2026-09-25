using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Quotations;
using FluentValidation;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.Documents;

public record LinkQuotationDocumentRequest(Guid DocumentId, string FileName);

public class LinkQuotationDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/quotations/{id:guid}/documents", async (
                Guid id,
                LinkQuotationDocumentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new LinkQuotationDocumentCommand(id, request.DocumentId, request.FileName);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("LinkQuotationDocument")
            .WithTags("Quotation")
            .RequireAuthorization()
            .Produces<QuotationDocumentDto>();
    }
}

public record LinkQuotationDocumentCommand(Guid QuotationRequestId, Guid DocumentId, string FileName)
    : ICommand<QuotationDocumentDto>, ITransactionalCommand<IAppraisalUnitOfWork>;

public class LinkQuotationDocumentCommandValidator : AbstractValidator<LinkQuotationDocumentCommand>
{
    public LinkQuotationDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
    }
}

public class LinkQuotationDocumentCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<LinkQuotationDocumentCommand, QuotationDocumentDto>
{
    public async Task<QuotationDocumentDto> Handle(
        LinkQuotationDocumentCommand command,
        CancellationToken ct)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUserService);

        var quotation = await quotationRepository.GetByIdWithDocumentsAsync(command.QuotationRequestId, ct)
            ?? throw new NotFoundException($"Quotation {command.QuotationRequestId} not found");

        var userCode = currentUserService.UserCode
            ?? currentUserService.Username
            ?? "system";
        var now = dateTimeProvider.ApplicationNow;

        var data = new QuotationDocumentData(
            DocumentId: command.DocumentId,
            DocumentType: "Upload",
            FileName: command.FileName,
            Source: "Uploaded",
            CreatedBy: userCode,
            CreatedAt: now);

        var quotationDoc = quotation.AddDocument(data);

        // Publish link event so the Document module increments ReferenceCount.
        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                RequestId: command.QuotationRequestId,   // owner id — quotation id reuses the RequestId field
                DocumentId: command.DocumentId,
                DocumentType: "Upload"),
            correlationId: command.QuotationRequestId.ToString());

        return new QuotationDocumentDto(
            quotationDoc.Id,
            command.DocumentId,
            command.FileName,
            "Upload",
            "Uploaded",
            userCode,
            now,
            FileSizeBytes: null,
            MimeType: null);
    }
}
