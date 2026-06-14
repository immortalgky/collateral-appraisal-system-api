using Integration.Domain.WebhookSubscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SystemCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CallbackUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.SecretKey)
            .HasMaxLength(256)
            .IsRequired();

        // SystemCode is the routing key the outbound dispatcher looks up — it must be unique so the
        // app-level duplicate check has an atomic DB backstop against concurrent inserts.
        builder.HasIndex(x => x.SystemCode)
            .IsUnique()
            .HasDatabaseName("IX_WebhookSubscription_SystemCode");

        builder.HasIndex(x => new { x.SystemCode, x.IsActive })
            .HasDatabaseName("IX_WebhookSubscription_SystemCode_IsActive");
    }
}
