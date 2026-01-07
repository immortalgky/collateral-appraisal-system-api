namespace Appraisal.Infrastructure.Configurations;

public class VesselAppraisalDetailConfiguration : IEntityTypeConfiguration<VesselAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<VesselAppraisalDetail> builder)
    {
        builder.ToTable("VesselAppraisalDetails", "appraisal");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Vessel Identification
        builder.Property(e => e.PropertyName).HasMaxLength(200);
        builder.Property(e => e.VesselName).HasMaxLength(200);
        builder.Property(e => e.EngineNo).HasMaxLength(100);
        builder.Property(e => e.RegistrationNo).HasMaxLength(50);

        // Vessel Specifications
        builder.Property(e => e.Brand).HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(100);
        builder.Property(e => e.PlaceOfManufacture).HasMaxLength(200);
        builder.Property(e => e.VesselType).HasMaxLength(100);
        builder.Property(e => e.ClassOfVessel).HasMaxLength(100);

        // Purchase Info
        builder.Property(e => e.PurchasePrice).HasPrecision(18, 2);

        // Dimensions
        builder.Property(e => e.EngineCapacity).HasMaxLength(100);
        builder.Property(e => e.Width).HasPrecision(10, 2);
        builder.Property(e => e.Length).HasPrecision(10, 2);
        builder.Property(e => e.Height).HasPrecision(10, 2);
        builder.Property(e => e.GrossTonnage).HasPrecision(18, 4);
        builder.Property(e => e.NetTonnage).HasPrecision(18, 4);

        // Energy
        builder.Property(e => e.EnergyUse).HasMaxLength(100);
        builder.Property(e => e.EnergyUseRemark).HasMaxLength(200);

        // Owner
        builder.Property(e => e.OwnerName).IsRequired().HasMaxLength(200);

        // Vessel Info
        builder.Property(e => e.FormerName).HasMaxLength(200);
        builder.Property(e => e.VesselCurrentName).HasMaxLength(200);
        builder.Property(e => e.Location).HasMaxLength(200);

        // Condition & Assessment
        builder.Property(e => e.ConditionUse).HasMaxLength(100);
        builder.Property(e => e.VesselCondition).HasMaxLength(100);
        builder.Property(e => e.VesselEfficiency).HasMaxLength(100);
        builder.Property(e => e.VesselTechnology).HasMaxLength(100);
        builder.Property(e => e.UsePurpose).HasMaxLength(200);
        builder.Property(e => e.VesselPart).HasMaxLength(500);

        // Appraiser Notes
        builder.Property(e => e.Remark).HasMaxLength(1000);
        builder.Property(e => e.Other).HasMaxLength(500);
        builder.Property(e => e.AppraiserOpinion).HasMaxLength(1000);

        // Relationship - FK to AppraisalProperty (1:1)
        builder.Property(e => e.AppraisalPropertyId).IsRequired();
    }
}
