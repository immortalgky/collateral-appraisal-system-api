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

        // Null = catch-all (matches any event for the SystemCode).
        builder.Property(x => x.EventType)
            .HasMaxLength(100);

        builder.Property(x => x.CallbackUrl)
            .HasMaxLength(500)
            .IsRequired();

        // Nullable — only used for AuthType = HMAC. TokenBearer subscriptions carry their
        // credential in ClientSecret instead.
        builder.Property(x => x.SecretKey)
            .HasMaxLength(256);

        builder.Property(x => x.AuthType)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(WebhookAuthType.Hmac);

        builder.Property(x => x.TokenEndpoint)
            .HasMaxLength(500);

        builder.Property(x => x.ClientId)
            .HasMaxLength(100);

        builder.Property(x => x.ClientSecret)
            .HasMaxLength(256);

        builder.Property(x => x.HttpMethod)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("POST");

        // (SystemCode, EventType) is the routing key the outbound dispatcher looks up — it must be
        // unique so the app-level duplicate check has an atomic DB backstop against concurrent
        // inserts. EF Core's default for a unique index on a nullable column is a FILTERED index
        // (WHERE EventType IS NOT NULL), which would let multiple catch-all (EventType IS NULL)
        // rows through per SystemCode. Explicitly unfilter it: in SQL Server, a UNIQUE INDEX treats
        // NULLs as equal to each other (only one NULL allowed per indexed key prefix), so the
        // unfiltered index enforces exactly one catch-all row per SystemCode too — same NULL
        // semantics apply either way (index or constraint) in SQL Server.
        builder.HasIndex(x => new { x.SystemCode, x.EventType })
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName("IX_WebhookSubscription_SystemCode_EventType");

        builder.HasIndex(x => new { x.SystemCode, x.EventType, x.IsActive })
            .HasDatabaseName("IX_WebhookSubscription_SystemCode_EventType_IsActive");
    }
}
