using Auth.Infrastructure.Configuration;
using Auth.Domain.Identity;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>
/// Records the user's current password hash into history and stamps <c>PasswordChangedAt</c> after a
/// successful password set (register / change / reset). Old rows beyond the configured retention are
/// trimmed. Call AFTER the new password has been persisted so <c>user.PasswordHash</c> is the new hash.
/// </summary>
public interface IPasswordHistoryRecorder
{
    Task RecordAsync(ApplicationUser user, CancellationToken ct = default);
}

public class PasswordHistoryRecorder(
    AuthDbContext dbContext,
    IPasswordPolicyProvider policyProvider,
    IDateTimeProvider dateTimeProvider)
    : IPasswordHistoryRecorder
{
    public async Task RecordAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var now = dateTimeProvider.ApplicationNow;
        user.PasswordChangedAt = now;

        // LDAP accounts have only a throwaway local hash — don't track them.
        if (string.IsNullOrEmpty(user.PasswordHash) || AuthSources.IsLdap(user.AuthSource))
        {
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        dbContext.PasswordHistory.Add(new PasswordHistory
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(ct);

        var policy = await policyProvider.GetAsync(ct);
        if (policy.HistoryCount <= 0) return;

        // Keep the newest HistoryCount rows; drop the rest.
        var stale = await dbContext.PasswordHistory
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Skip(policy.HistoryCount)
            .ToListAsync(ct);

        if (stale.Count > 0)
        {
            dbContext.PasswordHistory.RemoveRange(stale);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
