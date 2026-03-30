using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class TeamWorkloadSummaryConfiguration : IEntityTypeConfiguration<TeamWorkloadSummary>
{
    public void Configure(EntityTypeBuilder<TeamWorkloadSummary> builder)
    {
        builder.ToTable("TeamWorkloadSummaries");

        builder.HasKey(x => x.Username);

        builder.Property(x => x.Username)
            .HasMaxLength(255);

        builder.Property(x => x.TeamId)
            .HasMaxLength(255)
            .IsRequired(false);
    }
}
