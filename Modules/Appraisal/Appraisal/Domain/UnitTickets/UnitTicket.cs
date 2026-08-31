using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Domain.UnitTickets;

/// <summary>
/// The number the bank uses to refer to one block-project collateral, issued when LOS asks for that
/// collateral's appraisal result and carried from there into AS400.
///
/// WHY IT EXISTS. A block appraisal values a whole development; the bank lends against one room, or
/// a few adjacent ones. Until now the two sides were joined by whatever string AS400 happened to
/// write — a room number, a house number, a deed reference — parsed out of free text and matched
/// against three different columns. That join is unreliable by construction: the identifiers are
/// issued by other authorities at other times, and a village plot has no house number at all when
/// the project is appraised. The ticket replaces the join with a key the two systems agree on
/// because we minted it and handed it over.
///
/// GRAIN. One ticket covers one AS400 collateral, which may be one room or several bought together.
/// That is why the unit link is a list: two adjacent rooms pledged as one collateral get one ticket
/// and one value, not two of each.
///
/// WHEN IT IS ISSUED. Only when LOS pulls the result — never at appraisal time. Most units of a
/// development are never financed by this bank, and numbering them all would issue thousands of
/// keys that no one will ever quote.
///
/// SCOPE. A ticket belongs to one appraisal book. Pull the same rooms twice against the same book
/// and the same ticket comes back — that is what keeps a repeated read from burning numbers and from
/// putting a second collateral into AS400 for rooms it already holds. Reappraise the block, though,
/// and the book itself is new (69000123 becomes 70000456 over the same units); a pull against the
/// new book issues a new ticket, because the number speaks for a valuation, not for the room.
/// Ownership has nothing to do with it — the customer is usually the same one.
/// </summary>
public class UnitTicket : Aggregate<Guid>
{
    /// <summary>Format {YY}U{00000} — eight characters, e.g. "69U00042".</summary>
    public string TicketNumber { get; private set; } = default!;

    /// <summary>
    /// The appraisal this ticket was issued against. Half of the idempotency key, and the reason a
    /// reappraisal starts the numbering over: the next round is a different appraisal.
    /// </summary>
    public Guid AppraisalId { get; private set; }

    /// <summary>
    /// The rooms this ticket covers, normalised and sorted, joined by '|'. Together with
    /// AppraisalId this is the idempotency key: the same rooms asked for twice return one ticket.
    /// </summary>
    public string UnitSetKey { get; private set; } = default!;

    /// <summary>Whatever the caller identified itself with (LOS case key), when it supplied one.</summary>
    public string? IssuedTo { get; private set; }

    public DateTime IssuedAt { get; private set; }

    private readonly List<UnitTicketUnit> _units = [];

    /// <summary>
    /// The unit rows priced at issue time. Kept for audit and for reading a value back without
    /// re-matching; the durable identity is <see cref="UnitSetKey"/>, because these rows are
    /// replaced by the next reappraisal.
    /// </summary>
    public IReadOnlyList<UnitTicketUnit> Units => _units.AsReadOnly();

    private UnitTicket()
    {
    }

    public static UnitTicket Issue(
        string ticketNumber,
        Guid appraisalId,
        IReadOnlyList<(Guid ProjectUnitId, string UnitKey)> units,
        string? issuedTo,
        DateTime issuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);
        ArgumentNullException.ThrowIfNull(units);

        if (units.Count == 0)
            throw new InvalidProjectStateException("A unit ticket must cover at least one unit.");

        var ticket = new UnitTicket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = ticketNumber,
            AppraisalId = appraisalId,
            UnitSetKey = BuildUnitSetKey(units.Select(u => u.UnitKey)),
            IssuedTo = issuedTo,
            IssuedAt = issuedAt
        };

        foreach (var (projectUnitId, unitKey) in units)
            ticket._units.Add(UnitTicketUnit.Create(ticket.Id, projectUnitId, unitKey));

        return ticket;
    }

    /// <summary>
    /// Normalises a set of room keys into one comparable string: trimmed, lower-cased, de-duplicated
    /// and sorted, joined by '|'.
    ///
    /// Sorting is what makes "13,14" and "14,13" the same request — LOS has no reason to order them,
    /// and two tickets for one collateral would put two collateral into AS400 for the same rooms.
    /// </summary>
    public static string BuildUnitSetKey(IEnumerable<string> unitKeys)
    {
        ArgumentNullException.ThrowIfNull(unitKeys);

        var parts = unitKeys
            .Select(k => k?.Trim().ToLowerInvariant())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        if (parts.Count == 0)
            throw new InvalidProjectStateException("A unit ticket needs at least one non-blank unit key.");

        return string.Join('|', parts);
    }
}
