using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public record SaveAssignmentEmailAttachment(Guid DocumentId, string Source);

/// <summary>
/// Saves the appraisal's one email draft, separately from the assignment draft. There is no To field: the
/// company's address is the assigned company's and cannot be replaced; <paramref name="ExtraTo"/> only adds to it.
/// </summary>
public record SaveAssignmentEmailDraftCommand(
    Guid AppraisalId,
    List<string> ExtraTo,
    List<string> Cc,
    string Subject,
    string BodyHtml,
    List<SaveAssignmentEmailAttachment> Attachments
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

public class SaveAssignmentEmailDraftCommandHandler(
    IAssignmentEmailRepository emailRepository,
    IAssignmentEmailCaseReader caseReader,
    ICurrentUserService currentUser,
    IConfiguration configuration) : ICommandHandler<SaveAssignmentEmailDraftCommand>
{
    public async Task<Unit> Handle(SaveAssignmentEmailDraftCommand command, CancellationToken cancellationToken)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);
        // The same read GET fills the template from: it names the assignment and company the draft belongs to.
        var @case = await caseReader.GetCaseAsync(command.AppraisalId, cancellationToken)
                    ?? throw new AppraisalNotFoundException(command.AppraisalId);
        await EnsureAttachmentsFitAsync(command.Attachments, cancellationToken);

        var draft = await emailRepository.GetDraftAsync(command.AppraisalId, cancellationToken);
        if (draft is null)
        {
            draft = AssignmentEmail.CreateDraft(command.AppraisalId);
            await emailRepository.AddAsync(draft, cancellationToken);
        }

        draft.UpdateDraft(
            @case.AssignmentId,
            @case.AssigneeCompanyId,
            command.ExtraTo,
            command.Cc,
            command.Subject,
            command.BodyHtml,
            command.Attachments.DistinctBy(a => a.DocumentId)
                .Select(a => new AssignmentEmailAttachment(a.DocumentId, a.Source)));

        return Unit.Value;
    }

    // Sizes come from document.Documents, never from the client. Checked here so the admin learns now, not at send.
    private async Task EnsureAttachmentsFitAsync(List<SaveAssignmentEmailAttachment> attachments, CancellationToken ct)
    {
        if (attachments.Count == 0) return;

        var ids = attachments.Select(a => a.DocumentId).Distinct().ToList();
        var documents = await caseReader.GetDocumentsAsync(ids, ct);
        if (documents.Count != ids.Count)
            throw new BadRequestException("One or more attachments are not available documents. Remove them and attach again.");

        var limit = AssignmentEmailLimits.MaxAttachmentBytes(configuration);
        var total = documents.Sum(d => d.FileSizeBytes);
        if (total > limit)
            throw new BadRequestException(FormattableString.Invariant(
                $"Attachments total {total / 1024d / 1024:0.#} MB, over the {limit / 1024d / 1024:0.#} MB limit. Remove some files."));
    }
}

public class SaveAssignmentEmailDraftCommandValidator : AbstractValidator<SaveAssignmentEmailDraftCommand>
{
    public SaveAssignmentEmailDraftCommandValidator()
    {
        // A line break (control characters, U+2028/U+2029) would end the header at send, or inject one: refused now.
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(500)
            .Must(s => s is null || !s.Any(ch => char.IsControl(ch) || ch is '\u2028' or '\u2029'))
            .WithMessage("The subject must be a single line.");
        RuleFor(c => c.BodyHtml).NotEmpty();
        RuleForEach(c => c.ExtraTo).Must(IsPlainAddress)
            .WithMessage("'{PropertyValue}' is not a valid email address.");
        RuleForEach(c => c.Cc).Must(cc => AssignmentEmailCcRoles.All.Contains(cc) || IsPlainAddress(cc))
            .WithMessage("'{PropertyValue}' is not a valid email address or Cc role.");
        RuleFor(c => c.Attachments.Count).LessThanOrEqualTo(AssignmentEmailLimits.MaxAttachments)
            .WithMessage($"At most {AssignmentEmailLimits.MaxAttachments} attachments can be sent.");
        RuleForEach(c => c.Attachments).Must(a => a is not null && AssignmentEmailAttachmentSources.All.Contains(a.Source))
            .WithMessage("Attachment source must be request, appraisal or upload.");
    }

    // One bare address (no display name, no list), within the SMTP path limit.
    private static bool IsPlainAddress(string? value) =>
        value is { Length: <= 254 }
        && System.Net.Mail.MailAddress.TryCreate(value, out var address)
        && address.Address == value
        && string.IsNullOrEmpty(address.DisplayName);
}
