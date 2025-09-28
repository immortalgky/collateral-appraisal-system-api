using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Data.Models;

namespace Shared.Messaging.OutboxPatterns.Configurations;

public class InboxMessageConfiguration: IEntityTypeConfiguration<InboxMessage>
{
        private readonly string _schema;

        public InboxMessageConfiguration(string schema)
        {
                _schema = schema;
        }

        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {

                builder.ToTable("InboxMessages", _schema);

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Id)
                    .ValueGeneratedNever();

                builder.Property(x => x.Id).HasColumnName("EventId");

                builder.Property(x => x.EventType)
                        .IsRequired()
                        .HasMaxLength(255);

                builder.Property(x => x.Payload)
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                builder.Property(x => x.OccurredOn)
                        .IsRequired();

                builder.Property(x => x.ReceiveAt)
                        .IsRequired();


                // Ignore audit fields from Entity<Guid>
                builder.Ignore(x => x.CreatedOn);
                builder.Ignore(x => x.CreatedBy);
                builder.Ignore(x => x.UpdatedOn);
                builder.Ignore(x => x.UpdatedBy);
        }
}
