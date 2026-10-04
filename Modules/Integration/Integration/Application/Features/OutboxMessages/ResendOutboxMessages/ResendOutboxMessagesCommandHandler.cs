using Dapper;
using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.CQRS;
using Shared.Identity;
using Shared.Time;

namespace Integration.Application.Features.OutboxMessages.ResendOutboxMessages;

/// <summary>
/// Never flips Processed -> Pending (design D14) — only a currently-Failed row is resendable. Each item
/// is checked against the module whitelist independently; an unknown module is a skipped entry, not a
/// whole-request 400 (api-contract.md).
///
/// The outbox UPDATE (Dapper) and its audit row (EF) commit together, per item, in one SQL
/// transaction on <see cref="IntegrationDbContext"/>'s own connection — every module schema lives in the
/// same database (verified: both the Dapper factory and this DbContext read the "Database" connection
/// string), so a single ambient transaction covers the cross-schema UPDATE and the audit INSERT. Wrapped
/// in <c>CreateExecutionStrategy</c> because the DbContext has <c>EnableRetryOnFailure</c> configured,
/// which refuses a manually-started transaction otherwise.
/// </summary>
public class ResendOutboxMessagesCommandHandler(
    IntegrationDbContext dbContext,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ResendOutboxMessagesCommand, OutboxResendResult>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "item.Module is checked against OutboxModuleWhitelist.IsValid immediately above, before either " +
            "interpolated [{item.Module}] query below is ever built — an unknown module is skipped and " +
            "never reaches this SQL. Id is bound as a @parameter, not interpolated.")]
    public async Task<OutboxResendResult> Handle(ResendOutboxMessagesCommand request, CancellationToken cancellationToken)
    {
        var actorCode = currentUser.UserCode
            ?? throw new UnauthorizedAccessException("A signed-in user is required to resend an outbox message.");
        var now = dateTimeProvider.ApplicationNow;
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var accepted = new List<OutboxMessageRef>();
        var skipped = new List<OutboxResendSkipped>();

        // A duplicate item would be accepted the first time, then skipped NotFailed. Records compare by value.
        foreach (var item in request.Items.Distinct())
        {
            if (!OutboxModuleWhitelist.IsValid(item.Module))
            {
                skipped.Add(new OutboxResendSkipped(item.Module, item.Id, "UnknownModule"));
                continue;
            }

            var skipReason = await strategy.ExecuteAsync(async () =>
            {
                // A retried attempt must not re-add the same audit entity twice.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var connection = dbContext.Database.GetDbConnection();
                var dbTransaction = transaction.GetDbTransaction();

                var rowsAffected = await connection.ExecuteAsync(
                    $"""
                    UPDATE [{item.Module}].[IntegrationEventOutbox]
                    SET Status = 'Pending', RetryCount = 0, Error = NULL, ProcessingStartedAt = NULL
                    WHERE Id = @Id AND Status = 'Failed'
                    """,
                    new { item.Id }, dbTransaction);

                if (rowsAffected == 0)
                {
                    var exists = await connection.ExecuteScalarAsync<int>(
                        $"SELECT COUNT(*) FROM [{item.Module}].[IntegrationEventOutbox] WHERE Id = @Id",
                        new { item.Id }, dbTransaction);

                    await transaction.RollbackAsync(cancellationToken);
                    return exists == 0 ? "NotFound" : "NotFailed";
                }

                dbContext.FailedMessageAuditLogs.Add(FailedMessageAuditLog.Create(
                    FailedMessageAuditAction.OutboxResend, FailedMessageAuditSource.Outbox,
                    item.Id, item.Module, actorCode, request.IpAddress, request.Reason, now));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            });

            if (skipReason is not null)
                skipped.Add(new OutboxResendSkipped(item.Module, item.Id, skipReason));
            else
                accepted.Add(item);
        }

        return new OutboxResendResult(accepted, skipped);
    }
}
