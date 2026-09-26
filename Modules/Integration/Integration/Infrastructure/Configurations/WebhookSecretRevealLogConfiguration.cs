using Integration.Domain.WebhookSubscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class WebhookSecretRevealLogConfiguration : IEntityTypeConfiguration<WebhookSecretRevealLog>
{
    public void Configure(EntityTypeBuilder<WebhookSecretRevealLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Field).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RevealedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.HasIndex(x => new { x.SubscriptionId, x.RevealedAt });
    }
}
