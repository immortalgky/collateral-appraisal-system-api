namespace Appraisal.Infrastructure.Configurations;

public class AppraisalFeeConfiguration : IEntityTypeConfiguration<AppraisalFee>
{
    public void Configure(EntityTypeBuilder<AppraisalFee> builder)
    {
        builder.ToTable("AppraisalFees");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.AppraisalId).IsRequired();
        builder.Property(f => f.FeeType).IsRequired().HasMaxLength(50);
        builder.Property(f => f.FeeCategory).IsRequired().HasMaxLength(50);
        builder.Property(f => f.Description).IsRequired().HasMaxLength(200);

        builder.Property(f => f.Amount).HasPrecision(18, 2);
        builder.Property(f => f.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("THB");

        builder.Property(f => f.VATRate).HasPrecision(5, 2);
        builder.Property(f => f.VATAmount).HasPrecision(18, 2);
        builder.Property(f => f.WithholdingTaxRate).HasPrecision(5, 2);
        builder.Property(f => f.WithholdingTaxAmount).HasPrecision(18, 2);
        builder.Property(f => f.NetAmount).HasPrecision(18, 2);

        builder.Property(f => f.InvoiceNumber).HasMaxLength(50);
        builder.Property(f => f.PaymentStatus).HasMaxLength(50);
        builder.Property(f => f.CostCenter).HasMaxLength(50);

        builder.Property(f => f.CreatedOn).IsRequired();
        builder.Property(f => f.CreatedBy).IsRequired();

        builder.HasMany(f => f.Items)
            .WithOne()
            .HasForeignKey(i => i.AppraisalFeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(f => f.AppraisalId);
        builder.HasIndex(f => f.AssignmentId);
        builder.HasIndex(f => f.PaymentStatus);
    }
}

public class AppraisalFeeItemConfiguration : IEntityTypeConfiguration<AppraisalFeeItem>
{
    public void Configure(EntityTypeBuilder<AppraisalFeeItem> builder)
    {
        builder.ToTable("AppraisalFeeItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.AppraisalFeeId).IsRequired();
        builder.Property(i => i.ItemType).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.Property(i => i.VATRate).HasPrecision(5, 2);
        builder.Property(i => i.VATAmount).HasPrecision(18, 2);
        builder.Property(i => i.NetAmount).HasPrecision(18, 2);

        builder.Property(i => i.PaymentStatus).IsRequired().HasMaxLength(50);

        builder.Property(i => i.CreatedOn).IsRequired();
        builder.Property(i => i.CreatedBy).IsRequired();

        builder.HasMany(i => i.PaymentHistory)
            .WithOne()
            .HasForeignKey(p => p.AppraisalFeeItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.PaymentHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => i.AppraisalFeeId);
        builder.HasIndex(i => i.PaymentStatus);
    }
}

public class AppraisalFeePaymentHistoryConfiguration : IEntityTypeConfiguration<AppraisalFeePaymentHistory>
{
    public void Configure(EntityTypeBuilder<AppraisalFeePaymentHistory> builder)
    {
        builder.ToTable("AppraisalFeePaymentHistory");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.AppraisalFeeItemId).IsRequired();
        builder.Property(p => p.PaidAmount).HasPrecision(18, 2);
        builder.Property(p => p.PaymentDate).IsRequired();
        builder.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(50);
        builder.Property(p => p.PaymentReference).HasMaxLength(100);

        builder.Property(p => p.Status).IsRequired().HasMaxLength(50);

        builder.Property(p => p.RefundAmount).HasPrecision(18, 2);
        builder.Property(p => p.RefundReason).HasMaxLength(500);

        builder.HasIndex(p => p.AppraisalFeeItemId);
        builder.HasIndex(p => p.Status);
    }
}