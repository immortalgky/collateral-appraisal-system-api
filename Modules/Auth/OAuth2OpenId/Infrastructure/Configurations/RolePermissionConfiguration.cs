namespace OAuth2OpenId.Data.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(p => new { p.RoleId, p.PermissionId });
        builder.HasOne(p => p.Role).WithMany(p => p.Permissions).HasForeignKey(p => p.RoleId);
        builder.HasOne(p => p.Permission).WithMany().HasForeignKey(p => p.PermissionId);
    }
}