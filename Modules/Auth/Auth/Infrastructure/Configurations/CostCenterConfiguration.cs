using Auth.Domain.Organization;

namespace Auth.Infrastructure.Configurations;

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("CostCenters", "auth");

        builder.HasKey(c => c.Code);

        builder.Property(c => c.Code).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(40);
        builder.Property(c => c.Text).HasMaxLength(50);
        builder.Property(c => c.IsActive).HasDefaultValue(true);
    }
}
