using Auth.Domain.Groups;

namespace Auth.Infrastructure.Configurations;

public class GroupUserConfiguration : IEntityTypeConfiguration<GroupUser>
{
    public void Configure(EntityTypeBuilder<GroupUser> builder)
    {
        builder.ToTable("GroupUsers", "auth");

        builder.HasKey(gu => new { gu.GroupId, gu.UserId });

        builder.HasOne(gu => gu.Group)
            .WithMany(g => g.Users)
            .HasForeignKey(gu => gu.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
