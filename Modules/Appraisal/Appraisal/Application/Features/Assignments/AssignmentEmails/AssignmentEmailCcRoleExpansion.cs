using Auth.Contracts.Users;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// Who a Cc role chip stands for. The one rule behind both the preview (which shows the people) and the send (which
/// mails them), so the admin checks the recipients that will receive it.
/// </summary>
internal static class AssignmentEmailCcRoleExpansion
{
    private const string IntAdminGroup = "IntAdmin";

    /// <summary>
    /// One address per entry, de-duplicated. A master-data address may hold several ("a; b"): split on ',' / ';' as
    /// the Notification module's EmailRecipients does, so an entry of separators only counts as no address.
    /// </summary>
    private static List<string> Split(IEnumerable<string?> addresses) =>
        addresses.SelectMany(a => (a ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// The parts of <paramref name="value"/> the validator accepts as an address: the one rule for what the preview shows
    /// and the send mails (company To, extra To, Cc), so junk in a master-data email is never an address and "Name <a@b>" is its bare address.
    /// </summary>
    public static List<string> PlainAddresses(IEnumerable<string?> value) =>
        Split(value).Select(Bare).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // A bare address as it is, or the address inside "Somchai <somchai@bank.co.th>". Anything else is dropped, never
    // rewritten: MailAddress would read "john smith@bank.co.th" as the display name "john" and the address smith@bank.co.th.
    private static string? Bare(string part)
    {
        if (AssignmentEmailInputValidator.IsPlainAddress(part)) return part;

        var open = part.LastIndexOf('<');
        if (open < 0 || !part.EndsWith('>')) return null;
        var inner = part[(open + 1)..^1].Trim();
        return AssignmentEmailInputValidator.IsPlainAddress(inner) ? inner : null;
    }

    /// <summary>True for a chip that needs the keyer / follow-up officer to be known.</summary>
    public static bool NeedsResponsible(string role) => role is AssignmentEmailCcRoles.Keyer
        or AssignmentEmailCcRoles.FollowupStaff or AssignmentEmailCcRoles.KeyerSupervisor or AssignmentEmailCcRoles.FollowupSupervisor;

    /// <summary>The addresses behind <paramref name="role"/>; keyer / follow-up chips name <paramref name="responsibleUserId"/>.</summary>
    public static async Task<IReadOnlyList<string>> EmailsAsync(
        IUserLookupService users, string role, string? responsibleUserId, CancellationToken ct) => role switch
    {
        AssignmentEmailCcRoles.IntAdminAll => await users.GetEmailsInGroupAsync(IntAdminGroup, ct),
        AssignmentEmailCcRoles.Keyer or AssignmentEmailCcRoles.FollowupStaff =>
            string.IsNullOrWhiteSpace(responsibleUserId) ? [] : await users.GetEmailsAsync([responsibleUserId], ct),
        AssignmentEmailCcRoles.KeyerSupervisor or AssignmentEmailCcRoles.FollowupSupervisor =>
            string.IsNullOrWhiteSpace(responsibleUserId) ? [] : await users.GetSupervisorEmailsAsync(responsibleUserId, ct),
        _ => []
    };
}
