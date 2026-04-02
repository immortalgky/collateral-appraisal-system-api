namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the AppraisalProperty entity.
/// </summary>
public class
    AppraisalPropertyConfiguration : IEntityTypeConfiguration<AppraisalProperty>
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

        // Indexes
        builder.HasIndex(c => c.AppraisalId);
        builder.HasIndex(c => new { c.AppraisalId, c.SequenceNumber })
            .IsUnique();

        // Relationships with Detail entities (1:1)
        // For LandAndBuilding type, both LandDetail AND BuildingDetail are populated
        builder.OwnsOne(p => p.LandDetail, new LandAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.BuildingDetail, new BuildingAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.CondoDetail, new CondoAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.VehicleDetail, new VehicleAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.VesselDetail, new VesselAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.MachineryDetail, new MachineryAppraisalDetailConfiguration().Configure);
        builder.OwnsOne(p => p.LeaseAgreementDetail, new LeaseAgreementDetailConfiguration().Configure);
        builder.OwnsOne(p => p.RentalInfo, new RentalInfoConfiguration().Configure);

        // Construction Inspection (1:1, stored in separate table)
        builder.OwnsOne(p => p.ConstructionInspection, new ConstructionInspectionConfiguration().Configure);
    }
}