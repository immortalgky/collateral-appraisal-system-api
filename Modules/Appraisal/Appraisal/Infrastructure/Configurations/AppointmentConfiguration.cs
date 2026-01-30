namespace Appraisal.Infrastructure.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AppraisalId).IsRequired();
        builder.Property(a => a.AppointmentDate).IsRequired();

        builder.Property(a => a.Latitude).HasPrecision(9, 6);
        builder.Property(a => a.Longitude).HasPrecision(9, 6);

        builder.Property(a => a.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");

        builder.Property(a => a.AppointedBy).IsRequired();
        builder.Property(a => a.ContactPerson).HasMaxLength(200);
        builder.Property(a => a.ContactPhone).HasMaxLength(50);

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AppraisalId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.AppointmentDate);
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
        builder.Property(h => h.AppraisalId).IsRequired();

        builder.Property(h => h.PreviousAppointmentDate).IsRequired();
        builder.Property(h => h.PreviousStatus).IsRequired().HasMaxLength(20);

        builder.Property(h => h.ChangeType).IsRequired().HasMaxLength(50);
        builder.Property(h => h.ChangeReason).HasMaxLength(500);
        builder.Property(h => h.ChangedOn).IsRequired();
        builder.Property(h => h.ChangedBy).IsRequired();

        builder.HasIndex(h => h.AppointmentId);
        builder.HasIndex(h => h.AppraisalId);
    }
}