using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Notification.Models;
using System.Text.Json;

namespace Notification.Data.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ActionUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Metadata)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
            )
            .HasColumnType("nvarchar(max)");

        // Indexes for better query performance
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_UserNotifications_UserId");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_UserNotifications_CreatedAt");

        builder.HasIndex(x => new { x.UserId, x.IsRead })
            .HasDatabaseName("IX_UserNotifications_UserId_IsRead");

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_CreatedAt");
    }
}