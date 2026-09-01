using System.Data;
using Appraisal.Application.Services;
using Appraisal.Domain.UnitTickets;
using Appraisal.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Time;

namespace Appraisal.Application.Features.Project.IssueUnitTicket;

/// <summary>
/// Issues the ticket for a set of block-project units, or returns the one already issued for that
/// same set.
///
/// IDEMPOTENCY IS THE POINT, not a nicety. The caller is a read endpoint: LOS pulls a result to show
/// it, to retry, and again when the loan actually goes ahead. Minting on every pull would burn
/// numbers on browsing and — worse — hand AS400 a second collateral id for rooms it already holds.
/// The lookup below, backed by the unique index on (AppraisalId, UnitSetKey), is what makes
/// issuing a number from a GET safe.
///
/// The lookup alone is read-then-write, though, and two simultaneous pulls for the same rooms both
/// miss it. The loser used to reach SaveChanges, violate the unique index and surface a 500 from an
/// endpoint documented as idempotent, so an application lock keyed on the same pair serialises them
/// first: the second request waits, then finds the ticket the first committed and returns it. The
/// lock is held by the transaction and released when it ends, so nothing has to unwind it.
///
/// SCOPED TO ONE APPRAISAL BOOK, ON PURPOSE. A ticket belongs to the valuation it was issued from.
/// Reappraising the block produces a new book — 69000123 becomes 70000456 over the same units — and
/// its tickets are new with it. Idempotency therefore stops at the book: pull the same rooms twice
/// against one book and the same ticket returns; pull them again after the reappraisal and a fresh
/// one is issued.
///
/// This is NOT about ownership. The customer is often the same person holding the same room; a
/// reappraisal is a revaluation, not a sale. What changes is which valuation the number speaks for.
///
/// It rests on one thing being true at the host: AS400 refreshes a collateral's CCSURV from the
/// result file we send rather than opening a second collateral when the key changes. If that ever
/// stops holding, the same room would end up recorded twice there, and the scope of this key has to
/// be revisited before that happens — not after.
///
/// The consequence to know either way: one physical room accumulates a ticket per book it was pulled
/// under, and nothing in this table joins them. Walking PrevAppraisalId is still how a room's
/// history is found.
/// </summary>
public class IssueUnitTicketCommandHandler(
    AppraisalDbContext dbContext,
    IUnitTicketNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider,
    ILogger<IssueUnitTicketCommandHandler> logger)
    : ICommandHandler<IssueUnitTicketCommand, IssueUnitTicketResult>
{
    public async Task<IssueUnitTicketResult> Handle(
        IssueUnitTicketCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Units.Count == 0)
            throw new BadRequestException("A unit ticket needs at least one unit.");

        var unitSetKey = UnitTicket.BuildUnitSetKey(command.Units.Select(u => u.ProjectUnitId));

        await SerialiseOnUnitSetAsync(command.AppraisalId, unitSetKey, cancellationToken);

        var existing = await dbContext.UnitTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.AppraisalId == command.AppraisalId && t.UnitSetKey == unitSetKey,
                cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "Unit ticket {TicketNumber} already covers {UnitSetKey} on appraisal {AppraisalId}; returning it.",
                existing.TicketNumber, unitSetKey, command.AppraisalId);

            return new IssueUnitTicketResult(existing.TicketNumber, AlreadyIssued: true);
        }

        var now = dateTimeProvider.ApplicationNow;
        var ticketNumber = await numberGenerator.GenerateAsync(now.Year + 543, cancellationToken);

        var ticket = UnitTicket.Issue(
            ticketNumber: ticketNumber,
            appraisalId: command.AppraisalId,
            units: [.. command.Units.Select(u => (u.ProjectUnitId, u.UnitKey))],
            issuedTo: command.IssuedTo,
            issuedAt: now);

        dbContext.UnitTickets.Add(ticket);

        logger.LogInformation(
            "Issued unit ticket {TicketNumber} for {UnitCount} unit(s) ({UnitSetKey}) on appraisal {AppraisalId}.",
            ticketNumber, command.Units.Count, unitSetKey, command.AppraisalId);

        return new IssueUnitTicketResult(ticketNumber, AlreadyIssued: false);
    }

    /// <summary>
    /// Blocks a concurrent request for this same appraisal and unit set until the first one commits.
    ///
    /// sp_getapplock with LockOwner='Transaction' must run on the connection that owns the ambient
    /// transaction, which is why it goes through the DbContext rather than a fresh connection.
    /// Handlers of this command always run inside one (ITransactionalCommand), and a missing
    /// transaction is a wiring mistake rather than a runtime condition, so it is loud.
    ///
    /// A timeout returns a negative code from the proc; it is not thrown on, because losing the race
    /// to acquire the lock still leaves the unique index as the backstop and a raised timeout here
    /// would be a worse failure than the one it guards.
    /// </summary>
    private async Task SerialiseOnUnitSetAsync(
        Guid appraisalId, string unitSetKey, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

        if (transaction is null)
            throw new InvalidOperationException(
                "IssueUnitTicketCommand must run inside a transaction — it takes an application "
                + "lock with LockOwner='Transaction' to keep two simultaneous pulls for the same "
                + "rooms from issuing two tickets.");

        var parameters = new DynamicParameters();
        parameters.Add("Resource", $"UnitTicket:{appraisalId:N}:{unitSetKey}");
        parameters.Add("LockMode", "Exclusive");
        parameters.Add("LockOwner", "Transaction");
        parameters.Add("LockTimeout", 15000);
        parameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

        await dbContext.Database.GetDbConnection().ExecuteAsync(new CommandDefinition(
            "sp_getapplock", parameters, transaction,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var code = parameters.Get<int>("ReturnValue");

        if (code < 0)
            logger.LogWarning(
                "Could not take the unit-ticket lock for appraisal {AppraisalId} (sp_getapplock "
                + "returned {Code}); relying on the unique index instead.",
                appraisalId, code);
    }
}
