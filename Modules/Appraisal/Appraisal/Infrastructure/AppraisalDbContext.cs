using MassTransit;

namespace Appraisal.Infrastructure;

public class AppraisalDbContext : DbContext
{
    public AppraisalDbContext(DbContextOptions<AppraisalDbContext> options) : base(options)
    {
    }

    // =====================================================
    // Core Entities
    // =====================================================
    public DbSet<Domain.Appraisals.Appraisal> Appraisals => Set<Domain.Appraisals.Appraisal>();
    public DbSet<AppraisalProperty> AppraisalProperties => Set<AppraisalProperty>();
    public DbSet<PropertyGroup> PropertyGroups => Set<PropertyGroup>();
    public DbSet<PropertyGroupItem> PropertyGroupItems => Set<PropertyGroupItem>();
    public DbSet<AppraisalAssignment> AppraisalAssignments => Set<AppraisalAssignment>();

    // =====================================================
    // Property Detail Entities
    // =====================================================
    public DbSet<LandAppraisalDetail> LandAppraisalDetails => Set<LandAppraisalDetail>();
    public DbSet<BuildingAppraisalDetail> BuildingAppraisalDetails => Set<BuildingAppraisalDetail>();
    public DbSet<CondoAppraisalDetail> CondoAppraisalDetails => Set<CondoAppraisalDetail>();
    public DbSet<VehicleAppraisalDetail> VehicleAppraisalDetails => Set<VehicleAppraisalDetail>();
    public DbSet<VesselAppraisalDetail> VesselAppraisalDetails => Set<VesselAppraisalDetail>();
    public DbSet<MachineryAppraisalDetail> MachineryAppraisalDetails => Set<MachineryAppraisalDetail>();

    // =====================================================
    // Valuation Entities
    // =====================================================
    public DbSet<ValuationAnalysis> ValuationAnalyses => Set<ValuationAnalysis>();
    public DbSet<GroupValuation> GroupValuations => Set<GroupValuation>();
    public DbSet<PropertyValuation> PropertyValuations => Set<PropertyValuation>();

    // =====================================================
    // Market Comparables
    // =====================================================
    public DbSet<MarketComparable> MarketComparables => Set<MarketComparable>();
    public DbSet<AppraisalComparable> AppraisalComparables => Set<AppraisalComparable>();
    public DbSet<ComparableAdjustment> ComparableAdjustments => Set<ComparableAdjustment>();
    public DbSet<AdjustmentTypeLookup> AdjustmentTypeLookups => Set<AdjustmentTypeLookup>();

    // =====================================================
    // Review & Committee Entities
    // =====================================================
    public DbSet<AppraisalReview> AppraisalReviews => Set<AppraisalReview>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<CommitteeApprovalCondition> CommitteeApprovalConditions => Set<CommitteeApprovalCondition>();
    public DbSet<CommitteeVote> CommitteeVotes => Set<CommitteeVote>();

    // =====================================================
    // Fee & Gallery Entities
    // =====================================================
    public DbSet<AppraisalFee> AppraisalFees => Set<AppraisalFee>();
    public DbSet<AppraisalFeeItem> AppraisalFeeItems => Set<AppraisalFeeItem>();
    public DbSet<AppraisalFeePaymentHistory> AppraisalFeePaymentHistories => Set<AppraisalFeePaymentHistory>();
    public DbSet<AppraisalGallery> AppraisalGallery => Set<AppraisalGallery>();
    public DbSet<PropertyPhotoMapping> PropertyPhotoMappings => Set<PropertyPhotoMapping>();

    // =====================================================
    // Settings & Rules
    // =====================================================
    public DbSet<AppraisalSettings> AppraisalSettings => Set<AppraisalSettings>();
    public DbSet<AutoAssignmentRule> AutoAssignmentRules => Set<AutoAssignmentRule>();

    // =====================================================
    // Document Requirements
    // =====================================================
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentRequirement> DocumentRequirements => Set<DocumentRequirement>();

    // =====================================================
    // Quotation Entities
    // =====================================================
    public DbSet<QuotationRequest> QuotationRequests => Set<QuotationRequest>();
    public DbSet<QuotationRequestItem> QuotationRequestItems => Set<QuotationRequestItem>();
    public DbSet<QuotationInvitation> QuotationInvitations => Set<QuotationInvitation>();
    public DbSet<CompanyQuotation> CompanyQuotations => Set<CompanyQuotation>();
    public DbSet<CompanyQuotationItem> CompanyQuotationItems => Set<CompanyQuotationItem>();
    public DbSet<QuotationNegotiation> QuotationNegotiations => Set<QuotationNegotiation>();

    // =====================================================
    // Appointment Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentHistory> AppointmentHistories => Set<AppointmentHistory>();

    // =====================================================
    // Pricing Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<PricingAnalysis> PricingAnalyses => Set<PricingAnalysis>();
    public DbSet<PricingAnalysisApproach> PricingAnalysisApproaches => Set<PricingAnalysisApproach>();
    public DbSet<PricingAnalysisMethod> PricingAnalysisMethods => Set<PricingAnalysisMethod>();
    public DbSet<PricingComparableLink> PricingComparableLinks => Set<PricingComparableLink>();
    public DbSet<PricingCalculation> PricingCalculations => Set<PricingCalculation>();
    public DbSet<PricingFinalValue> PricingFinalValues => Set<PricingFinalValue>();

    // =====================================================
    // Supporting Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<LandTitle> LandTitles => Set<LandTitle>();
    public DbSet<BuildingDepreciationDetail> BuildingDepreciationDetails => Set<BuildingDepreciationDetail>();
    public DbSet<BuildingAppraisalSurface> BuildingAppraisalSurfaces => Set<BuildingAppraisalSurface>();
    public DbSet<CondoAppraisalAreaDetail> CondoAppraisalAreaDetails => Set<CondoAppraisalAreaDetail>();
    public DbSet<LawAndRegulation> LawAndRegulations => Set<LawAndRegulation>();
    public DbSet<LawAndRegulationImage> LawAndRegulationImages => Set<LawAndRegulationImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set default schema
        modelBuilder.HasDefaultSchema("appraisal");

        // Apply global conventions (audit fields, etc.)
        modelBuilder.ApplyGlobalConventions();

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Soft delete query filter for new Appraisal aggregate
        modelBuilder.Entity<Domain.Appraisals.Appraisal>()
            .HasQueryFilter(a => !a.SoftDelete.IsDeleted);

        // MassTransit Outbox for reliable messaging
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        base.OnModelCreating(modelBuilder);
    }
}