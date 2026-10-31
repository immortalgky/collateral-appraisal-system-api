namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// What the template needs from the case, read in one go across the request, fee and user tables.
/// Assignment fields are the appraisal's current assignment row (with the admin's assignment draft on it).
/// </summary>
public record AssignmentEmailCase(
    string? AppraisalNumber,
    string? CustomerName,
    string? ContactName,
    string? ContactPhone,
    string? FeePaymentText,
    string? AssignmentMethod,
    string? AssigneeCompanyId,
    string? KeyerName,
    string? FollowupMethod,
    string? FollowupName,
    string? Remark)
{
    /// <summary>EXTO: the bank engaged the company off-system.</summary>
    public bool IsOffline => AppraisalAssignment.IsOfflineMethod(AssignmentMethod);
}

public record AssignmentEmailDocument(Guid Id, string? FileName, long FileSizeBytes);

/// <summary>The cross-module reads behind the assignment email (request, fee, user and document tables).</summary>
public interface IAssignmentEmailCaseReader
{
    /// <summary>Null when the appraisal does not exist.</summary>
    Task<AssignmentEmailCase?> GetCaseAsync(Guid appraisalId, CancellationToken ct);

    /// <summary>The live (active, not deleted) documents among <paramref name="ids"/>.</summary>
    Task<IReadOnlyList<AssignmentEmailDocument>> GetDocumentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

public record AssignmentEmailAttachmentDto(Guid DocumentId, string Source, string? FileName, long FileSizeBytes);

/// <param name="CompanyTo">The assigned company's address, locked; null while the company is not known yet.</param>
public record AssignmentEmailPreviewResult(
    string? CompanyTo,
    List<string> ExtraTo,
    List<string> Cc,
    string Subject,
    string BodyHtml,
    List<AssignmentEmailAttachmentDto> Attachments,
    int MaxAttachments,
    long MaxAttachmentBytes);

/// <summary>Who may preview the assignment email and choose it at assign: the Administration tab's editors (IntAdmin, Admin).</summary>
internal static class AssignmentEmailAccess
{
    public const string Permission = "APPRAISAL_ADMINISTRATION_EDIT";

    public static void EnsureAllowed(ICurrentUserService user)
    {
        if (!user.HasPermission(Permission))
            throw new ForbiddenException("Only an appraisal administrator can prepare the email to the company.");
    }
}

/// <summary>Attachment limits; <c>Mail:MaxAttachmentBytes</c> is read on every call so it can change without a redeploy.</summary>
internal static class AssignmentEmailLimits
{
    public const int MaxAttachments = 10;
    public const long DefaultMaxAttachmentBytes = 10L * 1024 * 1024;

    public static long MaxAttachmentBytes(IConfiguration configuration) =>
        configuration.GetValue<long?>("Mail:MaxAttachmentBytes") ?? DefaultMaxAttachmentBytes;
}

/// <summary>
/// The email the admin edited in the assign dialog. There is no To field: the company's address is the assigned
/// company's and cannot be replaced; <paramref name="ExtraTo"/> only adds to it.
/// </summary>
public record AssignmentEmailInput(
    List<string>? ExtraTo,
    List<string>? Cc,
    string Subject,
    string BodyHtml,
    List<AssignmentEmailAttachmentInput>? Attachments);

public record AssignmentEmailAttachmentInput(Guid DocumentId, string Source);

/// <summary>What can be checked without the database; live documents and their total size are checked at assign.</summary>
public class AssignmentEmailInputValidator : AbstractValidator<AssignmentEmailInput>
{
    public AssignmentEmailInputValidator()
    {
        // A line break (control characters, U+2028/U+2029) would end the header at send, or inject one: refused now.
        RuleFor(e => e.Subject).NotEmpty().MaximumLength(500)
            .Must(s => s is null || !s.Any(ch => char.IsControl(ch) || ch is '\u2028' or '\u2029'))
            .WithMessage("The subject must be a single line, without tabs or other control characters.");
        RuleFor(e => e.BodyHtml).NotEmpty();
        RuleForEach(e => e.ExtraTo).Must(IsPlainAddress)
            .WithMessage("'{PropertyValue}' is not a valid email address.");
        RuleForEach(e => e.Cc).Must(cc => AssignmentEmailCcRoles.All.Contains(cc) || IsPlainAddress(cc))
            .WithMessage("'{PropertyValue}' is not a valid email address or Cc role.");
        // Counted as the assign attaches them: once per document.
        RuleFor(e => e.Attachments!.Where(a => a != null).Select(a => a.DocumentId).Distinct().Count())
            .LessThanOrEqualTo(AssignmentEmailLimits.MaxAttachments)
            .When(e => e.Attachments is not null)
            .WithMessage($"At most {AssignmentEmailLimits.MaxAttachments} attachments can be sent.");
        RuleForEach(e => e.Attachments).Must(a => a is not null && AssignmentEmailAttachmentSources.All.Contains(a.Source))
            .WithMessage("Attachment source must be request, appraisal or upload.");
    }

    // One bare address (no display name, no list), within the SMTP path limit.
    private static bool IsPlainAddress(string? value) =>
        value is { Length: <= 254 }
        && System.Net.Mail.MailAddress.TryCreate(value, out var address)
        && address.Address == value
        && string.IsNullOrEmpty(address.DisplayName);
}
