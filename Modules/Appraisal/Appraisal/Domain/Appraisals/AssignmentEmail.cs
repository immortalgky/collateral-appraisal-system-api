namespace Appraisal.Domain.Appraisals;

/// <summary>
/// One email to the external company an appraisal is assigned to (EXT / EXTO). While the admin prepares it the
/// row is the appraisal's single <see cref="AssignmentEmailStatus.Draft"/>; sending, resending and failure add the
/// other statuses. The company's own address is never stored here: it is the assigned company's email, resolved
/// when read or sent, so the email cannot go to a company other than the one assigned. <see cref="ExtraTo"/> holds
/// only the admin's additional addresses.
/// </summary>
public class AssignmentEmail : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public Guid? AssignmentId { get; private set; }

    /// <summary>
    /// The assignment's company when the draft was saved. A draft only describes the case for that assignment and
    /// that company: when either has changed, the template is offered instead.
    /// </summary>
    public string? CompanyId { get; private set; }
    public string Status { get; private set; } = AssignmentEmailStatus.Draft;
    public List<string> ExtraTo { get; private set; } = [];

    /// <summary>Role tokens (<see cref="AssignmentEmailCcRoles"/>), stored unresolved, plus literal addresses.</summary>
    public List<string> Cc { get; private set; } = [];

    public string Subject { get; private set; } = string.Empty;

    /// <summary>Admin-authored HTML, stored as given (same trust decision as the quotation email).</summary>
    public string BodyHtml { get; private set; } = string.Empty;

    public List<AssignmentEmailAttachment> Attachments { get; private set; } = [];

    private AssignmentEmail()
    {
        // For EF Core
    }

    public static AssignmentEmail CreateDraft(Guid appraisalId) =>
        new() { Id = Guid.CreateVersion7(), AppraisalId = appraisalId };

    public void UpdateDraft(
        Guid? assignmentId,
        string? companyId,
        IEnumerable<string> extraTo,
        IEnumerable<string> cc,
        string subject,
        string bodyHtml,
        IEnumerable<AssignmentEmailAttachment> attachments)
    {
        if (Status != AssignmentEmailStatus.Draft)
            throw new InvalidOperationException($"Only a draft email can be edited; this one is {Status}.");

        AssignmentId = assignmentId;
        CompanyId = companyId;
        ExtraTo = extraTo.ToList();
        Cc = cc.ToList();
        Subject = subject;
        BodyHtml = bodyHtml;
        Attachments = attachments.ToList();
    }
}

/// <summary>A Document-module document attached to the email, and where the admin picked it from.</summary>
public record AssignmentEmailAttachment(Guid DocumentId, string Source);

public static class AssignmentEmailStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string NotSent = "NotSent";
}

public static class AssignmentEmailAttachmentSources
{
    public const string Request = "request";
    public const string Appraisal = "appraisal";
    public const string Upload = "upload";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Request, Appraisal, Upload };
}

/// <summary>Cc role tokens, turned into addresses only when the email is sent.</summary>
public static class AssignmentEmailCcRoles
{
    public const string IntAdminAll = "IntAdminAll";
    public const string FollowupStaff = "FollowupStaff";
    public const string FollowupSupervisor = "FollowupSupervisor";
    public const string Keyer = "Keyer";
    public const string KeyerSupervisor = "KeyerSupervisor";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { IntAdminAll, FollowupStaff, FollowupSupervisor, Keyer, KeyerSupervisor };
}
