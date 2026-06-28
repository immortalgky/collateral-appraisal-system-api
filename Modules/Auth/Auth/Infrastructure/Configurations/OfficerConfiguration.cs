using Auth.Domain.Organization;

namespace Auth.Infrastructure.Configurations;

public class OfficerConfiguration : IEntityTypeConfiguration<Officer>
{
    public void Configure(EntityTypeBuilder<Officer> builder)
    {
        builder.ToTable("Officers", "auth");

        builder.HasKey(o => o.OfficerCode);

        builder.Property(o => o.OfficerCode).HasMaxLength(3).IsRequired();
        builder.Property(o => o.BranchNumber).HasMaxLength(3);
        builder.Property(o => o.OfficerId).HasMaxLength(10);
        builder.Property(o => o.Name).HasMaxLength(40);
        builder.Property(o => o.ShortName).HasMaxLength(20);
        builder.Property(o => o.CostCenterCode).HasMaxLength(8);
        builder.Property(o => o.DepartmentCode).HasMaxLength(3);
        builder.Property(o => o.IsActive).HasDefaultValue(true);

        builder.HasIndex(o => o.DepartmentCode);
        builder.HasIndex(o => o.CostCenterCode);
    }
}
