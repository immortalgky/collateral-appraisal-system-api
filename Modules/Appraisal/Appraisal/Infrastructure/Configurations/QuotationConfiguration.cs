using Appraisal.Domain.Quotations;

namespace Appraisal.Infrastructure.Configurations;

public class QuotationRequestConfiguration : IEntityTypeConfiguration<QuotationRequest>
{
    public void Configure(EntityTypeBuilder<QuotationRequest> builder)
    {
        builder.ToTable("QuotationRequests");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(q => q.QuotationNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(q => q.QuotationNumber).IsUnique();

        builder.Property(q => q.RequestDate).IsRequired();
        builder.Property(q => q.DueDate).IsRequired();

        builder.Property(q => q.RequestDescription).HasMaxLength(500);
        builder.Property(q => q.SpecialRequirements);

        builder.Property(q => q.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");
        builder.Property(q => q.SelectionReason).HasMaxLength(500);

        builder.Property(q => q.RequestedBy).IsRequired();
        builder.Property(q => q.RequestedByName).IsRequired().HasMaxLength(200);

        builder.HasMany(q => q.Items)
            .WithOne()
            .HasForeignKey(i => i.QuotationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Invitations)
            .WithOne()
            .HasForeignKey(i => i.QuotationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Quotations)
            .WithOne()
            .HasForeignKey(cq => cq.QuotationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => q.DueDate);
    }
}

public class QuotationRequestItemConfiguration : IEntityTypeConfiguration<QuotationRequestItem>
{
    public void Configure(EntityTypeBuilder<QuotationRequestItem> builder)
    {
        builder.ToTable("QuotationRequestItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.QuotationRequestId).IsRequired();
        builder.Property(i => i.AppraisalId).IsRequired();
        builder.Property(i => i.ItemNumber).IsRequired();

        builder.Property(i => i.AppraisalNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.PropertyType).IsRequired().HasMaxLength(50);
        builder.Property(i => i.PropertyLocation).HasMaxLength(500);
        builder.Property(i => i.EstimatedValue).HasPrecision(18, 2);

        builder.Property(i => i.SpecialRequirements).HasMaxLength(500);

        builder.HasIndex(i => i.QuotationRequestId);
        builder.HasIndex(i => i.AppraisalId);
    }
}

public class QuotationInvitationConfiguration : IEntityTypeConfiguration<QuotationInvitation>
{
    public void Configure(EntityTypeBuilder<QuotationInvitation> builder)
    {
        builder.ToTable("QuotationInvitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.QuotationRequestId).IsRequired();
        builder.Property(i => i.CompanyId).IsRequired();
        builder.Property(i => i.InvitedAt).IsRequired();

        builder.Property(i => i.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");

        builder.HasIndex(i => i.QuotationRequestId);
        builder.HasIndex(i => i.CompanyId);
        builder.HasIndex(i => i.Status);
    }
}

public class CompanyQuotationConfiguration : IEntityTypeConfiguration<CompanyQuotation>
{
    public void Configure(EntityTypeBuilder<CompanyQuotation> builder)
    {
        builder.ToTable("CompanyQuotations");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(q => q.QuotationRequestId).IsRequired();
        builder.Property(q => q.InvitationId).IsRequired();
        builder.Property(q => q.CompanyId).IsRequired();

        builder.Property(q => q.QuotationNumber).IsRequired().HasMaxLength(50);
        builder.Property(q => q.SubmittedAt).IsRequired();

        builder.Property(q => q.TotalQuotedPrice).HasPrecision(18, 2);
        builder.Property(q => q.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("THB");

        builder.Property(q => q.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Submitted");

        builder.Property(q => q.SubmittedByName).HasMaxLength(200);
        builder.Property(q => q.SubmittedByEmail).HasMaxLength(100);
        builder.Property(q => q.SubmittedByPhone).HasMaxLength(20);

        builder.HasMany(q => q.Items)
            .WithOne()
            .HasForeignKey(i => i.CompanyQuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Negotiations)
            .WithOne()
            .HasForeignKey(n => n.CompanyQuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.QuotationRequestId);
        builder.HasIndex(q => q.CompanyId);
        builder.HasIndex(q => q.Status);
    }
}

public class CompanyQuotationItemConfiguration : IEntityTypeConfiguration<CompanyQuotationItem>
{
    public void Configure(EntityTypeBuilder<CompanyQuotationItem> builder)
    {
        builder.ToTable("CompanyQuotationItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.CompanyQuotationId).IsRequired();
        builder.Property(i => i.QuotationRequestItemId).IsRequired();
        builder.Property(i => i.AppraisalId).IsRequired();
        builder.Property(i => i.ItemNumber).IsRequired();

        builder.Property(i => i.QuotedPrice).HasPrecision(18, 2);
        builder.Property(i => i.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("THB");
        builder.Property(i => i.PriceBreakdown).HasMaxLength(500);

        builder.Property(i => i.OriginalQuotedPrice).HasPrecision(18, 2);
        builder.Property(i => i.CurrentNegotiatedPrice).HasPrecision(18, 2);

        builder.HasIndex(i => i.CompanyQuotationId);
        builder.HasIndex(i => i.AppraisalId);
    }
}

public class QuotationNegotiationConfiguration : IEntityTypeConfiguration<QuotationNegotiation>
{
    public void Configure(EntityTypeBuilder<QuotationNegotiation> builder)
    {
        builder.ToTable("QuotationNegotiations");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(n => n.CompanyQuotationId).IsRequired();
        builder.Property(n => n.QuotationItemId).IsRequired();
        builder.Property(n => n.NegotiationRound).IsRequired();

        builder.Property(n => n.InitiatedBy).IsRequired().HasMaxLength(50);
        builder.Property(n => n.InitiatedAt).IsRequired();

        builder.Property(n => n.CounterPrice).HasPrecision(18, 2);
        builder.Property(n => n.Message).IsRequired();

        builder.Property(n => n.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");

        builder.HasIndex(n => n.CompanyQuotationId);
        builder.HasIndex(n => n.QuotationItemId);
        builder.HasIndex(n => n.Status);
    }
}