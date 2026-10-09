using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Shared.CQRS;
using Shared.Identity;
using Shared.Time;

namespace Integration.Application.Features.FailedMessages.RetryFailedMessages;

/// <summary>
/// Marks selected Pending rows RetryRequested; the owning node's collector performs the actual AMQP
/// republish (design D3/D10 step 3). Shares its per-row loop (load/transition/audit/concurrency) with
/// DiscardFailedMessagesCommandHandler via <see cref="FailedMessageBulkAction"/>.
/// </summary>
public class RetryFailedMessagesCommandHandler(
    IntegrationDbContext dbContext,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RetryFailedMessagesCommand, FailedMessageActionResult>
{
    public Task<FailedMessageActionResult> Handle(
        RetryFailedMessagesCommand request, CancellationToken cancellationToken)
    {
        var actorCode = currentUser.UserCode
            ?? throw new UnauthorizedAccessException("A signed-in user is required to retry a failed message.");
        var now = dateTimeProvider.ApplicationNow;

        return FailedMessageBulkAction.ExecuteAsync(
            dbContext, request.Ids,
            (message, at) =>
            {
                // Refuse a retry inside InboxGuard's stale-claim window rather than accept it and
                // have it silently no-op later — see FailedMessageRetryPolicy.
                if (message.Status == FailedMessageStatus.Pending &&
                    FailedMessageRetryPolicy.IsTooSoon(message.FaultedAt, message.CollectedAt, at))
                    return "TooSoon";

                return message.RequestRetry(actorCode, request.Reason, at) ? null : "NotPending";
            },
            FailedMessageAuditAction.Retry, actorCode, request.IpAddress, request.Reason, now, cancellationToken);
    }
}
