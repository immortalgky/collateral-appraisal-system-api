using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain;

namespace Workflow.Data.Configurations;

public class ApprovalVoteConfiguration : IEntityTypeConfiguration<ApprovalVote>
{
    public void Configure(EntityTypeBuilder<ApprovalVote> builder)
    {
        builder.ToTable("ApprovalVotes");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.ActivityId).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Member).HasMaxLength(255).IsRequired();
        builder.Property(v => v.MemberRole).HasMaxLength(50);
        builder.Property(v => v.Vote).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Comments).HasMaxLength(1000);

        builder.HasIndex(v => new { v.ActivityExecutionId, v.Member }).IsUnique();
        builder.HasIndex(v => v.ActivityExecutionId);
    }
}
