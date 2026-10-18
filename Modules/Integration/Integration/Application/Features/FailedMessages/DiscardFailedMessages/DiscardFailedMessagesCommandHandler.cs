using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Shared.CQRS;
using Shared.Identity;
using Shared.Time;

namespace Integration.Application.Features.FailedMessages.DiscardFailedMessages;

/// <summary>
/// Marks selected Pending or RetryRequested rows Discarded (RetryRequested is the way out for a row
/// stuck there because its owning node is dead). Shares its per-row loop with
/// RetryFailedMessagesCommandHandler via <see cref="FailedMessageBulkAction"/>.
/// </summary>
public class DiscardFailedMessagesCommandHandler(
    IntegrationDbContext dbContext,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DiscardFailedMessagesCommand, FailedMessageActionResult>
{
    public Task<FailedMessageActionResult> Handle(
        DiscardFailedMessagesCommand request, CancellationToken cancellationToken)
    {
        var actorCode = currentUser.UserCode
            ?? throw new UnauthorizedAccessException("A signed-in user is required to discard a failed message.");
        var now = dateTimeProvider.ApplicationNow;

        return FailedMessageBulkAction.ExecuteAsync(
            dbContext, request.Ids,
            (message, at) =>
            {
                // A RetryRequested row currently claimed by the collector (mid-publish) must not
                // be discarded out from under it — a claim older than the window is stale (owning node
                // dead or the publish failed silently) and does not block discard. Optimistic concurrency
                // (FailedMessage.RowVersion) still catches the narrow race where a claim lands between
                // this check and the save below; that lands on the existing "NotPending" branch.
                if (message.Status == Domain.FailedMessages.FailedMessageStatus.RetryRequested &&
                    message.RetryClaimedAt is { } claimedAt &&
                    at - claimedAt < Domain.FailedMessages.FailedMessageRetryClaimPolicy.StaleAfter)
                    return "Publishing";

                return message.Discard(actorCode, request.Reason, at) ? null : "NotPending";
            },
            FailedMessageAuditAction.Discard, actorCode, request.IpAddress, request.Reason, now, cancellationToken);
    }
}
