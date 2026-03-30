using Common.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class RequestStatusSummaryConfiguration : IEntityTypeConfiguration<RequestStatusSummary>
{
    public void Configure(EntityTypeBuilder<RequestStatusSummary> builder)
    {
        builder.ToTable("RequestStatusSummaries");

        builder.HasKey(x => x.Status);

        builder.Property(x => x.Status)
            .HasMaxLength(50);
    }
}
