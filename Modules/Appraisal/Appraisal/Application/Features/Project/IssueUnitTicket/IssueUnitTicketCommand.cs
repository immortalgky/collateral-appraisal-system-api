namespace Appraisal.Application.Features.Project.IssueUnitTicket;

/// <summary>
/// Issues — or returns the one already issued for — the ticket covering a set of block-project
/// units, so the caller can hand it to AS400 as the collateral's key.
/// </summary>
/// <param name="AppraisalId">The block appraisal the caller pulled a result for.</param>
/// <param name="Units">
///   The units the collateral covers, already resolved. One ticket covers the whole set: two rooms
///   bought together are one collateral to AS400 and must be one ticket.
/// </param>
/// <param name="IssuedTo">Whatever the caller identified itself with — the LOS case key, when given.</param>
public record IssueUnitTicketCommand(
    Guid AppraisalId,
    IReadOnlyList<TicketUnitRef> Units,
    string? IssuedTo
) : ICommand<IssueUnitTicketResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

/// <param name="ProjectUnitId">The appraisal.ProjectUnits row that was matched.</param>
/// <param name="UnitKey">The key the caller named it by — the room, plot or registration number.</param>
public record TicketUnitRef(Guid ProjectUnitId, string UnitKey);

/// <param name="TicketNumber">Format {YY}U{00000}.</param>
/// <param name="AlreadyIssued">
///   True when the set had a ticket already and this call returned it unchanged. Not an error —
///   it is the normal answer to a repeated pull, and the reason browsing a result cannot burn
///   numbers or put a second collateral into AS400 for the same rooms.
/// </param>
public record IssueUnitTicketResult(string TicketNumber, bool AlreadyIssued);
