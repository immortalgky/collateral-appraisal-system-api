using Dapper;
using Shared.Data;

namespace Workflow.Services.Users;

/// <summary>
/// Resolves whether a username actually exists in <c>auth.AspNetUsers</c>.
///
/// Committee and meeting members are stored as usernames (<c>CommitteeMember.UserId</c> /
/// <c>MeetingMember.UserId</c>), and those usernames become the approval round's voters: they are
/// counted into the majority denominator and fanned out as PendingTasks. A member who does not
/// resolve to a real user can never vote, so the round's quorum/majority can become unreachable —
/// the appraisal then sits in Committee Approval with no error. Callers that accept a username
/// from the client check it here first.
/// </summary>
public interface IUserDirectory
{
    Task<bool> ExistsAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Returns the subset of <paramref name="usernames"/> that exist, compared case-insensitively
    /// (matching how member usernames are compared everywhere else in the approval path).
    /// </summary>
    Task<IReadOnlySet<string>> GetExistingAsync(IEnumerable<string> usernames, CancellationToken ct = default);
}

public class UserDirectory(ISqlConnectionFactory connectionFactory) : IUserDirectory
{
    public async Task<bool> ExistsAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        var existing = await GetExistingAsync([username], ct);
        return existing.Count > 0;
    }

    public async Task<IReadOnlySet<string>> GetExistingAsync(
        IEnumerable<string> usernames, CancellationToken ct = default)
    {
        var candidates = usernames
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const string sql = """
                           SELECT UserName
                           FROM auth.AspNetUsers
                           WHERE UserName IN @Usernames
                           """;

        var connection = connectionFactory.GetOpenConnection();
        var found = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { Usernames = candidates }, cancellationToken: ct));

        return found.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
