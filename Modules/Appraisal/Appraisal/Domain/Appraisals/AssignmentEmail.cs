namespace Appraisal.Domain.Appraisals;

/// <summary>
/// One email to the external company an appraisal is assigned to (EXT / EXTO), written when the admin assigns:
/// <see cref="AssignmentEmailStatus.Pending"/> to be sent, or <see cref="AssignmentEmailStatus.NotSent"/> when the
/// admin assigned without sending. Sending, resending and failure add the other statuses. The company's own address
/// is never stored here: it is the assigned company's email, resolved when sent, so the email cannot go to a company
/// other than the one assigned. <see cref="ExtraTo"/> holds only the admin's additional addresses.
/// </summary>
public class AssignmentEmail : Entity<Guid>
{
    public const string CompanyPlaceholder = "{{company}}";
    public const string ResponsiblePlaceholder = "{{responsible}}";

    public Guid AppraisalId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public string Status { get; private set; } = AssignmentEmailStatus.Pending;
    public List<string> ExtraTo { get; private set; } = [];

    /// <summary>Role tokens (<see cref="AssignmentEmailCcRoles"/>), stored unresolved, plus literal addresses.</summary>
    public List<string> Cc { get; private set; } = [];

    public string Subject { get; private set; } = string.Empty;

    /// <summary>Admin-authored HTML, stored as given (same trust decision as the quotation email).</summary>
    public string BodyHtml { get; private set; } = string.Empty;

    public List<AssignmentEmailAttachment> Attachments { get; private set; } = [];

    /// <summary>Why the send failed, as Notification reported it (#624). The time is <c>UpdatedAt</c>.</summary>
    public string? FailureReason { get; private set; }

    private AssignmentEmail()
    {
        // For EF Core
    }

    /// <summary>The email the admin chose to send with the assignment, waiting for the send.</summary>
    public static AssignmentEmail ToSend(
        Guid appraisalId,
        Guid assignmentId,
        IEnumerable<string> extraTo,
        IEnumerable<string> cc,
        string subject,
        string bodyHtml,
        IEnumerable<AssignmentEmailAttachment> attachments) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AssignmentId = assignmentId,
            Status = AssignmentEmailStatus.Pending,
            ExtraTo = extraTo.ToList(),
            Cc = cc.ToList(),
            Subject = subject,
            BodyHtml = bodyHtml,
            Attachments = attachments.ToList()
        };

    /// <summary>The admin assigned without sending: only the choice is recorded, not what the dialog held.</summary>
    public static AssignmentEmail NotSent(Guid appraisalId, Guid assignmentId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AssignmentId = assignmentId,
            Status = AssignmentEmailStatus.NotSent
        };

    /// <summary>
    /// Hands the email to the sender: fills the template's <c>{{company}}</c> / <c>{{responsible}}</c> left open at
    /// assign, so the row keeps what was sent, and moves it to <see cref="AssignmentEmailStatus.Queued"/> so it is never
    /// picked for a later handover. Values are HTML-encoded: they come from master data, not from the admin.
    /// </summary>
    public void Queue(string? company, string? responsible)
    {
        BodyHtml = Fill(BodyHtml, CompanyPlaceholder, company);
        BodyHtml = Fill(BodyHtml, ResponsiblePlaceholder, responsible);
        Status = AssignmentEmailStatus.Queued;
    }

    private static string Fill(string html, string placeholder, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? html
            : html.Replace(placeholder, System.Net.WebUtility.HtmlEncode(value.Trim()), StringComparison.Ordinal);

    /// <summary>The gateway accepted it. Only a Queued email becomes Sent, so a redelivered confirmation changes nothing.</summary>
    public void MarkSent()
    {
        if (Status == AssignmentEmailStatus.Queued)
            Status = AssignmentEmailStatus.Sent;
    }

    /// <summary>The send gave up. Only a Queued email becomes Failed, so a redelivered report changes nothing.</summary>
    public void MarkFailed(string reason)
    {
        if (Status != AssignmentEmailStatus.Queued) return;
        Status = AssignmentEmailStatus.Failed;
        FailureReason = reason;
    }
}

/// <summary>A Document-module document attached to the email, and where the admin picked it from.</summary>
public record AssignmentEmailAttachment(Guid DocumentId, string Source);

public static class AssignmentEmailStatus
{
    /// <summary>Chosen at assign, waiting for the case to be handed over.</summary>
    public const string Pending = "Pending";

    /// <summary>Handed to the Notification module, waiting for its confirmation.</summary>
    public const string Queued = "Queued";

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
