using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Integration.Application.Features.FailedMessages;

/// <summary>
/// One per-row loop shared by the bulk retry and discard commands: load the rows, apply the
/// caller's transition, write one audit row per accepted row, and handle a rowversion concurrency
/// conflict the same way in both places.
/// </summary>
internal static class FailedMessageBulkAction
{
    /// <param name="tryTransition">
    /// Applies the domain transition for one row and returns the skip reason on failure, or null on
    /// success. Only "NotPending" carries the row's <see cref="FailedMessage.ActionBy"/>/ActionAt in the
    /// response (api-contract.md); any other reason (e.g. "TooSoon") omits them.
    /// </param>
    public static async Task<FailedMessageActionResult> ExecuteAsync(
        IntegrationDbContext dbContext,
        IReadOnlyList<Guid> ids,
        Func<FailedMessage, DateTime, string?> tryTransition,
        string auditAction,
        string actorCode,
        string? ipAddress,
        string? reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // A duplicate id would be processed twice: accepted the first time, then skipped NotPending.
        ids = ids.Distinct().ToList();

        var rows = await dbContext.FailedMessages
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var accepted = new List<Guid>();
        var skipped = new List<SkippedFailedMessage>();

        foreach (var id in ids)
        {
            if (!rows.TryGetValue(id, out var message))
            {
                skipped.Add(new SkippedFailedMessage(id, "NotFound", null, null));
                continue;
            }

            var skipReason = tryTransition(message, now);
            if (skipReason is not null)
            {
                skipped.Add(skipReason == "NotPending"
                    ? new SkippedFailedMessage(id, skipReason, message.ActionBy, message.ActionAt)
                    : new SkippedFailedMessage(id, skipReason, null, null));
                continue;
            }

            var auditLog = FailedMessageAuditLog.Create(
                auditAction, FailedMessageAuditSource.Consumer, message.Id, null, actorCode, ipAddress, reason, now);
            dbContext.FailedMessageAuditLogs.Add(auditLog);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                accepted.Add(id);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another actor changed this row between our read and our write — untrack the audit
                // log we would have written, reload the row's real current state, and report it.
                dbContext.FailedMessageAuditLogs.Remove(auditLog);
                await dbContext.Entry(message).ReloadAsync(cancellationToken);
                skipped.Add(new SkippedFailedMessage(id, "NotPending", message.ActionBy, message.ActionAt));
            }
        }

        return new FailedMessageActionResult(accepted, skipped);
    }
}
