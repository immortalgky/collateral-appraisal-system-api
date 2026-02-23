namespace Appraisal.Infrastructure.Configurations;

public class AppraisalFeeConfiguration : IEntityTypeConfiguration<AppraisalFee>
{
    public void Configure(EntityTypeBuilder<AppraisalFee> builder)
    {
        builder.ToTable("AppraisalFees");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(f => f.AssignmentId).IsRequired();

        // Fee Totals
        builder.Property(f => f.TotalFeeBeforeVAT).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(f => f.VATRate).HasPrecision(5, 2).HasDefaultValue(7.00m);
        builder.Property(f => f.VATAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(f => f.TotalFeeAfterVAT).HasPrecision(18, 2).HasDefaultValue(0m);

        // Bank Absorb
        builder.Property(f => f.BankAbsorbAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(f => f.CustomerPayableAmount).HasPrecision(18, 2).HasDefaultValue(0m);

        // Payment Status
        builder.Property(f => f.TotalPaidAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(f => f.OutstandingAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(f => f.PaymentStatus).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");

        // Fee Metadata
        builder.Property(f => f.FeePaymentType).HasMaxLength(100);
        builder.Property(f => f.FeeNotes).HasMaxLength(4000);

        // InspectionFee
        builder.Property(f => f.InspectionFeeAmount).HasPrecision(18, 2);

        // FK to AppraisalAssignment (1:1, cascade delete per spec)
        builder.HasOne<AppraisalAssignment>()
            .WithOne()
            .HasForeignKey<AppraisalFee>(f => f.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationships
        builder.HasMany(f => f.Items)
            .WithOne()
            .HasForeignKey(i => i.AppraisalFeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.PaymentHistory)
            .WithOne()
            .HasForeignKey(p => p.AppraisalFeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(f => f.PaymentHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(f => f.AssignmentId).IsUnique();
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
        builder.Property(i => i.FeeCode).IsRequired().HasMaxLength(20);
        builder.Property(i => i.FeeDescription).IsRequired().HasMaxLength(200);
        builder.Property(i => i.FeeAmount).HasPrecision(18, 2);

        // Approval
        builder.Property(i => i.RequiresApproval).HasDefaultValue(false);
        builder.Property(i => i.ApprovalStatus).HasMaxLength(50);
        builder.Property(i => i.RejectionReason).HasMaxLength(4000);

        builder.HasIndex(i => i.AppraisalFeeId);
    }
}

public class AppraisalFeePaymentHistoryConfiguration : IEntityTypeConfiguration<AppraisalFeePaymentHistory>
{
    public void Configure(EntityTypeBuilder<AppraisalFeePaymentHistory> builder)
    {
        builder.ToTable("AppraisalFeePaymentHistory");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.AppraisalFeeId).IsRequired();
        builder.Property(p => p.PaymentAmount).HasPrecision(18, 2);
        builder.Property(p => p.PaymentDate).IsRequired();
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);
        builder.Property(p => p.PaymentReference).HasMaxLength(100);
        builder.Property(p => p.Remarks).HasMaxLength(4000);

        builder.HasIndex(p => p.AppraisalFeeId);
    }
}
