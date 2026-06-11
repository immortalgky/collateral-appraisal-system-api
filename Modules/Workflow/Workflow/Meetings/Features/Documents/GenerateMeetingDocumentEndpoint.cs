using FluentValidation;
using Workflow.Meetings.Application;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.Documents;

public record GenerateMeetingDocumentRequest(string DocumentType);

public class GenerateMeetingDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/documents/generate", async (
                Guid id,
                GenerateMeetingDocumentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new GenerateMeetingDocumentCommand(id, request.DocumentType);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("GenerateMeetingDocument")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<MeetingDocumentDto>();
    }
}

public record GenerateMeetingDocumentCommand(Guid MeetingId, string DocumentType)
    : ICommand<MeetingDocumentDto>, ITransactionalCommand<IWorkflowUnitOfWork>;

public class GenerateMeetingDocumentCommandValidator : AbstractValidator<GenerateMeetingDocumentCommand>
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Invitation", "Minute"
    };

    public GenerateMeetingDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("DocumentType must be 'Invitation' or 'Minute'.");
    }
}

public class GenerateMeetingDocumentCommandHandler(
    IMeetingRepository meetingRepository,
    IMeetingDocumentGenerator meetingDocumentGenerator)
    : ICommandHandler<GenerateMeetingDocumentCommand, MeetingDocumentDto>
{
    public async Task<MeetingDocumentDto> Handle(
        GenerateMeetingDocumentCommand command,
        CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var meetingDoc = await meetingDocumentGenerator.GenerateAndLinkAsync(
            meeting, command.DocumentType, ct);

        return new MeetingDocumentDto(
            meetingDoc.Id,
            meetingDoc.DocumentId,
            meetingDoc.FileName,
            meetingDoc.DocumentType,
            meetingDoc.Source,
            meetingDoc.CreatedBy,
            meetingDoc.CreatedAt,
            FileSizeBytes: null,
            MimeType: null);
    }
}
