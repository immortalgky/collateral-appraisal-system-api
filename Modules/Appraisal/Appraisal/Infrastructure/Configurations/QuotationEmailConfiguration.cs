using Appraisal.Domain.Quotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Infrastructure.Configurations;

public class QuotationEmailConfiguration : IEntityTypeConfiguration<QuotationEmail>
{
    public void Configure(EntityTypeBuilder<QuotationEmail> builder)
    {
        builder.ToTable("QuotationEmails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.From).HasMaxLength(500).IsRequired();
        builder.Property(e => e.To).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Cc).HasMaxLength(500);
        builder.Property(e => e.Bcc).HasMaxLength(500);
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Content).HasMaxLength(4000);
        builder.HasIndex(e => e.QuotationRequestId);
    }
}
