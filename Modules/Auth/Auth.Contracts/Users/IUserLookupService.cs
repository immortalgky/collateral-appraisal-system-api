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
    /// Resolves a single user by their <paramref name="employeeId"/> (UserName / bank code, e.g. P5229)
    /// and returns their identity + org-structure snapshot (AO code, cost center, department).
    /// Returns null when the user does not exist or is inactive.
    /// </summary>
    Task<RequestorInfoDto?> GetRequestorAsync(string employeeId, CancellationToken ct = default);
}
