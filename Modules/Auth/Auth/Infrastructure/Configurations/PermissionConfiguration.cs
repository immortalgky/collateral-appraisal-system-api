namespace Auth.Infrastructure.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("PermissionId");

        builder.Property(p => p.PermissionCode).IsRequired().HasMaxLength(100);
        builder.Property(p => p.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Module).IsRequired().HasMaxLength(50);

        builder.HasIndex(p => p.PermissionCode).IsUnique();
    }
}
