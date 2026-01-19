namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the AppraisalProperty entity.
/// </summary>
public class AppraisalPropertyConfiguration : IEntityTypeConfiguration<AppraisalProperty>
{
    public void Configure(EntityTypeBuilder<AppraisalProperty> builder)
    {
        builder.ToTable("AppraisalProperties");

        // Primary Key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(c => c.AppraisalId)
            .IsRequired();

        builder.Property(c => c.SequenceNumber)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        // PropertyType Value Object (stored as string)
        builder.OwnsOne(c => c.PropertyType, pt =>
        {
            pt.Property(t => t.Code)
                .HasColumnName("PropertyType")
                .IsRequired()
                .HasMaxLength(30);
        });

        // Audit Fields
        builder.Property(c => c.CreatedOn)
            .IsRequired();
        builder.Property(c => c.CreatedBy)
            .IsRequired();
        builder.Property(c => c.UpdatedOn);
        builder.Property(c => c.UpdatedBy);

        // Indexes
        builder.HasIndex(c => c.AppraisalId);
        builder.HasIndex(c => new { c.AppraisalId, c.SequenceNumber })
            .IsUnique();

        // Relationships with Detail entities (1:1)
        // For LandAndBuilding type, both LandDetail AND BuildingDetail are populated
        builder.HasOne(p => p.LandDetail)
            .WithOne()
            .HasForeignKey<LandAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BuildingDetail)
            .WithOne()
            .HasForeignKey<BuildingAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CondoDetail)
            .WithOne()
            .HasForeignKey<CondoAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.VehicleDetail)
            .WithOne()
            .HasForeignKey<VehicleAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.VesselDetail)
            .WithOne()
            .HasForeignKey<VesselAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.MachineryDetail)
            .WithOne()
            .HasForeignKey<MachineryAppraisalDetail>(d => d.AppraisalPropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}