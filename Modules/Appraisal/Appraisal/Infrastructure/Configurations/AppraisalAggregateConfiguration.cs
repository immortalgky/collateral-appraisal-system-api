namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the Appraisal aggregate root.
/// </summary>
public class AppraisalAggregateConfiguration : IEntityTypeConfiguration<Domain.Appraisals.Appraisal>
{
    public void Configure(EntityTypeBuilder<Domain.Appraisals.Appraisal> builder)
    {
        builder.ToTable("Appraisals");

        // Primary Key
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(a => a.AppraisalNumber)
            .HasMaxLength(50);

        builder.Property(a => a.RequestId)
            .IsRequired();

        builder.Property(a => a.AppraisalType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Priority)
            .IsRequired()
            .HasMaxLength(20);

        // Status Value Object (stored as string)
        builder.OwnsOne(a => a.Status, status =>
        {
            status.Property(s => s.Code)
                .HasColumnName("Status")
                .IsRequired()
                .HasMaxLength(30);
        });

        // SLA Tracking
        builder.Property(a => a.SLADays);
        builder.Property(a => a.SLADueDate);
        builder.Property(a => a.SLAStatus)
            .HasMaxLength(20);
        builder.Property(a => a.ActualDaysToComplete);
        builder.Property(a => a.IsWithinSLA);

        // Audit Fields
        builder.Property(a => a.CreatedOn)
            .IsRequired();
        builder.Property(a => a.CreatedBy)
            .IsRequired();
        builder.Property(a => a.UpdatedOn);
        builder.Property(a => a.UpdatedBy);

        // SoftDelete Value Object
        builder.OwnsOne(a => a.SoftDelete, sd =>
        {
            sd.Property(s => s.IsDeleted)
                .HasColumnName("IsDeleted")
                .IsRequired()
                .HasDefaultValue(false);
            sd.Property(s => s.DeletedOn)
                .HasColumnName("DeletedOn");
            sd.Property(s => s.DeletedBy)
                .HasColumnName("DeletedBy");
        });

        // Navigation to child entities
        builder.HasMany(a => a.Properties)
            .WithOne()
            .HasForeignKey(c => c.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Groups)
            .WithOne()
            .HasForeignKey(g => g.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Assignments)
            .WithOne()
            .HasForeignKey(a => a.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backing fields for collections
        builder.Navigation(a => a.Properties)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Groups)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Assignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(a => a.AppraisalNumber)
            .IsUnique()
            .HasFilter("[AppraisalNumber] IS NOT NULL");

        builder.HasIndex(a => a.RequestId);

        // Ignore domain events (not persisted)
        builder.Ignore(a => a.DomainEvents);
    }
}