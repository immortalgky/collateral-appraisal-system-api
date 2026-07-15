namespace Appraisal.Infrastructure.Configurations;

public class PricingDocumentConfiguration
    : IOwnedEntityConfiguration<Domain.Appraisals.PricingAnalysis, PricingAnalysisDocument>
{
    public void Configure(OwnedNavigationBuilder<PricingAnalysis, PricingAnalysisDocument> builder)
    {
        builder.ToTable("PricingAnalysisDocuments");
        builder.WithOwner().HasForeignKey(d => d.PricingAnalysisId);
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.FilePath).HasMaxLength(500);
        builder.Property(d => d.UploadedBy).HasMaxLength(10);
        builder.Property(d => d.UploadedByName).HasMaxLength(100);

        builder.HasIndex(d => new { d.PricingAnalysisId, d.DocumentId })
            .IsUnique()
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_PricingAnalysisDocument_PricingAnalysis_Document");
    }
}
