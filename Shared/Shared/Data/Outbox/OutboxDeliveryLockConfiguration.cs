using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Outbox;

public class OutboxDeliveryLockConfiguration : IEntityTypeConfiguration<OutboxDeliveryLock>
{
    public void Configure(EntityTypeBuilder<OutboxDeliveryLock> builder)
    {
        builder.ToTable("OutboxDeliveryLock");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(100);

        builder.Property(x => x.InstanceId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.LeasedUntil)
            .IsRequired();

        builder.Property(x => x.AcquiredAt)
            .IsRequired();
    }
}
