namespace Shared.Identity;

/// <summary>
/// Lets a module that holds long-lived connections (SignalR) ask whether the browser sessions behind
/// them are still alive, without referencing the Auth module. Implemented by Auth.
/// </summary>
public interface ISessionStatusReader
{
    /// <summary>Of the given session ids, returns those that have ended or gone idle.</summary>
    Task<IReadOnlySet<Guid>> GetEndedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken);
}
