using Appraisal.Application.Services;
using Appraisal.Domain.UnitTickets;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

        var unitSetKey = UnitTicket.BuildUnitSetKey(command.Units.Select(u => u.UnitKey));

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
}
