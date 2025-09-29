namespace Collateral.Data.Configurations;

public class CollateralEngagementConfiguration : IEntityTypeConfiguration<CollateralEngagement>
{
    public void Configure(EntityTypeBuilder<CollateralEngagement> builder)
    {
        builder.HasKey(p => new { p.Id, p.ReqId });
        builder.Property(p => p.Id).HasColumnName("CollatId");
        builder
            .HasOne<CollateralMaster>()
            .WithMany(p => p.CollateralEngagements)
            .HasForeignKey(p => p.Id);
    }
}
