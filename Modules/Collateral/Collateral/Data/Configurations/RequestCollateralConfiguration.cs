namespace Collateral.Data.Configurations;

public class RequestCollateralConfiguration : IEntityTypeConfiguration<RequestCollateral>
{
    public void Configure(EntityTypeBuilder<RequestCollateral> builder)
    {
        builder.HasKey(p => new { p.Id, p.ReqId });
        builder.Property(p => p.Id).HasColumnName("CollatId");
        builder
            .HasOne<CollateralMaster>()
            .WithMany(p => p.RequestCollaterals)
            .HasForeignKey(p => p.Id);
    }
}
