using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Email;

namespace Notification.Data.Configurations;

public class EmailSendLogConfiguration : IEntityTypeConfiguration<EmailSendLog>
{
    public void Configure(EntityTypeBuilder<EmailSendLog> builder)
    {
        builder.ToTable("EmailSendLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();

        builder.Property(x => x.Source).HasMaxLength(50);

        builder.Property(x => x.ReferenceId).HasMaxLength(100);

        builder.Property(x => x.ToAddresses).HasMaxLength(1000);

        builder.Property(x => x.CcAddresses).HasMaxLength(1000);

        builder.Property(x => x.BccAddresses).HasMaxLength(1000);

        builder.Property(x => x.FromAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.RecipientCount).IsRequired();

        builder.Property(x => x.AttachmentCount).IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        // Error can be arbitrarily long SMTP exception text
        builder.Property(x => x.Error)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_EmailSendLogs_CreatedAt");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_EmailSendLogs_Status");

        builder.HasIndex(x => x.ReferenceId)
            .HasDatabaseName("IX_EmailSendLogs_ReferenceId");
    }
}
