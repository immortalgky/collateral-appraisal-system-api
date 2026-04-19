namespace Workflow.DocumentFollowups.Application.Commands;

public record SubmitFollowupAttachmentDto(
    Guid LineItemId,
    Guid DocumentId,
    string DocumentType,
    string FileName,
    bool AttachToRequest,
    Guid? TitleId);

public record SubmitDocumentFollowupCommand(
    Guid FollowupId,
    IReadOnlyList<SubmitFollowupAttachmentDto> Attachments)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;
