using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Application.Services;
using Appraisal.Domain.Quotations;
using FluentValidation;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.Documents;

public record GenerateQuotationDocumentRequest(string DocumentType);

public class GenerateQuotationDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/quotations/{id:guid}/documents/generate", async (
                Guid id,
                GenerateQuotationDocumentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new GenerateQuotationDocumentCommand(id, request.DocumentType);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("GenerateQuotationDocument")
            .WithTags("Quotation")
            .RequireAuthorization()
            .Produces<QuotationDocumentDto>();
    }
}

public record GenerateQuotationDocumentCommand(Guid QuotationRequestId, string DocumentType)
    : ICommand<QuotationDocumentDto>, ITransactionalCommand<IAppraisalUnitOfWork>;

public class GenerateQuotationDocumentCommandValidator : AbstractValidator<GenerateQuotationDocumentCommand>
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Summary"
    };

    public GenerateQuotationDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("DocumentType must be 'Summary'.");
    }
}

public class GenerateQuotationDocumentCommandHandler(
    IQuotationRepository quotationRepository,
    IQuotationDocumentGenerator quotationDocumentGenerator,
    ICurrentUserService currentUser)
    : ICommandHandler<GenerateQuotationDocumentCommand, QuotationDocumentDto>
{
    public async Task<QuotationDocumentDto> Handle(
        GenerateQuotationDocumentCommand command,
        CancellationToken ct)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdWithDocumentsAsync(command.QuotationRequestId, ct)
            ?? throw new NotFoundException($"Quotation {command.QuotationRequestId} not found");

        var quotationDoc = await quotationDocumentGenerator.GenerateAndLinkAsync(
            quotation, command.DocumentType, ct);

        return new QuotationDocumentDto(
            quotationDoc.Id,
            quotationDoc.DocumentId,
            quotationDoc.FileName,
            quotationDoc.DocumentType,
            quotationDoc.Source,
            quotationDoc.CreatedBy,
            quotationDoc.CreatedAt,
            FileSizeBytes: null,
            MimeType: null);
    }
}
