using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Lease;

public class BackgroundServiceLeaseConfiguration : IEntityTypeConfiguration<BackgroundServiceLease>
{
    public void Configure(EntityTypeBuilder<BackgroundServiceLease> builder)
    {
        builder.ToTable("BackgroundServiceLease");

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
