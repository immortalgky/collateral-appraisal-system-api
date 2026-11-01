namespace Auth.Contracts.Users;

/// <summary>
/// Cross-module contract for resolving user display information by username.
/// Implemented in the Auth module, consumed by other modules (e.g. Workflow)
/// that only hold a username string and need to show a human-friendly name.
/// </summary>
public interface IUserLookupService
{
    Task<IReadOnlyDictionary<string, UserLookupDto>> GetByUsernamesAsync(
        IEnumerable<string> usernames,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the usernames (bank codes) of all active users in the given role.
    /// When <paramref name="companyId"/> is provided, only users belonging to that company are returned.
    /// </summary>
    Task<string[]> GetUsernamesInRoleAsync(
        string role,
        Guid? companyId = null,
        CancellationToken ct = default);

    /// <summary>
    /// The emails of the users in <paramref name="role"/> who may use the system now (active, inside any access
    /// window), blank emails dropped.
    /// </summary>
    Task<IReadOnlyList<string>> GetEmailsInRoleAsync(string role, CancellationToken ct);

    /// <summary>The emails of those of <paramref name="usernames"/> who may use the system now, blank emails dropped.</summary>
    Task<IReadOnlyList<string>> GetEmailsAsync(IEnumerable<string> usernames, CancellationToken ct);

    /// <summary>
    /// The emails of <paramref name="username"/>'s supervisors: usable members of the groups that monitor (Group
    /// Monitoring) any group the user is in, de-duplicated. The IntAdmin group never counts as a supervisor group:
    /// it monitors everyone, and is reached through its own role instead.
    /// </summary>
    Task<IReadOnlyList<string>> GetSupervisorEmailsAsync(string username, CancellationToken ct);

    /// <summary>
    /// The display names (FirstName LastName, else the username) of the usable users holding <paramref name="emails"/>,
    /// keyed by address (case-insensitive). Addresses nobody usable holds are left out.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetNamesByEmailAsync(IEnumerable<string> emails, CancellationToken ct);

    /// <summary>
    /// Resolves a single user by their <paramref name="employeeId"/> (UserName / bank code, e.g. P5229)
    /// and returns their identity + org-structure snapshot (AO code, cost center, department).
    /// Returns null when the user does not exist or is inactive.
    /// </summary>
    Task<RequestorInfoDto?> GetRequestorAsync(string employeeId, CancellationToken ct = default);
}
