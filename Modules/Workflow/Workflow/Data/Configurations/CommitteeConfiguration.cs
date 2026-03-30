using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain.Committees;

namespace Workflow.Data.Configurations;

public class CommitteeConfiguration : IEntityTypeConfiguration<Committee>
{
    public void Configure(EntityTypeBuilder<Committee> builder)
    {
        builder.ToTable("Committees");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.QuorumType).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.MajorityType).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.CommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Thresholds)
            .WithOne()
            .HasForeignKey(t => t.CommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Conditions)
            .WithOne()
            .HasForeignKey(c => c.CommitteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Thresholds).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Conditions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
