namespace Appraisal.Infrastructure.Configurations;

public class
    MachineryAppraisalDetailConfiguration : IOwnedEntityConfiguration<AppraisalProperty, MachineryAppraisalDetail>
{
    public void Configure(OwnedNavigationBuilder<AppraisalProperty, MachineryAppraisalDetail> builder)
    {
        builder.ToTable("MachineryAppraisalDetails", "appraisal");
        builder.WithOwner().HasForeignKey(e => e.AppraisalPropertyId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Machine Identification
        builder.Property(e => e.PropertyName).HasMaxLength(200);
        builder.Property(e => e.MachineName).HasMaxLength(200);
        builder.Property(e => e.EngineNo).HasMaxLength(100);
        builder.Property(e => e.ChassisNo).HasMaxLength(100);
        builder.Property(e => e.RegistrationNo).HasMaxLength(50);

        // Machine Specifications
        builder.Property(e => e.Brand).HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(100);
        builder.Property(e => e.CountryOfManufacture).HasMaxLength(100);

        // Purchase Info
        builder.Property(e => e.PurchasePrice).HasPrecision(18, 2);

        // Dimensions
        builder.Property(e => e.Capacity).HasMaxLength(100);
        builder.Property(e => e.Width).HasPrecision(10, 2);
        builder.Property(e => e.Length).HasPrecision(10, 2);
        builder.Property(e => e.Height).HasPrecision(10, 2);

        // Energy
        builder.Property(e => e.EnergyUse).HasMaxLength(100);
        builder.Property(e => e.EnergyUseRemark).HasMaxLength(200);

        // Owner
        builder.Property(e => e.OwnerName).IsRequired().HasMaxLength(200);

        // Usage & Condition
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.ConditionUse).HasMaxLength(100);
        builder.Property(e => e.MachineCondition).HasMaxLength(100);
        builder.Property(e => e.MachineEfficiency).HasMaxLength(100);
        builder.Property(e => e.MachineTechnology).HasMaxLength(100);
        builder.Property(e => e.UsePurpose).HasMaxLength(200);
        builder.Property(e => e.MachinePart).HasMaxLength(500);

        // Appraiser Notes
        builder.Property(e => e.Remark).HasMaxLength(1000);
        builder.Property(e => e.Other).HasMaxLength(500);
        builder.Property(e => e.AppraiserOpinion).HasMaxLength(1000);

        // Relationship - FK to AppraisalProperty (1:1)
        builder.Property(e => e.AppraisalPropertyId).IsRequired();
    }
}