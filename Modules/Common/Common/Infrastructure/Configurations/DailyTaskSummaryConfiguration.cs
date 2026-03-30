using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class DailyTaskSummaryConfiguration : IEntityTypeConfiguration<DailyTaskSummary>
{
    public void Configure(EntityTypeBuilder<DailyTaskSummary> builder)
    {
        builder.ToTable("DailyTaskSummaries");

        builder.HasKey(x => new { x.Date, x.Username });

        builder.Property(x => x.Date)
            .HasColumnType("date");

        builder.Property(x => x.Username)
            .HasMaxLength(255);
    }
}
