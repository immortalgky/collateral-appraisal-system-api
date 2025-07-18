namespace OAuth2OpenId.Data.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasOne(p => p.Role)
            .WithMany(p => p.Permissions)
            .HasForeignKey(p => p.RoleId);
    }
}