namespace OAuth2OpenId.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("PermissionId");

        builder.HasIndex(p => p.PermissionCode).IsUnique();
    }
}
