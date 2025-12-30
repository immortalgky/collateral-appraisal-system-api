namespace OAuth2OpenId.Data.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasKey(p => new { p.UserId, p.PermissionId });
        builder.HasOne(p => p.User).WithMany(p => p.Permissions).HasForeignKey(p => p.UserId);
        builder.HasOne(p => p.Permission).WithMany().HasForeignKey(p => p.PermissionId);
    }
}
