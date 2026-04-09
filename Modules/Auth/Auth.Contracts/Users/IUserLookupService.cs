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
}
