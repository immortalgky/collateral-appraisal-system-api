using Auth.Contracts.Users;
using Microsoft.Extensions.Logging;

namespace Workflow.Workflow.Services;

internal static class UserDisplayName
{
    internal static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Best-effort batch lookup: a display name must never fail the request. Every failure of the lookup
    /// itself (any exception type, e.g. SqlClient's "Operation cancelled by user" on timeout) is logged
    /// and an empty map is returned, so callers fall back. The lookup gets a short time budget.
    /// Log levels: caller abort = Debug, timeout (our budget fired) = Warning, any other failure = Error
    /// (so a permanently broken lookup is visible instead of hiding behind the fallback).
    /// <para>
    /// <paramref name="honourCallerCancellation"/> decides what a cancelled caller token means.
    /// Read paths pass true: an aborted request stops with <see cref="OperationCanceledException"/>
    /// whatever exception the lookup raised. The post-commit complete-activity path passes false: the
    /// write is already committed, so even a caller abort just yields an empty map.
    /// </para>
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, UserLookupDto>> TryLookupAsync(
        IUserLookupService lookup, IEnumerable<string> usernames, ILogger logger, CancellationToken ct,
        bool honourCallerCancellation = true, TimeSpan? timeout = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? LookupTimeout);
        try
        {
            return await lookup.GetByUsernamesAsync(usernames, cts.Token);
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
            {
                if (honourCallerCancellation) throw new OperationCanceledException(ct);
                logger.LogDebug(ex, "Display-name lookup abandoned: caller cancelled; continuing without names");
            }
            else if (cts.IsCancellationRequested)
                logger.LogWarning(ex, "Display-name lookup timed out; continuing without names");
            else
                logger.LogError(ex, "Display-name lookup failed; continuing without names");

            return new Dictionary<string, UserLookupDto>();
        }
    }

    /// <summary>
    /// "First Last" for a user found by <see cref="IUserLookupService"/>, each part trimmed and blank
    /// parts skipped. Null whenever there is no usable name: blank/null username, user not found (or a
    /// group/pool code), or no non-blank name part.
    /// </summary>
    internal static string? Resolve(IReadOnlyDictionary<string, UserLookupDto> users, string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || !users.TryGetValue(username, out var user)) return null;
        var name = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrEmpty(p)));
        return name.Length > 0 ? name : null;
    }
}
