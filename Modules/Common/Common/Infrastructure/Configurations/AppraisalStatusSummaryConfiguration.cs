using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class AppraisalStatusSummaryConfiguration : IEntityTypeConfiguration<AppraisalStatusSummary>
{
    public void Configure(EntityTypeBuilder<AppraisalStatusSummary> builder)
    {
        builder.ToTable("AppraisalStatusSummaries");

        builder.HasKey(x => x.Status);

        builder.Property(x => x.Status)
            .HasMaxLength(50);

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnType("datetimeoffset");
    }
}
