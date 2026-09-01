using Appraisal.Domain.UnitTickets;

namespace Appraisal.Infrastructure.Configurations.UnitTickets;

public class UnitTicketConfiguration : IEntityTypeConfiguration<UnitTicket>
{
    public void Configure(EntityTypeBuilder<UnitTicket> builder)
    {
        builder.ToTable("UnitTickets");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Eight characters: {YY}U{00000}. Sized exactly so a value that outgrew the format cannot be
        // stored and then truncated into the 10-character AS400 field further downstream.
        builder.Property(e => e.TicketNumber).IsRequired().HasMaxLength(8);
        builder.HasIndex(e => e.TicketNumber)
            .IsUnique()
            .HasDatabaseName("UX_UnitTickets_TicketNumber");

        builder.Property(e => e.AppraisalId).IsRequired();

        // 400 holds far more rooms than a single collateral ever covers; the longest real key seen
        // in the AS400 feed is a five-room list well under 60 characters.
        // 64 hex characters of SHA-256 — see UnitTicket.UnitSetKey for why it is a hash.
        builder.Property(e => e.UnitSetKey).IsRequired().HasMaxLength(64).IsFixedLength();

        // The idempotency guard, enforced by the database rather than by a read-then-write race:
        // two concurrent pulls for the same rooms cannot both insert.
        builder.HasIndex(e => new { e.AppraisalId, e.UnitSetKey })
            .IsUnique()
            .HasDatabaseName("UX_UnitTickets_Appraisal_UnitSet");

        builder.Property(e => e.IssuedTo).HasMaxLength(100);
        builder.Property(e => e.IssuedAt).IsRequired();

        builder.HasMany(e => e.Units)
            .WithOne()
            .HasForeignKey(u => u.UnitTicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UnitTicketUnitConfiguration : IEntityTypeConfiguration<UnitTicketUnit>
{
    public void Configure(EntityTypeBuilder<UnitTicketUnit> builder)
    {
        builder.ToTable("UnitTicketUnits");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.UnitTicketId).IsRequired();
        builder.Property(e => e.ProjectUnitId).IsRequired();
        builder.Property(e => e.UnitKey).IsRequired().HasMaxLength(100);

        // Reading back the other way: which ticket covers this unit row.
        builder.HasIndex(e => e.ProjectUnitId);
    }
}
