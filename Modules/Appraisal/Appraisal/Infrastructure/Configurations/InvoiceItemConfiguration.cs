using Appraisal.Domain.Invoices;

namespace Appraisal.Infrastructure.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");
        builder.HasKey(ii => ii.Id);
        builder.Property(ii => ii.Id).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedNever();
        builder.Property(ii => ii.InvoiceId).IsRequired();
        builder.Property(ii => ii.AssignmentId).IsRequired();
        builder.HasIndex(ii => ii.AssignmentId).IsUnique();
        builder.Property(ii => ii.AppraisalFeeId).IsRequired();
        builder.Property(ii => ii.BankAbsorbAmount).HasPrecision(18, 2);
        builder.Property(ii => ii.AppraisalNumber).HasMaxLength(50);
        builder.Property(ii => ii.CustomerName).HasMaxLength(200);
        builder.Property(ii => ii.ProductType).HasMaxLength(100);
        builder.Property(ii => ii.FeeBeforeVAT).HasPrecision(18, 2);
        builder.Property(ii => ii.VATRate).HasPrecision(5, 2);
        builder.Property(ii => ii.VATAmount).HasPrecision(18, 2);
        builder.Property(ii => ii.TotalFeeAfterVAT).HasPrecision(18, 2);
        builder.Property(ii => ii.SubmittedDate).HasColumnType("datetime2");
        builder.HasIndex(ii => ii.InvoiceId);
    }
}