using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain.Committees;

namespace Workflow.Data.Configurations;

public class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> builder)
    {
        builder.ToTable("CommitteeMembers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId).HasMaxLength(255).IsRequired();
        builder.Property(m => m.MemberName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.Position).HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.Attendance)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(CommitteeAttendance.Always)
            .IsRequired();

        builder.HasIndex(m => new { m.CommitteeId, m.UserId })
            .HasFilter("[IsActive] = 1")
            .IsUnique();
    }
}
