using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Data.Models;

namespace Shared.Messaging.OutboxPatterns.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private readonly string _schema;

    public OutboxMessageConfiguration(string schema)
    {
        _schema = schema;
    }

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Table configuration
        builder.ToTable("OutboxMessages", _schema);
        
        // Primary key
        builder.HasKey(x => x.Id);
        
        // Properties configuration
        builder.Property(x => x.Id)
            .ValueGeneratedNever(); // Guid จะถูกกำหนดจากภายนอก
            
        builder.Property(x => x.OccurredOn)
            .IsRequired();
            
        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
            
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(x => x.ExceptionInfo)
            .HasMaxLength(4000);
            
        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);
            
        builder.Property(x => x.MaxRetries)
            .HasDefaultValue(3);
            
        builder.Property(x => x.IsInfrastructureFailure)
            .HasDefaultValue(false);

        // Ignore audit fields from Entity<Guid>
        builder.Ignore(x => x.CreatedOn);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedOn);
        builder.Ignore(x => x.UpdatedBy);
    }
}