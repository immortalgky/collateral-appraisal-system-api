namespace Appraisal.Infrastructure.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AssignmentId).IsRequired();
        builder.Property(a => a.AppointmentDateTime).IsRequired();

        builder.Property(a => a.Latitude).HasPrecision(9, 6);
        builder.Property(a => a.Longitude).HasPrecision(9, 6);

        builder.Property(a => a.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(a => a.Reason).HasMaxLength(4000);

        builder.Property(a => a.RescheduleCount).HasDefaultValue(0);

        builder.Property(a => a.AppointedBy).IsRequired();
        builder.Property(a => a.ContactPerson).HasMaxLength(200);
        builder.Property(a => a.ContactPhone).HasMaxLength(50);

        // FK to AppraisalAssignment (no cascade per spec)
        builder.HasOne<AppraisalAssignment>()
            .WithMany()
            .HasForeignKey(a => a.AssignmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AssignmentId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.AppointmentDateTime);
    }
}

public class AppointmentHistoryConfiguration : IEntityTypeConfiguration<AppointmentHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentHistory> builder)
    {
        builder.ToTable("AppointmentHistory");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(h => h.AppointmentId).IsRequired();

        builder.Property(h => h.PreviousAppointmentDateTime).IsRequired();
        builder.Property(h => h.PreviousStatus).IsRequired().HasMaxLength(20);

        builder.Property(h => h.ChangeType).IsRequired().HasMaxLength(50);
        builder.Property(h => h.ChangeReason).HasMaxLength(4000);
        builder.Property(h => h.ChangedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
        builder.Property(h => h.ChangedBy).IsRequired();

        builder.HasIndex(h => h.AppointmentId);
    }
}
