namespace Appraisal.Domain.UnitTickets;

/// <summary>One unit covered by a <see cref="UnitTicket"/>.</summary>
public class UnitTicketUnit : Entity<Guid>
{
    public Guid UnitTicketId { get; private set; }

    /// <summary>The appraisal.ProjectUnits row priced when the ticket was issued.</summary>
    public Guid ProjectUnitId { get; private set; }

    /// <summary>
    /// The key the caller named this unit by, stored as matched. Survives the reappraisal that
    /// replaces ProjectUnitId, so a ticket can still say which rooms it covers.
    /// </summary>
    public string UnitKey { get; private set; } = default!;

    private UnitTicketUnit()
    {
    }

    internal static UnitTicketUnit Create(Guid unitTicketId, Guid projectUnitId, string unitKey)
    {
        return new UnitTicketUnit
        {
            Id = Guid.CreateVersion7(),
            UnitTicketId = unitTicketId,
            ProjectUnitId = projectUnitId,
            UnitKey = unitKey.Trim()
        };
    }
}
