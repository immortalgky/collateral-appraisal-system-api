using Appraisal.Domain.Invoices;

namespace Appraisal.Infrastructure.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique().HasFilter("[InvoiceNumber] IS NOT NULL");
        builder.Property(i => i.CompanyId).IsRequired();
        builder.HasIndex(i => i.CompanyId);
        builder.Property(i => i.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Draft");
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(i => i.Notes).HasMaxLength(4000);
        builder.Property(i => i.PaymentReference).HasMaxLength(200);
        builder.Property(i => i.PaymentMethod).HasMaxLength(100);
        builder.Property(i => i.ApprovedBy).HasMaxLength(100);

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
