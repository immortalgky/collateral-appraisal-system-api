using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.ComparativeAnalysis;
using Shared.Data.Outbox;

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
    public DbSet<MachineryAppraisalSummary> MachineryAppraisalSummaries => Set<MachineryAppraisalSummary>();

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
    // Market Comparable Templates (EAV System)
    // =====================================================
    public DbSet<MarketComparableTemplate> MarketComparableTemplates => Set<MarketComparableTemplate>();

    public DbSet<MarketComparableTemplateFactor> MarketComparableTemplateFactors =>
        Set<MarketComparableTemplateFactor>();

    public DbSet<MarketComparableFactor> MarketComparableFactors => Set<MarketComparableFactor>();
    public DbSet<MarketComparableData> MarketComparableData => Set<MarketComparableData>();
    public DbSet<MarketComparableImage> MarketComparableImages => Set<MarketComparableImage>();

    // =====================================================
    // Review & Committee Entities
    // =====================================================
    public DbSet<AppraisalReview> AppraisalReviews => Set<AppraisalReview>();
    public DbSet<AppraisalDecision> AppraisalDecisions => Set<AppraisalDecision>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<CommitteeApprovalCondition> CommitteeApprovalConditions => Set<CommitteeApprovalCondition>();
    public DbSet<CommitteeVote> CommitteeVotes => Set<CommitteeVote>();
    public DbSet<CommitteeThreshold> CommitteeThresholds => Set<CommitteeThreshold>();

    // =====================================================
    // Fee & Gallery Entities
    // =====================================================
    public DbSet<AppraisalFee> AppraisalFees => Set<AppraisalFee>();
    public DbSet<AppraisalFeeItem> AppraisalFeeItems => Set<AppraisalFeeItem>();
    public DbSet<AppraisalFeePaymentHistory> AppraisalFeePaymentHistories => Set<AppraisalFeePaymentHistory>();
    public DbSet<AppraisalGallery> AppraisalGallery => Set<AppraisalGallery>();
    public DbSet<PropertyPhotoMapping> PropertyPhotoMappings => Set<PropertyPhotoMapping>();
    public DbSet<GalleryPhotoTopicMapping> GalleryPhotoTopicMappings => Set<GalleryPhotoTopicMapping>();
    public DbSet<PhotoTopic> PhotoTopics => Set<PhotoTopic>();

    // =====================================================
    // Settings & Rules
    // =====================================================
    public DbSet<AppraisalSettings> AppraisalSettings => Set<AppraisalSettings>();
    public DbSet<AutoAssignmentRule> AutoAssignmentRules => Set<AutoAssignmentRule>();
    public DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();

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
    public DbSet<PricingFactorScore> PricingFactorScores => Set<PricingFactorScore>();
    public DbSet<PricingFinalValue> PricingFinalValues => Set<PricingFinalValue>();
    public DbSet<PricingComparativeFactor> PricingComparativeFactors => Set<PricingComparativeFactor>();
    public DbSet<PricingRsqResult> PricingRsqResults => Set<PricingRsqResult>();
    public DbSet<MachineCostItem> MachineCostItems => Set<MachineCostItem>();
    public DbSet<LeaseholdAnalysis> LeaseholdAnalyses => Set<LeaseholdAnalysis>();
    public DbSet<LeaseholdLandGrowthPeriod> LeaseholdLandGrowthPeriods => Set<LeaseholdLandGrowthPeriod>();
    public DbSet<LeaseholdCalculationDetail> LeaseholdCalculationDetails => Set<LeaseholdCalculationDetail>();
    public DbSet<ProfitRentAnalysis> ProfitRentAnalyses => Set<ProfitRentAnalysis>();
    public DbSet<ProfitRentGrowthPeriod> ProfitRentGrowthPeriods => Set<ProfitRentGrowthPeriod>();
    public DbSet<ProfitRentCalculationDetail> ProfitRentCalculationDetails => Set<ProfitRentCalculationDetail>();
    public DbSet<IncomeAnalysis> IncomeAnalyses => Set<IncomeAnalysis>();
    public DbSet<IncomeSection> IncomeSections => Set<IncomeSection>();
    public DbSet<IncomeCategory> IncomeCategories => Set<IncomeCategory>();
    public DbSet<IncomeAssumption> IncomeAssumptions => Set<IncomeAssumption>();

    // =====================================================
    // Comparative Analysis Templates
    // =====================================================
    public DbSet<ComparativeAnalysisTemplate> ComparativeAnalysisTemplates => Set<ComparativeAnalysisTemplate>();

    public DbSet<ComparativeAnalysisTemplateFactor> ComparativeAnalysisTemplateFactors =>
        Set<ComparativeAnalysisTemplateFactor>();

    // =====================================================
    // Block Condo Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<CondoProject> CondoProjects => Set<CondoProject>();
    public DbSet<CondoModel> CondoModels => Set<CondoModel>();
    public DbSet<CondoModelAreaDetail> CondoModelAreaDetails => Set<CondoModelAreaDetail>();
    public DbSet<CondoTower> CondoTowers => Set<CondoTower>();
    public DbSet<CondoUnit> CondoUnits => Set<CondoUnit>();
    public DbSet<CondoUnitUpload> CondoUnitUploads => Set<CondoUnitUpload>();
    public DbSet<CondoUnitPrice> CondoUnitPrices => Set<CondoUnitPrice>();
    public DbSet<CondoPricingAssumption> CondoPricingAssumptions => Set<CondoPricingAssumption>();
    public DbSet<CondoModelAssumption> CondoModelAssumptions => Set<CondoModelAssumption>();

    // =====================================================
    // Block Village Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<VillageProject> VillageProjects => Set<VillageProject>();
    public DbSet<VillageProjectLand> VillageProjectLands => Set<VillageProjectLand>();
    public DbSet<VillageModel> VillageModels => Set<VillageModel>();
    public DbSet<VillageUnit> VillageUnits => Set<VillageUnit>();
    public DbSet<VillageUnitUpload> VillageUnitUploads => Set<VillageUnitUpload>();
    public DbSet<VillageUnitPrice> VillageUnitPrices => Set<VillageUnitPrice>();
    public DbSet<VillagePricingAssumption> VillagePricingAssumptions => Set<VillagePricingAssumption>();

    // =====================================================
    // Supporting Entities (part of Appraisal aggregate)
    // =====================================================
    public DbSet<LandTitle> LandTitles => Set<LandTitle>();

    // BuildingDepreciationDetail removed - now owned by BuildingAppraisalDetail via OwnsMany
    // BuildingAppraisalSurface removed - now owned by BuildingAppraisalDetail via OwnsMany
    public DbSet<CondoAppraisalAreaDetail> CondoAppraisalAreaDetails => Set<CondoAppraisalAreaDetail>();
    public DbSet<LawAndRegulation> LawAndRegulations => Set<LawAndRegulation>();
    public DbSet<LawAndRegulationImage> LawAndRegulationImages => Set<LawAndRegulationImage>();

    // =====================================================
    // Appendix Entities
    // =====================================================
    public DbSet<AppendixType> AppendixTypes => Set<AppendixType>();
    public DbSet<AppraisalAppendix> AppraisalAppendices => Set<AppraisalAppendix>();
    public DbSet<AppendixDocument> AppendixDocuments => Set<AppendixDocument>();

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

        modelBuilder.Entity<AppraisalAssignment>()
            .HasQueryFilter(a =>
                a.AssignmentStatus != AssignmentStatus.Rejected && a.AssignmentStatus != AssignmentStatus.Cancelled);


        modelBuilder.Entity<MarketComparable>()
            .HasQueryFilter(m => !m.SoftDelete.IsDeleted);

        // Integration event outbox for reliable messaging
        modelBuilder.AddIntegrationEventOutbox();

        base.OnModelCreating(modelBuilder);
    }
}