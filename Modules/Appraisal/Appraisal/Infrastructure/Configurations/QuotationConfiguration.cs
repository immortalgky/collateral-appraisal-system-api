using Appraisal.Domain.Quotations;

namespace Appraisal.Infrastructure.Configurations;

public class QuotationRequestConfiguration : IEntityTypeConfiguration<QuotationRequest>
{
    public void Configure(EntityTypeBuilder<QuotationRequest> builder)
    {
        builder.ToTable("QuotationRequests");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Concurrency token — prevents RM vs Admin race on PickTentativeWinner / shortlist ops
        builder.Property(q => q.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(q => q.QuotationNumber).HasMaxLength(50);
        builder.HasIndex(q => q.QuotationNumber).IsUnique().HasFilter("[QuotationNumber] IS NOT NULL");

        builder.Property(q => q.RequestDate).IsRequired();
        builder.Property(q => q.DueDate).IsRequired();

        builder.Property(q => q.RequestDescription).HasMaxLength(500);
        builder.Property(q => q.SpecialRequirements);

        // Extended status vocabulary (v2 — AppraisalId scalar removed):
        //   Draft | Sent | Closed (legacy) | Cancelled |
        //   UnderAdminReview | PendingRmSelection | WinnerTentative | Negotiating | Finalized
        builder.Property(q => q.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");
        builder.Property(q => q.SelectionReason).HasMaxLength(500);
        builder.Property(q => q.CancellationReason).HasMaxLength(500);

        builder.Property(q => q.RequestedBy).IsRequired().HasMaxLength(50);

        // ── RM identity (denormalised for access-policy evaluation) ──────────
        builder.Property(q => q.RmUserId);
        builder.Property(q => q.RmUsername).HasMaxLength(50);

        // ── IBG workflow link fields ──────────────────────────────────────────
        // NOTE: AppraisalId column was dropped in v2. Use QuotationRequestAppraisals join table.
        builder.Property(q => q.RequestId);
        builder.Property(q => q.WorkflowInstanceId);
        builder.Property(q => q.TaskExecutionId);
        builder.Property(q => q.BankingSegment).HasMaxLength(50);

        // ── Shortlist tracking ────────────────────────────────────────────────
        builder.Property(q => q.SubmissionsClosedAt);
        builder.Property(q => q.ShortlistSentToRmAt);
        builder.Property(q => q.ShortlistSentByAdminId);
        builder.Property(q => q.TotalShortlisted).HasDefaultValue(0);

        // ── Tentative winner tracking ─────────────────────────────────────────
        builder.Property(q => q.TentativeWinnerQuotationId);
        builder.Property(q => q.TentativelySelectedAt);
        builder.Property(q => q.TentativelySelectedBy);
        builder.Property(q => q.TentativelySelectedByRole).HasMaxLength(20);

        // ── v4: RM negotiation recommendation (set at rm-pick-winner step) ─────
        builder.Property(q => q.RmRequestsNegotiation).HasDefaultValue(false);
        builder.Property(q => q.RmNegotiationNote).HasMaxLength(1000);

        // ── v4: quotation child workflow instance link ─────────────────────────
        builder.Property(q => q.QuotationWorkflowInstanceId);
        builder.HasIndex(q => q.QuotationWorkflowInstanceId)
            .HasDatabaseName("IX_QuotationRequests_QuotationWorkflowInstanceId");

        // ── v2: ignore computed convenience property (not a mapped column) ─────
        builder.Ignore(q => q.FirstAppraisalId);

        // ── Relationships ─────────────────────────────────────────────────────
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

        // v2: multi-appraisal join
        builder.HasMany(q => q.Appraisals)
            .WithOne()
            .HasForeignKey(a => a.QuotationRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // v4: shared documents — cascade delete: removing a QuotationRequest removes all its shared doc entries
        builder.HasMany(q => q.SharedDocuments)
            .WithOne()
            .HasForeignKey(d => d.QuotationRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuotationSharedDocuments_QuotationRequests_QuotationRequestId");

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => q.DueDate);
        builder.HasIndex(q => q.WorkflowInstanceId).HasDatabaseName("IX_QuotationRequests_WorkflowInstanceId");
        builder.HasIndex(q => new { q.Status, q.DueDate }).HasDatabaseName("IX_QuotationRequests_Status_DueDate");
    }
}

public class QuotationRequestAppraisalConfiguration : IEntityTypeConfiguration<QuotationRequestAppraisal>
{
    public void Configure(EntityTypeBuilder<QuotationRequestAppraisal> builder)
    {
        builder.ToTable("QuotationRequestAppraisals");

        // Composite primary key
        builder.HasKey(a => new { a.QuotationRequestId, a.AppraisalId });

        builder.Property(a => a.QuotationRequestId).IsRequired();
        builder.Property(a => a.AppraisalId).IsRequired();
        builder.Property(a => a.AddedAt).IsRequired();
        builder.Property(a => a.AddedBy).IsRequired().HasMaxLength(450);

        // ── FK: AppraisalId → Appraisals.Id (Restrict on delete) ─────────────
        // M5: FK added in follow-up migration AddFkQuotationRequestAppraisalToAppraisal.
        builder.HasOne<Domain.Appraisals.Appraisal>()
            .WithMany()
            .HasForeignKey(a => a.AppraisalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_QuotationRequestAppraisals_Appraisals_AppraisalId");

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(a => a.AppraisalId)
            .HasDatabaseName("IX_QuotationRequestAppraisals_AppraisalId");
        builder.HasIndex(a => a.QuotationRequestId)
            .HasDatabaseName("IX_QuotationRequestAppraisals_QuotationRequestId");
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

        // Extended status vocabulary:
        //   Submitted | UnderReview | Tentative | Negotiating | Accepted | Rejected | Withdrawn
        builder.Property(q => q.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Submitted");

        builder.Property(q => q.SubmittedByName).HasMaxLength(200);
        builder.Property(q => q.SubmittedByEmail).HasMaxLength(100);
        builder.Property(q => q.SubmittedByPhone).HasMaxLength(20);

        // ── Shortlist + negotiation fields ────────────────────────────────────
        builder.Property(q => q.IsShortlisted).HasDefaultValue(false);
        builder.Property(q => q.OriginalQuotedPrice).HasPrecision(18, 2);
        builder.Property(q => q.CurrentNegotiatedPrice).HasPrecision(18, 2);
        builder.Property(q => q.NegotiationRounds).HasDefaultValue(0);

        // ── v2: Decline fields ────────────────────────────────────────────────
        builder.Property(q => q.DeclineReason).HasMaxLength(500);
        builder.Property(q => q.DeclinedAt);
        builder.Property(q => q.DeclinedBy).HasMaxLength(450);

        // Captured when admin rejects this row as the tentative winner (RejectTentativeWinner flow).
        builder.Property(q => q.WithdrawalReason).HasMaxLength(500);

        // ── Relationships ─────────────────────────────────────────────────────
        builder.HasMany(q => q.Items)
            .WithOne()
            .HasForeignKey(i => i.CompanyQuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Negotiations)
            .WithOne()
            .HasForeignKey(n => n.CompanyQuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(q => q.QuotationRequestId);
        builder.HasIndex(q => q.CompanyId);
        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => new { q.QuotationRequestId, q.IsShortlisted })
            .HasDatabaseName("IX_CompanyQuotations_QuotationRequestId_IsShortlisted");
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

        // ── Fee breakdown columns (Maker/Checker flow) ────────────────────────
        builder.Property(i => i.FeeAmount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(i => i.Discount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(i => i.NegotiatedDiscount).HasPrecision(18, 2).IsRequired(false);
        builder.Property(i => i.VatPercent).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();

        // Derived computed helpers — not persisted
        builder.Ignore(i => i.FeeAfterDiscount);
        builder.Ignore(i => i.VatAmount);
        builder.Ignore(i => i.NetAmount);

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
        // Nullable — quotation-level negotiations in the IBG flow do not reference a specific item
        builder.Property(n => n.QuotationItemId).IsRequired(false);
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

public class QuotationActivityLogConfiguration : IEntityTypeConfiguration<QuotationActivityLog>
{
    public void Configure(EntityTypeBuilder<QuotationActivityLog> builder)
    {
        builder.ToTable("QuotationActivityLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuotationRequestId).IsRequired();
        builder.Property(x => x.ActivityName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ActionAt).IsRequired();
        builder.Property(x => x.ActionBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ActionByRole).HasMaxLength(50);
        builder.Property(x => x.Remark).HasMaxLength(1000);

        builder.HasIndex(x => x.QuotationRequestId)
            .HasDatabaseName("IX_QuotationActivityLogs_QuotationRequestId");
        builder.HasIndex(x => new { x.QuotationRequestId, x.ActionAt })
            .HasDatabaseName("IX_QuotationActivityLogs_QuotationRequestId_ActionAt");
    }
}

public class QuotationSharedDocumentConfiguration : IEntityTypeConfiguration<QuotationSharedDocument>
{
    public void Configure(EntityTypeBuilder<QuotationSharedDocument> builder)
    {
        builder.ToTable("QuotationSharedDocuments");

        // Composite PK: (QuotationRequestId, DocumentId) — one doc cannot be shared twice
        // to the same quotation regardless of appraisal.
        builder.HasKey(d => new { d.QuotationRequestId, d.DocumentId });

        builder.Property(d => d.QuotationRequestId).IsRequired();
        builder.Property(d => d.DocumentId).IsRequired();
        builder.Property(d => d.AppraisalId).IsRequired();
        builder.Property(d => d.Level).IsRequired().HasMaxLength(30);
        builder.Property(d => d.SharedAt).IsRequired();
        builder.Property(d => d.SharedBy).IsRequired().HasMaxLength(450);

        builder.HasIndex(d => d.QuotationRequestId)
            .HasDatabaseName("IX_QuotationSharedDocuments_QuotationRequestId");
        builder.HasIndex(d => new { d.QuotationRequestId, d.AppraisalId })
            .HasDatabaseName("IX_QuotationSharedDocuments_QuotationRequestId_AppraisalId");
    }
}
