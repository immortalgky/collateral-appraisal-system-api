namespace Appraisal.Infrastructure.Configurations;

public class CommitteeConfiguration : IEntityTypeConfiguration<Committee>
{
    public void Configure(EntityTypeBuilder<Committee> builder)
    {
        builder.ToTable("Committees");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.CommitteeName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CommitteeCode).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.QuorumType).IsRequired().HasMaxLength(50);
        builder.Property(c => c.MajorityType).IsRequired().HasMaxLength(50);

        builder.Property(c => c.CreatedOn).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.CommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Conditions)
            .WithOne()
            .HasForeignKey(cond => cond.CommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Conditions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.CommitteeCode).IsUnique();
    }
}

public class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> builder)
    {
        builder.ToTable("CommitteeMembers");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(m => m.CommitteeId).IsRequired();
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.MemberName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Role).IsRequired().HasMaxLength(100);

        builder.Property(m => m.CreatedOn).IsRequired();
        builder.Property(m => m.CreatedBy).IsRequired();

        builder.HasIndex(m => m.CommitteeId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => new { m.CommitteeId, m.UserId });
    }
}

public class CommitteeApprovalConditionConfiguration : IEntityTypeConfiguration<CommitteeApprovalCondition>
{
    public void Configure(EntityTypeBuilder<CommitteeApprovalCondition> builder)
    {
        builder.ToTable("CommitteeApprovalConditions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.CommitteeId).IsRequired();
        builder.Property(c => c.ConditionType).IsRequired().HasMaxLength(50);
        builder.Property(c => c.RoleRequired).HasMaxLength(100);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(200);

        builder.HasIndex(c => c.CommitteeId);
    }
}

public class CommitteeVoteConfiguration : IEntityTypeConfiguration<CommitteeVote>
{
    public void Configure(EntityTypeBuilder<CommitteeVote> builder)
    {
        builder.ToTable("CommitteeVotes");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(v => v.ReviewId).IsRequired();
        builder.Property(v => v.CommitteeMemberId).IsRequired();
        builder.Property(v => v.MemberName).IsRequired().HasMaxLength(200);
        builder.Property(v => v.MemberRole).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Vote).IsRequired().HasMaxLength(50);
        builder.Property(v => v.VotedAt).IsRequired();
        builder.Property(v => v.Comments).HasMaxLength(1000);

        builder.HasIndex(v => v.ReviewId);
        builder.HasIndex(v => v.CommitteeMemberId);
    }
}