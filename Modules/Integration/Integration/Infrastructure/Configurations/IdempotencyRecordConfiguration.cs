using Integration.Domain.IdempotencyRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.OperationType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasMaxLength(64);

        builder.Property(x => x.ResponseData)
            .HasMaxLength(4000);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_IdempotencyRecord_IdempotencyKey");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_IdempotencyRecord_ExpiresAt");
    }
}
