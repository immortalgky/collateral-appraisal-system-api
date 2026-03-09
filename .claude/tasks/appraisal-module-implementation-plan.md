# Appraisal Module Implementation Plan

## Overview

Implement the Appraisal module following the established DDD and CQRS patterns from Request and Document modules. The module handles the complete appraisal lifecycle from assignment to completion.

---

## Phase 1: Project Structure & Foundation

### 1.1 Create Project Structure

```
Modules/Appraisal/
├── Appraisal/                              # Main implementation
│   ├── Domain/                             # DDD Domain Layer
│   │   ├── Appraisals/                     # Appraisal Aggregate Root
│   │   │   ├── Appraisal.cs
│   │   │   ├── AppraisalStatus.cs          # Value Object
│   │   │   ├── Events/
│   │   │   │   ├── AppraisalCreatedEvent.cs
│   │   │   │   ├── AppraisalAssignedEvent.cs
│   │   │   │   ├── AppraisalCompletedEvent.cs
│   │   │   │   └── AppraisalStatusChangedEvent.cs
│   │   │   ├── Exceptions/
│   │   │   │   ├── AppraisalNotFoundException.cs
│   │   │   │   └── InvalidAppraisalStateException.cs
│   │   │   └── IAppraisalRepository.cs
│   │   │
│   │   ├── Collaterals/                    # AppraisalCollateral Entity
│   │   │   ├── AppraisalCollateral.cs
│   │   │   ├── CollateralType.cs           # Value Object
│   │   │   └── IAppraisalCollateralRepository.cs
│   │   │
│   │   ├── CollateralGroups/               # CollateralGroup Entity
│   │   │   ├── CollateralGroup.cs
│   │   │   ├── CollateralGroupItem.cs
│   │   │   └── ICollateralGroupRepository.cs
│   │   │
│   │   ├── Assignments/                    # AppraisalAssignment Entity
│   │   │   ├── AppraisalAssignment.cs
│   │   │   ├── AssignmentStatus.cs         # Value Object
│   │   │   ├── AssignmentMode.cs           # Value Object
│   │   │   ├── Events/
│   │   │   │   └── AssignmentCreatedEvent.cs
│   │   │   └── IAppraisalAssignmentRepository.cs
│   │   │
│   │   ├── PropertyDetails/                # Property Detail Entities (7 types)
│   │   │   ├── LandAppraisalDetail.cs
│   │   │   ├── BuildingAppraisalDetail.cs
│   │   │   ├── LandAndBuildingAppraisalDetail.cs
│   │   │   ├── CondoAppraisalDetail.cs
│   │   │   ├── VehicleAppraisalDetail.cs
│   │   │   ├── VesselAppraisalDetail.cs
│   │   │   ├── MachineryAppraisalDetail.cs
│   │   │   └── IPropertyDetailRepository.cs
│   │   │
│   │   ├── Valuations/                     # Valuation Entities
│   │   │   ├── ValuationAnalysis.cs
│   │   │   ├── GroupValuation.cs
│   │   │   └── IValuationRepository.cs
│   │   │
│   │   ├── Reviews/                        # Review Entities
│   │   │   ├── AppraisalReview.cs
│   │   │   ├── ReviewStatus.cs             # Value Object
│   │   │   ├── Events/
│   │   │   │   └── ReviewCompletedEvent.cs
│   │   │   └── IAppraisalReviewRepository.cs
│   │   │
│   │   ├── Committees/                     # Committee Aggregate Root
│   │   │   ├── Committee.cs
│   │   │   ├── CommitteeMember.cs
│   │   │   ├── CommitteeApprovalCondition.cs
│   │   │   ├── CommitteeVote.cs
│   │   │   └── ICommitteeRepository.cs
│   │   │
│   │   ├── Fees/                           # Fee Entities
│   │   │   ├── AppraisalFee.cs
│   │   │   ├── AppraisalFeeItem.cs
│   │   │   ├── AppraisalFeePaymentHistory.cs
│   │   │   └── IAppraisalFeeRepository.cs
│   │   │
│   │   ├── Gallery/                        # Photo Gallery Entities
│   │   │   ├── AppraisalGallery.cs
│   │   │   ├── PropertyPhotoMapping.cs
│   │   │   └── IAppraisalGalleryRepository.cs
│   │   │
│   │   ├── MarketComparables/              # Market Comparable Aggregate Root
│   │   │   ├── MarketComparable.cs
│   │   │   ├── MarketComparableTemplate.cs
│   │   │   ├── MarketComparableFactor.cs
│   │   │   ├── MarketComparableData.cs
│   │   │   ├── MarketComparableImage.cs
│   │   │   └── IMarketComparableRepository.cs
│   │   │
│   │   ├── AppraisalComparables/           # Appraisal-Comparable Links
│   │   │   ├── AppraisalComparable.cs
│   │   │   ├── ComparableAdjustment.cs
│   │   │   └── IAppraisalComparableRepository.cs
│   │   │
│   │   ├── Quotations/                     # Quotation Aggregate Root
│   │   │   ├── QuotationRequest.cs
│   │   │   ├── QuotationRequestItem.cs
│   │   │   ├── QuotationInvitation.cs
│   │   │   ├── CompanyQuotation.cs
│   │   │   ├── CompanyQuotationItem.cs
│   │   │   ├── QuotationNegotiation.cs
│   │   │   └── IQuotationRepository.cs
│   │   │
│   │   ├── Appointments/                   # Appointment Entities
│   │   │   ├── Appointment.cs
│   │   │   ├── AppointmentHistory.cs
│   │   │   └── IAppointmentRepository.cs
│   │   │
│   │   ├── Pricing/                        # Pricing Analysis Entities
│   │   │   ├── PricingAnalysis.cs
│   │   │   ├── PricingAnalysisApproach.cs
│   │   │   ├── PricingAnalysisMethod.cs
│   │   │   ├── PricingCalculation.cs
│   │   │   ├── PricingFinalValue.cs
│   │   │   └── IPricingAnalysisRepository.cs
│   │   │
│   │   ├── Supporting/                     # Supporting Entities
│   │   │   ├── LandTitle.cs
│   │   │   ├── BuildingDepreciationDetail.cs
│   │   │   ├── BuildingAppraisalSurface.cs
│   │   │   ├── CondoAppraisalAreaDetail.cs
│   │   │   ├── LawAndRegulation.cs
│   │   │   ├── LawAndRegulationImage.cs
│   │   │   └── AutoAssignmentRule.cs
│   │   │
│   │   └── Settings/                       # Module Settings
│   │       ├── AppraisalSettings.cs
│   │       └── IAppraisalSettingsRepository.cs
│   │
│   ├── Application/                        # Application Layer
│   │   ├── Features/                       # CQRS Commands & Queries
│   │   │   ├── Appraisals/
│   │   │   │   ├── CreateAppraisal/
│   │   │   │   ├── UpdateAppraisal/
│   │   │   │   ├── AssignAppraisal/
│   │   │   │   ├── SubmitForReview/
│   │   │   │   ├── CompleteAppraisal/
│   │   │   │   ├── GetAppraisals/
│   │   │   │   ├── GetAppraisalById/
│   │   │   │   └── GetAppraisalsByRequest/
│   │   │   │
│   │   │   ├── Collaterals/
│   │   │   │   ├── AddCollateral/
│   │   │   │   ├── UpdateCollateral/
│   │   │   │   ├── RemoveCollateral/
│   │   │   │   └── GetCollateralsByAppraisal/
│   │   │   │
│   │   │   ├── CollateralGroups/
│   │   │   │   ├── CreateGroup/
│   │   │   │   ├── UpdateGroup/
│   │   │   │   ├── AddCollateralToGroup/
│   │   │   │   ├── RemoveCollateralFromGroup/
│   │   │   │   └── GetGroupsByAppraisal/
│   │   │   │
│   │   │   ├── PropertyDetails/
│   │   │   │   ├── SaveLandDetail/
│   │   │   │   ├── SaveBuildingDetail/
│   │   │   │   ├── SaveCondoDetail/
│   │   │   │   ├── SaveVehicleDetail/
│   │   │   │   ├── SaveVesselDetail/
│   │   │   │   ├── SaveMachineryDetail/
│   │   │   │   └── GetPropertyDetail/
│   │   │   │
│   │   │   ├── Assignments/
│   │   │   │   ├── CreateAssignment/
│   │   │   │   ├── UpdateAssignment/
│   │   │   │   ├── ReassignAppraisal/
│   │   │   │   ├── UpdateProgress/
│   │   │   │   └── GetAssignmentHistory/
│   │   │   │
│   │   │   ├── Valuations/
│   │   │   │   ├── SaveValuationAnalysis/
│   │   │   │   ├── SaveGroupValuation/
│   │   │   │   └── GetValuationByAppraisal/
│   │   │   │
│   │   │   ├── Reviews/
│   │   │   │   ├── CreateReview/
│   │   │   │   ├── ApproveReview/
│   │   │   │   ├── ReturnReview/
│   │   │   │   ├── RecordVote/
│   │   │   │   └── GetReviewHistory/
│   │   │   │
│   │   │   ├── Fees/
│   │   │   │   ├── AddFee/
│   │   │   │   ├── UpdateFee/
│   │   │   │   ├── RecordPayment/
│   │   │   │   └── GetFeesByAppraisal/
│   │   │   │
│   │   │   ├── Gallery/
│   │   │   │   ├── UploadPhoto/
│   │   │   │   ├── DeletePhoto/
│   │   │   │   ├── MapPhotoToProperty/
│   │   │   │   ├── MarkPhotoForReport/
│   │   │   │   └── GetGalleryByAppraisal/
│   │   │   │
│   │   │   ├── MarketComparables/
│   │   │   │   ├── CreateComparable/
│   │   │   │   ├── UpdateComparable/
│   │   │   │   ├── SearchComparables/
│   │   │   │   └── GetComparableById/
│   │   │   │
│   │   │   ├── AppraisalComparables/
│   │   │   │   ├── LinkComparable/
│   │   │   │   ├── UnlinkComparable/
│   │   │   │   ├── SaveAdjustments/
│   │   │   │   └── GetLinkedComparables/
│   │   │   │
│   │   │   ├── Quotations/
│   │   │   │   ├── CreateQuotationRequest/
│   │   │   │   ├── InviteCompany/
│   │   │   │   ├── SubmitQuotation/
│   │   │   │   ├── NegotiateQuotation/
│   │   │   │   ├── AcceptQuotation/
│   │   │   │   └── GetQuotationRequests/
│   │   │   │
│   │   │   ├── Appointments/
│   │   │   │   ├── ScheduleAppointment/
│   │   │   │   ├── RescheduleAppointment/
│   │   │   │   ├── CancelAppointment/
│   │   │   │   └── GetAppointments/
│   │   │   │
│   │   │   ├── Pricing/
│   │   │   │   ├── CreatePricingAnalysis/
│   │   │   │   ├── AddPricingApproach/
│   │   │   │   ├── AddPricingMethod/
│   │   │   │   ├── SaveCalculation/
│   │   │   │   ├── SetFinalValue/
│   │   │   │   └── GetPricingAnalysis/
│   │   │   │
│   │   │   ├── Committees/
│   │   │   │   ├── CreateCommittee/
│   │   │   │   ├── UpdateCommittee/
│   │   │   │   ├── AddMember/
│   │   │   │   ├── RemoveMember/
│   │   │   │   └── GetCommittees/
│   │   │   │
│   │   │   └── Settings/
│   │   │       ├── UpdateSettings/
│   │   │       ├── CreateAutoAssignmentRule/
│   │   │       └── GetSettings/
│   │   │
│   │   ├── EventHandlers/
│   │   │   ├── AppraisalCreatedEventHandler.cs
│   │   │   ├── AppraisalAssignedEventHandler.cs
│   │   │   ├── AppraisalCompletedEventHandler.cs
│   │   │   └── ReviewCompletedEventHandler.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── IAppraisalNumberGenerator.cs
│   │   │   ├── AppraisalNumberGenerator.cs
│   │   │   ├── IAutoAssignmentService.cs
│   │   │   ├── AutoAssignmentService.cs
│   │   │   ├── ISlaService.cs
│   │   │   └── SlaService.cs
│   │   │
│   │   ├── ReadModels/                     # Query DTOs for Dapper
│   │   │   ├── AppraisalRow.cs
│   │   │   ├── CollateralRow.cs
│   │   │   ├── PropertyDetailRow.cs
│   │   │   └── ...
│   │   │
│   │   └── Configurations/
│   │       └── MappingConfiguration.cs
│   │
│   ├── Infrastructure/                     # Infrastructure Layer
│   │   ├── AppraisalDbContext.cs
│   │   ├── AppraisalUnitOfWork.cs
│   │   ├── Configurations/                 # EF Core Configurations
│   │   │   ├── AppraisalConfiguration.cs
│   │   │   ├── AppraisalCollateralConfiguration.cs
│   │   │   ├── CollateralGroupConfiguration.cs
│   │   │   ├── AppraisalAssignmentConfiguration.cs
│   │   │   ├── LandAppraisalDetailConfiguration.cs
│   │   │   ├── BuildingAppraisalDetailConfiguration.cs
│   │   │   ├── ... (all entity configurations)
│   │   │   └── MarketComparableConfiguration.cs
│   │   ├── Repositories/
│   │   │   ├── AppraisalRepository.cs
│   │   │   ├── AppraisalCollateralRepository.cs
│   │   │   ├── CollateralGroupRepository.cs
│   │   │   ├── AppraisalAssignmentRepository.cs
│   │   │   ├── PropertyDetailRepository.cs
│   │   │   ├── ValuationRepository.cs
│   │   │   ├── AppraisalReviewRepository.cs
│   │   │   ├── CommitteeRepository.cs
│   │   │   ├── AppraisalFeeRepository.cs
│   │   │   ├── AppraisalGalleryRepository.cs
│   │   │   ├── MarketComparableRepository.cs
│   │   │   ├── QuotationRepository.cs
│   │   │   ├── AppointmentRepository.cs
│   │   │   └── PricingAnalysisRepository.cs
│   │   ├── Migrations/
│   │   └── Seed/
│   │       └── AppraisalDataSeed.cs
│   │
│   ├── IAppraisalUnitOfWork.cs
│   ├── AppraisalModule.cs
│   └── GlobalUsing.cs
│
├── Appraisal.Contracts/                    # Public Contracts/DTOs
│   ├── Appraisals/Dtos/
│   │   ├── AppraisalDto.cs
│   │   ├── AppraisalSummaryDto.cs
│   │   └── CreateAppraisalDto.cs
│   ├── Collaterals/Dtos/
│   ├── PropertyDetails/Dtos/
│   ├── Valuations/Dtos/
│   ├── Reviews/Dtos/
│   ├── Fees/Dtos/
│   ├── Gallery/Dtos/
│   ├── MarketComparables/Dtos/
│   ├── Quotations/Dtos/
│   └── GlobalUsing.cs
│
└── Tests/
    └── Unit/
        └── Appraisal.Tests/
            ├── Domain/
            │   ├── AppraisalTests.cs
            │   ├── AppraisalCollateralTests.cs
            │   ├── CollateralGroupTests.cs
            │   └── ...
            ├── Application/
            │   ├── CreateAppraisalCommandHandlerTests.cs
            │   ├── AssignAppraisalCommandHandlerTests.cs
            │   └── ...
            ├── Fixtures/
            │   └── AppraisalTestFixture.cs
            └── TestData/
                └── AppraisalTestData.cs
```

### 1.2 Files to Create

```
[ ] Modules/Appraisal/Appraisal/Appraisal.csproj
[ ] Modules/Appraisal/Appraisal/GlobalUsing.cs
[ ] Modules/Appraisal/Appraisal/AppraisalModule.cs
[ ] Modules/Appraisal/Appraisal/IAppraisalUnitOfWork.cs
[ ] Modules/Appraisal/Appraisal.Contracts/Appraisal.Contracts.csproj
[ ] Modules/Appraisal/Appraisal.Contracts/GlobalUsing.cs
[ ] Tests/Unit/Appraisal.Tests/Appraisal.Tests.csproj
```

---

## Phase 2: Domain Layer - Core Aggregates

### 2.1 Appraisal Aggregate Root (Priority: HIGH)

The main aggregate handling appraisal lifecycle.

```csharp
// Domain/Appraisals/Appraisal.cs
public class Appraisal : Aggregate<Guid>
{
    // Private collections
    private readonly List<AppraisalCollateral> _collaterals = [];
    private readonly List<AppraisalAssignment> _assignments = [];
    private readonly List<AppraisalReview> _reviews = [];
    private readonly List<AppraisalFee> _fees = [];
    private readonly List<AppraisalGallery> _photos = [];

    // Read-only accessors
    public IReadOnlyList<AppraisalCollateral> Collaterals => _collaterals.AsReadOnly();
    public IReadOnlyList<AppraisalAssignment> Assignments => _assignments.AsReadOnly();
    public IReadOnlyList<AppraisalReview> Reviews => _reviews.AsReadOnly();
    public IReadOnlyList<AppraisalFee> Fees => _fees.AsReadOnly();
    public IReadOnlyList<AppraisalGallery> Photos => _photos.AsReadOnly();

    // Properties
    public string AppraisalNumber { get; private set; }
    public Guid RequestId { get; private set; }              // Cross-module reference
    public AppraisalStatus Status { get; private set; }
    public string AppraisalType { get; private set; }        // Initial, Revaluation, Special
    public string Priority { get; private set; }             // Normal, High

    // SLA
    public int? SLADays { get; private set; }
    public DateTime? SLADueDate { get; private set; }
    public string? SLAStatus { get; private set; }           // OnTrack, AtRisk, Breached

    // Valuation (1:1)
    public ValuationAnalysis? Valuation { get; private set; }

    // Pricing (1:1)
    public PricingAnalysis? Pricing { get; private set; }

    // Audit (from base/interceptor)
    public DateTime CreatedOn { get; private set; }
    public Guid CreatedBy { get; private set; }

    // Factory method
    public static Appraisal Create(Guid requestId, string appraisalType, string priority, int? slaDays)
    {
        var appraisal = new Appraisal
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            AppraisalType = appraisalType,
            Priority = priority,
            Status = AppraisalStatus.Pending,
            SLADays = slaDays,
            CreatedOn = DateTime.UtcNow
        };

        if (slaDays.HasValue)
            appraisal.SLADueDate = DateTime.UtcNow.AddDays(slaDays.Value);

        appraisal.AddDomainEvent(new AppraisalCreatedEvent(appraisal));
        return appraisal;
    }

    // Business methods
    public void SetAppraisalNumber(string number) { ... }

    public AppraisalCollateral AddCollateral(string collateralType, string description)
    {
        var collateral = AppraisalCollateral.Create(Id, _collaterals.Count + 1, collateralType, description);
        _collaterals.Add(collateral);
        return collateral;
    }

    public void Assign(AppraisalAssignment assignment)
    {
        ValidateCanAssign();
        _assignments.Add(assignment);
        Status = AppraisalStatus.Assigned;
        AddDomainEvent(new AppraisalAssignedEvent(this, assignment));
    }

    public void StartWork()
    {
        ValidateStatus(AppraisalStatus.Assigned);
        Status = AppraisalStatus.InProgress;
        UpdateSlaStatus();
    }

    public void SubmitForReview(AppraisalReview review)
    {
        ValidateStatus(AppraisalStatus.InProgress);
        _reviews.Add(review);
        Status = AppraisalStatus.UnderReview;
    }

    public void Complete()
    {
        ValidateStatus(AppraisalStatus.UnderReview);
        Status = AppraisalStatus.Completed;
        AddDomainEvent(new AppraisalCompletedEvent(this));
    }

    public void UpdateSlaStatus()
    {
        if (!SLADueDate.HasValue) return;

        var daysRemaining = (SLADueDate.Value - DateTime.UtcNow).Days;
        SLAStatus = daysRemaining switch
        {
            < 0 => "Breached",
            < 2 => "AtRisk",
            _ => "OnTrack"
        };
    }
}
```

### 2.2 Files to Create - Phase 2

```
[ ] Domain/Appraisals/Appraisal.cs
[ ] Domain/Appraisals/AppraisalStatus.cs (Value Object)
[ ] Domain/Appraisals/Events/AppraisalCreatedEvent.cs
[ ] Domain/Appraisals/Events/AppraisalAssignedEvent.cs
[ ] Domain/Appraisals/Events/AppraisalCompletedEvent.cs
[ ] Domain/Appraisals/Events/AppraisalStatusChangedEvent.cs
[ ] Domain/Appraisals/Exceptions/AppraisalNotFoundException.cs
[ ] Domain/Appraisals/Exceptions/InvalidAppraisalStateException.cs
[ ] Domain/Appraisals/IAppraisalRepository.cs

[ ] Domain/Collaterals/AppraisalCollateral.cs
[ ] Domain/Collaterals/CollateralType.cs (Value Object)
[ ] Domain/Collaterals/IAppraisalCollateralRepository.cs

[ ] Domain/CollateralGroups/CollateralGroup.cs
[ ] Domain/CollateralGroups/CollateralGroupItem.cs
[ ] Domain/CollateralGroups/ICollateralGroupRepository.cs

[ ] Domain/Assignments/AppraisalAssignment.cs
[ ] Domain/Assignments/AssignmentStatus.cs (Value Object)
[ ] Domain/Assignments/AssignmentMode.cs (Value Object)
[ ] Domain/Assignments/Events/AssignmentCreatedEvent.cs
[ ] Domain/Assignments/IAppraisalAssignmentRepository.cs
```

---

## Phase 3: Domain Layer - Property Details

### 3.1 Property Detail Entities (7 types)

Each property type has its own entity with specific fields from the data model spec.

```csharp
// Domain/PropertyDetails/LandAppraisalDetail.cs
public class LandAppraisalDetail : Entity<Guid>
{
    public Guid AppraisalCollateralId { get; private set; }  // FK, 1:1

    // Property Identification
    public string? PropertyName { get; private set; }
    public string? LandDesc { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Administrative Address
    public string? SubDistrict { get; private set; }
    public string? District { get; private set; }
    public string? Province { get; private set; }
    public string? LandOffice { get; private set; }

    // Ownership
    public string Owner { get; private set; }
    public string? OwnerType { get; private set; }
    // ... 78 fields total from spec

    // Factory method
    public static LandAppraisalDetail Create(Guid collateralId, LandAppraisalData data) { ... }

    // Update method
    public void Update(LandAppraisalData data) { ... }
}
```

### 3.2 Files to Create - Phase 3

```
[ ] Domain/PropertyDetails/LandAppraisalDetail.cs
[ ] Domain/PropertyDetails/BuildingAppraisalDetail.cs
[ ] Domain/PropertyDetails/LandAndBuildingAppraisalDetail.cs
[ ] Domain/PropertyDetails/CondoAppraisalDetail.cs
[ ] Domain/PropertyDetails/VehicleAppraisalDetail.cs
[ ] Domain/PropertyDetails/VesselAppraisalDetail.cs
[ ] Domain/PropertyDetails/MachineryAppraisalDetail.cs
[ ] Domain/PropertyDetails/IPropertyDetailRepository.cs
```

---

## Phase 4: Domain Layer - Supporting Entities

### 4.1 Valuation & Market Comparables

```csharp
// Domain/Valuations/ValuationAnalysis.cs
public class ValuationAnalysis : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string ValuationApproach { get; private set; }  // Market, Cost, Income
    public decimal MarketValue { get; private set; }
    public decimal AppraisedValue { get; private set; }
    public decimal? ForcedSaleValue { get; private set; }
    public DateTime ValuationDate { get; private set; }

    private readonly List<GroupValuation> _groupValuations = [];
    public IReadOnlyList<GroupValuation> GroupValuations => _groupValuations.AsReadOnly();
}

// Domain/MarketComparables/MarketComparable.cs (Aggregate Root)
public class MarketComparable : Aggregate<Guid>
{
    public string ComparableNumber { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string PropertyType { get; private set; }
    public string Province { get; private set; }
    public string? Address { get; private set; }
    public DateTime SurveyDate { get; private set; }
    public DateTime? TransactionDate { get; private set; }
    public decimal? TransactionPrice { get; private set; }
    public decimal? PricePerUnit { get; private set; }
    public string Status { get; private set; }  // Active, Expired, Flagged

    private readonly List<MarketComparableData> _data = [];
    private readonly List<MarketComparableImage> _images = [];
}
```

### 4.2 Files to Create - Phase 4

```
[ ] Domain/Valuations/ValuationAnalysis.cs
[ ] Domain/Valuations/GroupValuation.cs
[ ] Domain/Valuations/IValuationRepository.cs

[ ] Domain/MarketComparables/MarketComparable.cs
[ ] Domain/MarketComparables/MarketComparableTemplate.cs
[ ] Domain/MarketComparables/MarketComparableFactor.cs
[ ] Domain/MarketComparables/MarketComparableTemplateFactor.cs
[ ] Domain/MarketComparables/MarketComparableData.cs
[ ] Domain/MarketComparables/MarketComparableImage.cs
[ ] Domain/MarketComparables/IMarketComparableRepository.cs

[ ] Domain/AppraisalComparables/AppraisalComparable.cs
[ ] Domain/AppraisalComparables/ComparableAdjustment.cs
[ ] Domain/AppraisalComparables/IAppraisalComparableRepository.cs

[ ] Domain/Reviews/AppraisalReview.cs
[ ] Domain/Reviews/ReviewStatus.cs
[ ] Domain/Reviews/Events/ReviewCompletedEvent.cs
[ ] Domain/Reviews/IAppraisalReviewRepository.cs

[ ] Domain/Committees/Committee.cs
[ ] Domain/Committees/CommitteeMember.cs
[ ] Domain/Committees/CommitteeApprovalCondition.cs
[ ] Domain/Committees/CommitteeVote.cs
[ ] Domain/Committees/ICommitteeRepository.cs

[ ] Domain/Fees/AppraisalFee.cs
[ ] Domain/Fees/AppraisalFeeItem.cs
[ ] Domain/Fees/AppraisalFeePaymentHistory.cs
[ ] Domain/Fees/IAppraisalFeeRepository.cs

[ ] Domain/Gallery/AppraisalGallery.cs
[ ] Domain/Gallery/PropertyPhotoMapping.cs
[ ] Domain/Gallery/IAppraisalGalleryRepository.cs

[ ] Domain/Quotations/QuotationRequest.cs
[ ] Domain/Quotations/QuotationRequestItem.cs
[ ] Domain/Quotations/QuotationInvitation.cs
[ ] Domain/Quotations/CompanyQuotation.cs
[ ] Domain/Quotations/CompanyQuotationItem.cs
[ ] Domain/Quotations/QuotationNegotiation.cs
[ ] Domain/Quotations/IQuotationRepository.cs

[ ] Domain/Appointments/Appointment.cs
[ ] Domain/Appointments/AppointmentHistory.cs
[ ] Domain/Appointments/IAppointmentRepository.cs

[ ] Domain/Pricing/PricingAnalysis.cs
[ ] Domain/Pricing/PricingAnalysisApproach.cs
[ ] Domain/Pricing/PricingAnalysisMethod.cs
[ ] Domain/Pricing/PricingComparableLink.cs
[ ] Domain/Pricing/PricingCalculation.cs
[ ] Domain/Pricing/PricingFinalValue.cs
[ ] Domain/Pricing/IPricingAnalysisRepository.cs

[ ] Domain/Supporting/LandTitle.cs
[ ] Domain/Supporting/BuildingDepreciationDetail.cs
[ ] Domain/Supporting/BuildingAppraisalSurface.cs
[ ] Domain/Supporting/CondoAppraisalAreaDetail.cs
[ ] Domain/Supporting/LawAndRegulation.cs
[ ] Domain/Supporting/LawAndRegulationImage.cs
[ ] Domain/Supporting/AutoAssignmentRule.cs

[ ] Domain/Settings/AppraisalSettings.cs
[ ] Domain/Settings/IAppraisalSettingsRepository.cs
```

---

## Phase 5: Infrastructure Layer

### 5.1 DbContext

```csharp
// Infrastructure/AppraisalDbContext.cs
public class AppraisalDbContext(DbContextOptions<AppraisalDbContext> options) : DbContext(options)
{
    // Core
    public DbSet<Appraisal> Appraisals => Set<Appraisal>();
    public DbSet<AppraisalCollateral> AppraisalCollaterals => Set<AppraisalCollateral>();
    public DbSet<CollateralGroup> CollateralGroups => Set<CollateralGroup>();
    public DbSet<CollateralGroupItem> CollateralGroupItems => Set<CollateralGroupItem>();
    public DbSet<AppraisalAssignment> AppraisalAssignments => Set<AppraisalAssignment>();

    // Property Details
    public DbSet<LandAppraisalDetail> LandAppraisalDetails => Set<LandAppraisalDetail>();
    public DbSet<BuildingAppraisalDetail> BuildingAppraisalDetails => Set<BuildingAppraisalDetail>();
    public DbSet<LandAndBuildingAppraisalDetail> LandAndBuildingAppraisalDetails => Set<LandAndBuildingAppraisalDetail>();
    public DbSet<CondoAppraisalDetail> CondoAppraisalDetails => Set<CondoAppraisalDetail>();
    public DbSet<VehicleAppraisalDetail> VehicleAppraisalDetails => Set<VehicleAppraisalDetail>();
    public DbSet<VesselAppraisalDetail> VesselAppraisalDetails => Set<VesselAppraisalDetail>();
    public DbSet<MachineryAppraisalDetail> MachineryAppraisalDetails => Set<MachineryAppraisalDetail>();

    // Valuation
    public DbSet<ValuationAnalysis> ValuationAnalyses => Set<ValuationAnalysis>();
    public DbSet<GroupValuation> GroupValuations => Set<GroupValuation>();

    // Market Comparables
    public DbSet<MarketComparable> MarketComparables => Set<MarketComparable>();
    public DbSet<MarketComparableTemplate> MarketComparableTemplates => Set<MarketComparableTemplate>();
    public DbSet<MarketComparableFactor> MarketComparableFactors => Set<MarketComparableFactor>();
    public DbSet<AppraisalComparable> AppraisalComparables => Set<AppraisalComparable>();
    public DbSet<ComparableAdjustment> ComparableAdjustments => Set<ComparableAdjustment>();

    // Review & Committee
    public DbSet<AppraisalReview> AppraisalReviews => Set<AppraisalReview>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<CommitteeVote> CommitteeVotes => Set<CommitteeVote>();

    // Fees
    public DbSet<AppraisalFee> AppraisalFees => Set<AppraisalFee>();
    public DbSet<AppraisalFeeItem> AppraisalFeeItems => Set<AppraisalFeeItem>();

    // Gallery
    public DbSet<AppraisalGallery> AppraisalGalleries => Set<AppraisalGallery>();
    public DbSet<PropertyPhotoMapping> PropertyPhotoMappings => Set<PropertyPhotoMapping>();

    // Quotations
    public DbSet<QuotationRequest> QuotationRequests => Set<QuotationRequest>();
    public DbSet<QuotationInvitation> QuotationInvitations => Set<QuotationInvitation>();
    public DbSet<CompanyQuotation> CompanyQuotations => Set<CompanyQuotation>();

    // Appointments
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentHistory> AppointmentHistories => Set<AppointmentHistory>();

    // Pricing
    public DbSet<PricingAnalysis> PricingAnalyses => Set<PricingAnalysis>();

    // Supporting
    public DbSet<LandTitle> LandTitles => Set<LandTitle>();
    public DbSet<BuildingDepreciationDetail> BuildingDepreciationDetails => Set<BuildingDepreciationDetail>();
    public DbSet<LawAndRegulation> LawAndRegulations => Set<LawAndRegulation>();
    public DbSet<AutoAssignmentRule> AutoAssignmentRules => Set<AutoAssignmentRule>();
    public DbSet<AppraisalSettings> AppraisalSettings => Set<AppraisalSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("appraisal");
        modelBuilder.ApplyGlobalConventions();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // MassTransit Outbox
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        base.OnModelCreating(modelBuilder);
    }
}
```

### 5.2 Files to Create - Phase 5

```
[ ] Infrastructure/AppraisalDbContext.cs
[ ] Infrastructure/AppraisalUnitOfWork.cs
[ ] Infrastructure/Configurations/AppraisalConfiguration.cs
[ ] Infrastructure/Configurations/AppraisalCollateralConfiguration.cs
[ ] Infrastructure/Configurations/CollateralGroupConfiguration.cs
[ ] Infrastructure/Configurations/AppraisalAssignmentConfiguration.cs
[ ] Infrastructure/Configurations/LandAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/BuildingAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/LandAndBuildingAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/CondoAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/VehicleAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/VesselAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/MachineryAppraisalDetailConfiguration.cs
[ ] Infrastructure/Configurations/ValuationAnalysisConfiguration.cs
[ ] Infrastructure/Configurations/MarketComparableConfiguration.cs
[ ] Infrastructure/Configurations/AppraisalReviewConfiguration.cs
[ ] Infrastructure/Configurations/CommitteeConfiguration.cs
[ ] Infrastructure/Configurations/AppraisalFeeConfiguration.cs
[ ] Infrastructure/Configurations/AppraisalGalleryConfiguration.cs
[ ] Infrastructure/Configurations/QuotationRequestConfiguration.cs
[ ] Infrastructure/Configurations/AppointmentConfiguration.cs
[ ] Infrastructure/Configurations/PricingAnalysisConfiguration.cs
[ ] Infrastructure/Configurations/LandTitleConfiguration.cs
[ ] Infrastructure/Configurations/AutoAssignmentRuleConfiguration.cs

[ ] Infrastructure/Repositories/AppraisalRepository.cs
[ ] Infrastructure/Repositories/AppraisalCollateralRepository.cs
[ ] Infrastructure/Repositories/CollateralGroupRepository.cs
[ ] Infrastructure/Repositories/AppraisalAssignmentRepository.cs
[ ] Infrastructure/Repositories/PropertyDetailRepository.cs
[ ] Infrastructure/Repositories/ValuationRepository.cs
[ ] Infrastructure/Repositories/MarketComparableRepository.cs
[ ] Infrastructure/Repositories/AppraisalReviewRepository.cs
[ ] Infrastructure/Repositories/CommitteeRepository.cs
[ ] Infrastructure/Repositories/AppraisalFeeRepository.cs
[ ] Infrastructure/Repositories/AppraisalGalleryRepository.cs
[ ] Infrastructure/Repositories/QuotationRepository.cs
[ ] Infrastructure/Repositories/AppointmentRepository.cs
[ ] Infrastructure/Repositories/PricingAnalysisRepository.cs
```

---

## Phase 6: Application Layer - Core Features

### 6.1 Create Appraisal Feature

```csharp
// Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommand.cs
public record CreateAppraisalCommand(
    Guid RequestId,
    string AppraisalType,
    string Priority,
    int? SLADays,
    List<CreateCollateralDto>? Collaterals
) : ICommand<CreateAppraisalResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record CreateAppraisalResult(Guid Id, string AppraisalNumber);

// Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommandHandler.cs
public class CreateAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalNumberGenerator numberGenerator,
    ILogger<CreateAppraisalCommandHandler> logger
) : ICommandHandler<CreateAppraisalCommand, CreateAppraisalResult>
{
    public async Task<CreateAppraisalResult> Handle(
        CreateAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating appraisal for request {RequestId}", command.RequestId);

        // Create aggregate
        var appraisal = Appraisal.Create(
            command.RequestId,
            command.AppraisalType,
            command.Priority,
            command.SLADays
        );

        // Add collaterals
        if (command.Collaterals is { Count: > 0 })
        {
            foreach (var c in command.Collaterals)
            {
                appraisal.AddCollateral(c.CollateralType, c.Description);
            }
        }

        // Generate number
        var number = await numberGenerator.GenerateAsync(cancellationToken);
        appraisal.SetAppraisalNumber(number);

        // Persist
        await appraisalRepository.AddAsync(appraisal, cancellationToken);

        return new CreateAppraisalResult(appraisal.Id, number);
    }
}

// Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommandValidator.cs
public class CreateAppraisalCommandValidator : AbstractValidator<CreateAppraisalCommand>
{
    public CreateAppraisalCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.AppraisalType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Priority).NotEmpty().Must(p => p is "Normal" or "High");
    }
}

// Application/Features/Appraisals/CreateAppraisal/CreateAppraisalEndpoint.cs
public class CreateAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals",
            async (CreateAppraisalRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = request.Adapt<CreateAppraisalCommand>();
                var result = await sender.Send(command, ct);
                return Results.Created($"/appraisals/{result.Id}", result);
            })
            .WithName("CreateAppraisal")
            .WithTags("Appraisals")
            .Produces<CreateAppraisalResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
```

### 6.2 Files to Create - Phase 6 (Priority Features)

```
# Create Appraisal
[ ] Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommand.cs
[ ] Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommandHandler.cs
[ ] Application/Features/Appraisals/CreateAppraisal/CreateAppraisalCommandValidator.cs
[ ] Application/Features/Appraisals/CreateAppraisal/CreateAppraisalEndpoint.cs

# Get Appraisals
[ ] Application/Features/Appraisals/GetAppraisals/GetAppraisalsQuery.cs
[ ] Application/Features/Appraisals/GetAppraisals/GetAppraisalsQueryHandler.cs
[ ] Application/Features/Appraisals/GetAppraisals/GetAppraisalsEndpoint.cs

# Get Appraisal By Id
[ ] Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdQuery.cs
[ ] Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdQueryHandler.cs
[ ] Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdEndpoint.cs

# Assign Appraisal
[ ] Application/Features/Appraisals/AssignAppraisal/AssignAppraisalCommand.cs
[ ] Application/Features/Appraisals/AssignAppraisal/AssignAppraisalCommandHandler.cs
[ ] Application/Features/Appraisals/AssignAppraisal/AssignAppraisalEndpoint.cs

# Update Appraisal Status
[ ] Application/Features/Appraisals/UpdateAppraisalStatus/UpdateAppraisalStatusCommand.cs
[ ] Application/Features/Appraisals/UpdateAppraisalStatus/UpdateAppraisalStatusCommandHandler.cs

# Services
[ ] Application/Services/IAppraisalNumberGenerator.cs
[ ] Application/Services/AppraisalNumberGenerator.cs
[ ] Application/Services/IAutoAssignmentService.cs
[ ] Application/Services/AutoAssignmentService.cs
[ ] Application/Services/ISlaService.cs
[ ] Application/Services/SlaService.cs

# Event Handlers
[ ] Application/EventHandlers/AppraisalCreatedEventHandler.cs
[ ] Application/EventHandlers/AppraisalAssignedEventHandler.cs

# Configurations
[ ] Application/Configurations/MappingConfiguration.cs

# Read Models
[ ] Application/ReadModels/AppraisalRow.cs
[ ] Application/ReadModels/CollateralRow.cs
```

---

## Phase 7: Module Registration

### 7.1 AppraisalModule.cs

```csharp
public static class AppraisalModule
{
    public static IServiceCollection AddAppraisalModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configure Mapster
        MappingConfiguration.ConfigureMappings();

        // 2. Register Repositories
        services.AddScoped<IAppraisalRepository, AppraisalRepository>();
        services.AddScoped<IAppraisalCollateralRepository, AppraisalCollateralRepository>();
        services.AddScoped<ICollateralGroupRepository, CollateralGroupRepository>();
        services.AddScoped<IAppraisalAssignmentRepository, AppraisalAssignmentRepository>();
        services.AddScoped<IPropertyDetailRepository, PropertyDetailRepository>();
        services.AddScoped<IValuationRepository, ValuationRepository>();
        services.AddScoped<IMarketComparableRepository, MarketComparableRepository>();
        services.AddScoped<IAppraisalReviewRepository, AppraisalReviewRepository>();
        services.AddScoped<ICommitteeRepository, CommitteeRepository>();
        services.AddScoped<IAppraisalFeeRepository, AppraisalFeeRepository>();
        services.AddScoped<IAppraisalGalleryRepository, AppraisalGalleryRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPricingAnalysisRepository, PricingAnalysisRepository>();

        // 3. Register Services
        services.AddScoped<IAppraisalNumberGenerator, AppraisalNumberGenerator>();
        services.AddScoped<IAutoAssignmentService, AutoAssignmentService>();
        services.AddScoped<ISlaService, SlaService>();

        // 4. Register Interceptors
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        // 5. Configure DbContext
        services.AddDbContext<AppraisalDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppraisalDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "appraisal");
            });
        });

        // 6. Register Unit of Work
        services.AddScoped<IAppraisalUnitOfWork>(sp =>
            new AppraisalUnitOfWork(sp.GetRequiredService<AppraisalDbContext>(), sp));

        // 7. Register Data Seeder (optional)
        services.AddScoped<IDataSeeder<AppraisalDbContext>, AppraisalDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseAppraisalModule(this IApplicationBuilder app)
    {
        app.UseMigration<AppraisalDbContext>();
        return app;
    }
}
```

---

## Phase 8: Testing

### 8.1 Unit Test Structure

```csharp
// Tests/Unit/Appraisal.Tests/Domain/AppraisalTests.cs
public class AppraisalTests
{
    [Fact]
    public void Create_ShouldInitializeWithPendingStatus()
    {
        // Arrange
        var requestId = Guid.NewGuid();

        // Act
        var appraisal = Appraisal.Create(requestId, "Initial", "Normal", 5);

        // Assert
        appraisal.Status.Should().Be(AppraisalStatus.Pending);
        appraisal.RequestId.Should().Be(requestId);
        appraisal.AppraisalType.Should().Be("Initial");
        appraisal.SLADays.Should().Be(5);
        appraisal.SLADueDate.Should().NotBeNull();
        appraisal.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AppraisalCreatedEvent>();
    }

    [Fact]
    public void AddCollateral_ShouldAddToCollection()
    {
        // Arrange
        var appraisal = Appraisal.Create(Guid.NewGuid(), "Initial", "Normal", null);

        // Act
        var collateral = appraisal.AddCollateral("Land", "Test land");

        // Assert
        appraisal.Collaterals.Should().ContainSingle();
        collateral.CollateralType.Should().Be("Land");
        collateral.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void Assign_ShouldChangeStatusToAssigned()
    {
        // Arrange
        var appraisal = Appraisal.Create(Guid.NewGuid(), "Initial", "Normal", null);
        var assignment = AppraisalAssignment.Create(appraisal.Id, "Internal", Guid.NewGuid());

        // Act
        appraisal.Assign(assignment);

        // Assert
        appraisal.Status.Should().Be(AppraisalStatus.Assigned);
        appraisal.Assignments.Should().ContainSingle();
    }

    [Fact]
    public void StartWork_WhenNotAssigned_ShouldThrow()
    {
        // Arrange
        var appraisal = Appraisal.Create(Guid.NewGuid(), "Initial", "Normal", null);

        // Act & Assert
        appraisal.Invoking(a => a.StartWork())
            .Should().Throw<InvalidAppraisalStateException>();
    }
}

// Tests/Unit/Appraisal.Tests/Application/CreateAppraisalCommandHandlerTests.cs
public class CreateAppraisalCommandHandlerTests
{
    private readonly Mock<IAppraisalRepository> _repositoryMock;
    private readonly Mock<IAppraisalNumberGenerator> _numberGeneratorMock;
    private readonly CreateAppraisalCommandHandler _handler;

    public CreateAppraisalCommandHandlerTests()
    {
        _repositoryMock = new Mock<IAppraisalRepository>();
        _numberGeneratorMock = new Mock<IAppraisalNumberGenerator>();
        _numberGeneratorMock.Setup(x => x.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("APR-2024-0001");

        _handler = new CreateAppraisalCommandHandler(
            _repositoryMock.Object,
            _numberGeneratorMock.Object,
            Mock.Of<ILogger<CreateAppraisalCommandHandler>>()
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateAppraisalAndReturnResult()
    {
        // Arrange
        var command = new CreateAppraisalCommand(
            RequestId: Guid.NewGuid(),
            AppraisalType: "Initial",
            Priority: "Normal",
            SLADays: 5,
            Collaterals: null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AppraisalNumber.Should().Be("APR-2024-0001");
        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<Appraisal>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### 8.2 Files to Create - Phase 8

```
[ ] Tests/Unit/Appraisal.Tests/Appraisal.Tests.csproj
[ ] Tests/Unit/Appraisal.Tests/Domain/AppraisalTests.cs
[ ] Tests/Unit/Appraisal.Tests/Domain/AppraisalCollateralTests.cs
[ ] Tests/Unit/Appraisal.Tests/Domain/CollateralGroupTests.cs
[ ] Tests/Unit/Appraisal.Tests/Domain/AppraisalAssignmentTests.cs
[ ] Tests/Unit/Appraisal.Tests/Application/CreateAppraisalCommandHandlerTests.cs
[ ] Tests/Unit/Appraisal.Tests/Application/AssignAppraisalCommandHandlerTests.cs
[ ] Tests/Unit/Appraisal.Tests/Application/GetAppraisalByIdQueryHandlerTests.cs
[ ] Tests/Unit/Appraisal.Tests/Fixtures/AppraisalTestFixture.cs
[ ] Tests/Unit/Appraisal.Tests/TestData/AppraisalTestData.cs
```

---

## Implementation Order (Priority)

### Sprint 1: Foundation (Week 1-2)
1. [ ] Project structure & csproj files
2. [ ] GlobalUsing.cs files
3. [ ] Core domain entities (Appraisal, AppraisalCollateral, CollateralGroup)
4. [ ] Value objects (AppraisalStatus, CollateralType, etc.)
5. [ ] Repository interfaces
6. [ ] DbContext with core configurations
7. [ ] AppraisalModule registration

### Sprint 2: Core CRUD (Week 2-3)
1. [ ] Create/Get/Update Appraisal features
2. [ ] Collateral management features
3. [ ] Collateral group features
4. [ ] Core unit tests

### Sprint 3: Assignment & Property Details (Week 3-4)
1. [ ] Assignment features
2. [ ] Auto-assignment service
3. [ ] Property detail entities (all 7 types)
4. [ ] Property detail configurations
5. [ ] Property detail features

### Sprint 4: Valuation & Comparables (Week 4-5)
1. [ ] Valuation entities
2. [ ] Market comparable aggregate
3. [ ] Appraisal comparable linking
4. [ ] Valuation features

### Sprint 5: Review & Committee (Week 5-6)
1. [ ] Review entities
2. [ ] Committee aggregate
3. [ ] Voting logic
4. [ ] Review features

### Sprint 6: Supporting Features (Week 6-7)
1. [ ] Fee management
2. [ ] Photo gallery
3. [ ] Appointments
4. [ ] Quotations

### Sprint 7: Pricing & Polish (Week 7-8)
1. [ ] Pricing analysis
2. [ ] Integration tests
3. [ ] Database views for queries
4. [ ] Final testing & documentation

---

## Database Views (for Query Side)

```sql
-- Create views for efficient querying with Dapper
CREATE VIEW [appraisal].[vw_Appraisals] AS
SELECT
    a.Id,
    a.AppraisalNumber,
    a.RequestId,
    a.Status,
    a.AppraisalType,
    a.Priority,
    a.SLADays,
    a.SLADueDate,
    a.SLAStatus,
    a.CreatedOn,
    a.CreatedBy,
    a.UpdatedOn,
    a.UpdatedBy,
    (SELECT COUNT(*) FROM appraisal.AppraisalCollaterals c WHERE c.AppraisalId = a.Id) AS CollateralCount,
    (SELECT COUNT(*) FROM appraisal.AppraisalAssignments aa WHERE aa.AppraisalId = a.Id) AS AssignmentCount
FROM appraisal.Appraisals a
WHERE a.IsDeleted = 0;

CREATE VIEW [appraisal].[vw_AppraisalCollaterals] AS
SELECT
    c.Id,
    c.AppraisalId,
    c.SequenceNumber,
    c.CollateralType,
    c.Description,
    c.CreatedOn
FROM appraisal.AppraisalCollaterals c;
```

---

## Notes

- Follow Request module patterns exactly
- Use Dapper for all queries (read side)
- Use EF Core for all commands (write side)
- Domain events for cross-module communication
- Soft delete for all entities
- Audit fields auto-populated via interceptor
- Schema isolation: `appraisal`

---

## Review: Image Document Reference Implementation (2026-01-28)

### Summary of Changes

The `MarketComparableImage` entity was refactored to use `DocumentId` references instead of storing file details directly. This change separates concerns by leveraging the existing Document module for file uploads.

### Files Modified

| File | Change |
|------|--------|
| `Domain/MarketComparables/MarketComparableImage.cs` | Replaced `FileName`, `FilePath` with `DocumentId`; updated `Create()` factory method |
| `Domain/MarketComparables/MarketComparable.cs` | Updated `AddImage()` method signature to accept `Guid documentId` |
| `Infrastructure/Configurations/MarketComparableImageConfiguration.cs` | Removed `FileName`/`FilePath` config; added `DocumentId` config with index |
| `Application/.../AddMarketComparableImageRequest.cs` | Changed to accept `DocumentId` instead of `FileName`/`FilePath` |
| `Application/.../AddMarketComparableImageCommand.cs` | Changed to accept `DocumentId` |
| `Application/.../AddMarketComparableImageCommandHandler.cs` | Updated to pass `DocumentId` to `AddImage()` |
| `Application/.../AddMarketComparableImageEndpoint.cs` | Updated command construction |
| `Application/.../GetMarketComparableByIdResult.cs` | Updated `ImageDto` to include `DocumentId` |
| `Application/.../GetMarketComparableByIdQueryHandler.cs` | Updated mapping to use `DocumentId` |
| `httpRequests/Appraisal/MarketComparableTemplateSystem.http` | Updated sample requests with new format |

### Migration Generated

Migration `20260128033735_ChangeMarketComparableImageToDocumentReference`:
- Drops `FileName` and `FilePath` columns
- Adds `DocumentId` column (uniqueidentifier, non-nullable)
- Creates index on `DocumentId`

### New Workflow

1. Upload image via Document API → Get `DocumentId`
2. Link document to market comparable via `POST /market-comparables/{id}/images`

### New API Request Format

```json
POST /market-comparables/{id}/images
{
  "documentId": "guid-from-document-upload",
  "title": "Front View",
  "description": "Optional description"
}
```

### Benefits

1. **Separation of Concerns**: File upload logic stays in Document module
2. **Consistency**: All files go through the same upload pipeline
3. **Flexibility**: Documents can be reused across different entities if needed
4. **Simpler Image Entity**: No need to track file paths; just references the document

---

## Review: TemplateId Enhancement Implementation (2026-01-28)

### Summary of Changes

Added an optional `TemplateId` field to `MarketComparable` entity to track which template was used when creating the comparable. This enables template version tracking and supports multiple templates per property type in the future.

### Files Modified

| File | Change |
|------|--------|
| `Domain/MarketComparables/MarketComparable.cs` | Added `TemplateId` property (nullable Guid), `SetTemplate()` method, and updated `Create()` factory to accept optional templateId |
| `Infrastructure/Configurations/MarketComparableConfiguration.cs` | Added `TemplateId` property config, FK to `MarketComparableTemplates` with `SetNull` delete behavior, and index |
| `Application/.../CreateMarketComparableRequest.cs` | Added `TemplateId` optional parameter |
| `Application/.../CreateMarketComparableCommand.cs` | Added `TemplateId` optional parameter |
| `Application/.../CreateMarketComparableCommandHandler.cs` | Updated to pass `TemplateId` to `Create()` factory method |
| `Application/.../GetMarketComparableByIdResult.cs` | Added `TemplateId` to `MarketComparableDetailDto` |
| `Application/.../GetMarketComparableByIdQueryHandler.cs` | Updated mapping to include `TemplateId` |
| `httpRequests/Appraisal/MarketComparableTemplateSystem.http` | Added example requests with `TemplateId` |

### Migration Generated

Migration `20260128041945_AddTemplateIdToMarketComparable`:
- Adds `TemplateId` column (uniqueidentifier, nullable)
- Creates index on `TemplateId`
- Adds FK constraint to `MarketComparableTemplates` with `SetNull` on delete

### Updated Entity Relationship

```
MarketComparableTemplate ──1:M──> MarketComparableTemplateFactor ──M:1──> MarketComparableFactor
        │                                                                       │
        │ (TemplateId - optional FK)                                            │
        ▼                                                                       ▼
MarketComparable ──1:M──> MarketComparableData ─────────────────────────────────┘
        │
        └──1:M──> MarketComparableImage
```

### New API Request Format

```json
POST /market-comparables
{
  "comparableNumber": "MC-2026-001",
  "propertyType": "Land",
  "province": "Bangkok",
  "dataSource": "Survey",
  "surveyDate": "2026-01-28T00:00:00Z",
  "templateId": "optional-template-guid"
}
```

### Key Implementation Details

1. **Optional FK**: `TemplateId` is nullable, allowing comparables to be created without a template
2. **SetNull on Delete**: If template is deleted, `TemplateId` is set to null (historical record preserved)
3. **Index**: Added index on `TemplateId` for query performance
4. **Mapster Auto-Mapping**: No explicit mapping needed - property names match between Request/Command

### Benefits

1. **Template Tracking**: Know exactly which template was used for each comparable
2. **Version History**: Even if template changes, the original template ID is preserved
3. **Future Flexibility**: Supports multiple templates per property type
4. **Optional**: Existing comparables without templates continue to work
