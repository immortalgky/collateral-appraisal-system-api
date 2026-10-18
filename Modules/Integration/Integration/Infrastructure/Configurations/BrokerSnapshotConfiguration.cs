using Integration.Domain.FailedMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class BrokerSnapshotConfiguration : IEntityTypeConfiguration<BrokerSnapshot>
{
    public void Configure(EntityTypeBuilder<BrokerSnapshot> builder)
    {
        builder.ToTable("BrokerSnapshots");

        builder.HasKey(x => x.Node);
        builder.Property(x => x.Node).HasMaxLength(100).ValueGeneratedNever();

        builder.Property(x => x.ManagementStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.QueuesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
    }
}
