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
    Guid? AssignmentId,
    string? AssignmentMethod,
    string? AssigneeCompanyId,
    string? KeyerName,
    string? FollowupMethod,
    string? FollowupName,
    string? Remark);

public record AssignmentEmailDocument(Guid Id, string? FileName, long FileSizeBytes);

/// <summary>The cross-module reads behind the assignment email (request, fee, user and document tables).</summary>
public interface IAssignmentEmailCaseReader
{
    /// <summary>Null when the appraisal does not exist.</summary>
    Task<AssignmentEmailCase?> GetCaseAsync(Guid appraisalId, CancellationToken ct);

    /// <summary>The live (not deleted) documents among <paramref name="ids"/>.</summary>
    Task<IReadOnlyList<AssignmentEmailDocument>> GetDocumentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

public record AssignmentEmailAttachmentDto(Guid DocumentId, string Source, string? FileName, long FileSizeBytes);

/// <param name="Id">The saved draft; null when this is the template default.</param>
/// <param name="CompanyTo">The assigned company's address, locked; null while the company is not known yet.</param>
public record AssignmentEmailDraftResult(
    Guid? Id,
    string? CompanyTo,
    List<string> ExtraTo,
    List<string> Cc,
    string Subject,
    string BodyHtml,
    List<AssignmentEmailAttachmentDto> Attachments,
    int MaxAttachments,
    long MaxAttachmentBytes,
    DateTime? SavedAt);

/// <summary>Who may read and save the assignment email: the Administration tab's editors (IntAdmin, Admin).</summary>
internal static class AssignmentEmailAccess
{
    public const string Permission = "APPRAISAL_ADMINISTRATION_EDIT";

    public static void EnsureAllowed(ICurrentUserService user)
    {
        if (!user.HasPermission(Permission))
            throw new ForbiddenException("Only an appraisal administrator can draft the email to the company.");
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
