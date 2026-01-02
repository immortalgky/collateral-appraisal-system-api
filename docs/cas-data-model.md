```mermaid
erDiagram
%% REQUEST MANAGEMENT BC - Blue
Request {
uniqueidentifier Id PK
nvarchar RequestNumber UK
nvarchar Purpose
nvarchar Status
nvarchar Channel
nvarchar Priority
}

    RequestTitle {
        uniqueidentifier Id PK
        uniqueidentifier RequestId FK
        nvarchar CollateralType
        nvarchar TitleNumber
    }
    
    RequestApplicant {
        uniqueidentifier Id PK
        uniqueidentifier RequestId FK
        nvarchar CustomerName
        nvarchar ContactNumber
    }
    
    RequestProperty {
        uniqueidentifier Id PK
        uniqueidentifier RequestId FK
        nvarchar PropertyType
        nvarchar BuildingType
        decimal SellingPrice
    }
    
    RequestComment {
        uniqueidentifier Id PK
        uniqueidentifier RequestId FK
        nvarchar Comment
    }
    
    %% QUOTATION MANAGEMENT (part of Request Management BC)
    QuotationRequest {
        uniqueidentifier Id PK
        nvarchar QuotationNumber UK
        nvarchar Status
        nvarchar QuotationTitle
        int NumberOfRequests
        decimal TotalEstimatedValue
        decimal MaxBudget
        uniqueidentifier CreatedBy
        datetime2 PublishedDate
        datetime2 SubmissionDeadline
        uniqueidentifier WinningCompanyId FK
        uniqueidentifier WinningQuotationId FK
        datetime2 AwardedDate
        uniqueidentifier AwardedBy
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    QuotationRequestItem {
        uniqueidentifier Id PK
        uniqueidentifier QuotationRequestId FK
        uniqueidentifier RequestId FK
        int ItemSequence
        nvarchar ItemDescription
        decimal EstimatedValue
        nvarchar SpecialRequirements
        nvarchar PropertyType
        nvarchar PropertyAddress
        nvarchar UrgencyLevel
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    CompanyQuotation {
        uniqueidentifier Id PK
        nvarchar QuotationNumber UK
        uniqueidentifier QuotationRequestId FK
        uniqueidentifier CompanyId FK
        nvarchar Status
        decimal TotalFee
        nvarchar Currency
        int ProposedTimeline
        nvarchar AssignedAppraiser
        int AppraiserExperience
        datetime2 DeliveryDate
        datetime2 SubmittedDate
        bit IsWithinDeadline
        decimal EvaluationScore
        int OverallRanking
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    CompanyQuotationItem {
        uniqueidentifier Id PK
        uniqueidentifier CompanyQuotationId FK
        uniqueidentifier QuotationRequestItemId FK
        decimal AppraisalFee
        decimal InspectionFee
        decimal ReportFee
        decimal TravelCost
        decimal AdditionalFees
        decimal SubTotal
        decimal DiscountAmount
        decimal FinalAmount
        nvarchar Currency
        int ProposedTimeline
        nvarchar AssignedAppraiser
        int AppraiserExperience
        datetime2 DeliveryDate
        nvarchar Methodology
        nvarchar QualityAssurance
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalCompany {
        uniqueidentifier Id PK
        nvarchar CompanyCode UK
        nvarchar CompanyName
        nvarchar RegistrationNumber
        nvarchar ContactPerson
        nvarchar Email
        nvarchar Phone
        nvarchar Address
        nvarchar City
        nvarchar Province
        int EstablishedYear
        nvarchar SpecializationAreas
        nvarchar LicenseNumber
        date LicenseExpiryDate
        int CompletedAppraisals
        decimal AverageRating
        nvarchar Status
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    %% APPRAISAL MANAGEMENT BC - Green
    Appraisal {
        uniqueidentifier Id PK
        nvarchar AppraisalNumber UK
        uniqueidentifier RequestId FK
        nvarchar Status
        nvarchar AppraisalType
        datetime2 ScheduledDate
        datetime2 InspectionDate
        datetime2 CompletedDate
        date ValidUntil
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalAppointment {
        uniqueidentifier Id PK
        nvarchar AppointmentNumber UK
        uniqueidentifier AppraisalId FK
        nvarchar Status
        datetime2 ScheduledDate
        datetime2 StartTime
        datetime2 EndTime
        nvarchar Location
        nvarchar AppointmentType
        nvarchar Contact
        nvarchar Remarks
    }
    
    AppraisalFee {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        nvarchar FeeNumber UK
        decimal SubTotal
        decimal DiscountAmount
        decimal AmountBeforeVAT
        decimal VATRate
        decimal VATAmount
        datetime2 TaxCalculationDate
        nvarchar VATConfigKey
        decimal TotalAmount
        nvarchar Currency
        nvarchar Status
        nvarchar PaymentStatus
        decimal TotalPaidAmount
        uniqueidentifier SubmittedBy FK
        uniqueidentifier ApprovedBy FK
        datetime2 SubmittedDate
        datetime2 ApprovedDate
        datetime2 CreatedAt
    }
    
    FeeStructure {
        uniqueidentifier Id PK
        nvarchar FeeCode UK
        nvarchar FeeName
        nvarchar FeeType
        nvarchar CollateralType
        nvarchar CalculationMethod
        decimal BaseAmount
        decimal RatePercentage
        int Version
        bit IsActive
        datetime2 EffectiveFrom
        datetime2 EffectiveTo
    }
    
    AppraisalFeeItem {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        uniqueidentifier FeeStructureId FK
        nvarchar ItemDescription
        int Quantity
        decimal UnitPrice
        decimal Amount
        int FeeStructureVersion
        datetime2 AppliedDate
        nvarchar Remarks
    }
    
    FeeAdjustment {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        nvarchar AdjustmentType
        decimal AdjustmentAmount
        nvarchar Reason
        uniqueidentifier AdjustedBy FK
        uniqueidentifier ApprovedBy FK
        datetime2 AdjustmentDate
        datetime2 ApprovalDate
    }
    
    FeePayment {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        nvarchar PaymentNumber UK
        decimal PaymentAmount
        datetime2 PaymentDate
        nvarchar PaymentMethod
        nvarchar TransactionReference
        nvarchar PaymentStatus
        uniqueidentifier ProcessedBy FK
        datetime2 ProcessedDate
        nvarchar Remarks
    }
    
    Assignment {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        nvarchar AssigneeType
        uniqueidentifier AssigneeId
        uniqueidentifier AssignedBy
        datetime2 AssignedDate
        datetime2 AcceptedDate
        datetime2 CompletedDate
        nvarchar Status
        nvarchar Remarks
    }
    
    AppraisalInspection {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        uniqueidentifier CollateralId FK
        nvarchar InspectionType
        nvarchar InspectorName
        datetime2 InspectionDate
        nvarchar WeatherCondition
        nvarchar AccessibilityStatus
        nvarchar OccupancyStatus
        nvarchar GeneralCondition
        int EstimatedAge
        int EffectiveAge
        int RemainingEconomicLife
        nvarchar InspectionNotes
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    %% APPRAISAL INSPECTION DETAILS (part of Appraisal Management BC)
    AppraisalBuildingDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar FoundationType
        nvarchar FoundationCondition
        nvarchar StructuralFrameType
        nvarchar StructuralCondition
        nvarchar RoofType
        nvarchar RoofMaterial
        nvarchar RoofCondition
        nvarchar ExteriorWallMaterial
        nvarchar ExteriorWallCondition
        nvarchar FloorMaterial
        nvarchar FloorCondition
        nvarchar DecorationQuality
        nvarchar KitchenType
        nvarchar MaintenanceLevel
        decimal EstimatedRepairCost
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalLandDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar Topography
        nvarchar SoilType
        nvarchar DrainageCondition
        nvarchar FloodRisk
        nvarchar Shape
        decimal Frontage
        decimal Depth
        nvarchar RoadAccess
        decimal RoadWidth
        bit ElectricityAvailable
        bit WaterSupplyAvailable
        bit SewerageAvailable
        nvarchar CurrentUse
        nvarchar DevelopmentStatus
        nvarchar LandImprovements
        nvarchar FencingType
        nvarchar FencingCondition
        nvarchar EnvironmentalIssues
        nvarchar ContaminationRisk
        nvarchar NoiseLevel
        nvarchar NeighborhoodType
        nvarchar NeighborhoodQuality
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalCondoDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar ViewType
        nvarchar ViewQuality
        nvarchar BalconyCondition
        int Floor
        nvarchar Position
        nvarchar Orientation
        nvarchar CommonAreaCondition
        nvarchar LobbyCondition
        nvarchar ElevatorCondition
        bit SwimmingPoolAvailable
        nvarchar SwimmingPoolCondition
        bit GymAvailable
        nvarchar GymCondition
        nvarchar SecuritySystemQuality
        bit ParkingAssigned
        nvarchar ParkingLocation
        nvarchar ManagementQuality
        nvarchar CommonFeeStatus
        decimal CommonFeeAmount
        nvarchar BuildingReputation
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalMachineDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar PhysicalCondition
        nvarchar OperationalStatus
        nvarchar MaintenanceHistory
        date LastServiceDate
        int HoursOfOperation
        nvarchar PerformanceLevel
        nvarchar ObsolescenceLevel
        int RemainingUsefulLife
        nvarchar ModificationsMade
        bit SafetyCompliance
        nvarchar MarketDemand
        bit ReplacementAvailable
        int EstimatedDowntime
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalVehicleDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar BodyCondition
        nvarchar PaintCondition
        nvarchar RustLevel
        nvarchar DentsDamage
        nvarchar GlassCondition
        nvarchar UpholsteryCondition
        nvarchar DashboardCondition
        bit ElectronicsWorking
        bit AirConditioningWorking
        int OdometerReading
        nvarchar OdometerCondition
        nvarchar EngineCondition
        nvarchar TransmissionCondition
        nvarchar BrakeCondition
        nvarchar TireCondition
        decimal TireRemainingTread
        bit ServiceHistoryAvailable
        bit AccidentHistory
        nvarchar AccidentDetails
        nvarchar RegistrationStatus
        nvarchar TaxStatus
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalLeaseDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar RentCollection
        nvarchar PaymentHistory
        nvarchar TenantQuality
        nvarchar TenantStability
        nvarchar PropertyMaintenance
        nvarchar TenantCare
        bit UnauthorizedModifications
        nvarchar ModificationDetails
        decimal MarketRentComparison
        nvarchar RentReviewFrequency
        int LeaseTermRemaining
        nvarchar RenewalProbability
        nvarchar EarlyTerminationRisk
        bit ComplianceWithTerms
        nvarchar SecurityDepositStatus
        nvarchar GuaranteeStatus
        nvarchar DemandForPropertyType
        nvarchar LocationDesirability
        nvarchar LeaseabilityRisk
        nvarchar VacancyRisk
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalVesselDetail {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalInspectionId FK
        nvarchar HullCondition
        nvarchar StructuralIntegrity
        nvarchar CorrosionLevel
        bit WaterTightness
        nvarchar HullDefects
        nvarchar DeckCondition
        nvarchar EngineCondition
        int EngineHours
        nvarchar PropulsionSystemCondition
        nvarchar ElectricalSystemCondition
        decimal MaxSpeedAchieved
        decimal FuelConsumption
        nvarchar OperationalReliability
        nvarchar MaintenanceRequirements
        nvarchar NavigationEquipmentCondition
        nvarchar SafetyEquipmentCondition
        nvarchar InteriorCondition
        nvarchar AccommodationQuality
        nvarchar SafetyCertificateStatus
        nvarchar ClassCertificateStatus
        nvarchar MarketDemandForType
        nvarchar AgeObsolescenceImpact
        nvarchar OperationalStatus
        date LastDryDockDate
        bit AccidentHistory
        nvarchar OperatingCostLevel
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    ComparableProperty {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        int SequenceNumber
        nvarchar PropertyName
        nvarchar PropertyAddress
        decimal DistanceKm
        nvarchar Direction
        nvarchar PropertyType
        date TransactionDate
        decimal TransactionPrice
        nvarchar DataSource
        nvarchar OverallCondition
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    SalesGridAnalysis {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        uniqueidentifier ValuationResultId FK
        date AnalysisDate
        nvarchar PreparedBy
        nvarchar SubjectAddress
        decimal SubjectLandAreaSqm
        decimal SubjectBuildingAreaSqm
        int NumberOfComparables
        decimal IndicatedValue
        nvarchar ReconciliationNotes
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    SalesGridAdjustment {
        uniqueidentifier Id PK
        uniqueidentifier SalesGridAnalysisId FK
        uniqueidentifier ComparablePropertyId FK
        int SequenceOrder
        decimal LocationAdjustmentPct
        decimal SizeAdjustmentPct
        decimal AgeAdjustmentPct
        decimal ConditionAdjustmentPct
        decimal GrossAdjustmentPct
        decimal NetAdjustmentPct
        decimal AdjustedSalePrice
        decimal WeightFactor
        decimal WeightedValue
        nvarchar Comments
    }
    
    WqsAnalysis {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        uniqueidentifier ValuationResultId FK
        uniqueidentifier TemplateId FK
        date AnalysisDate
        decimal TotalScore
        decimal MaxPossibleScore
        decimal QualityPercentage
        nvarchar QualityGrade
        decimal BaseValue
        decimal QualityAdjustedValue
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    WqsCriteriaScore {
        uniqueidentifier Id PK
        uniqueidentifier WqsAnalysisId FK
        uniqueidentifier CriteriaId FK
        nvarchar CriteriaName
        nvarchar CriteriaCategory
        decimal Weight
        decimal MaxScore
        decimal ActualScore
        decimal WeightedScore
        nvarchar ScoreJustification
        nvarchar EvidenceDocumentIds
    }
    
    ValuationResult {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        nvarchar ValuationLevel "Individual, Group"
        uniqueidentifier CollateralId FK
        uniqueidentifier CollateralGroupId FK
        nvarchar ValuationMethod
        decimal MarketValue
        decimal ForcedSaleValue
        decimal InsuranceValue
        decimal LiquidationValue
        nvarchar Currency
        date ValuationDate
        date ValidUntil
        nvarchar ConfidenceLevel
        nvarchar ValuationBasis
        nvarchar ValuedBy
        nvarchar AppraisalStandard
        nvarchar Assumptions
        nvarchar LimitingConditions
        nvarchar SpecialAssumptions
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    ValuationAllocation {
        uniqueidentifier Id PK
        uniqueidentifier GroupValuationResultId FK
        uniqueidentifier CollateralId FK
        nvarchar AllocationBasis
        decimal AllocationPercentage
        decimal AllocatedMarketValue
        decimal AllocatedForcedSaleValue
        decimal AllocatedInsuranceValue
        decimal AllocatedLiquidationValue
        nvarchar AllocationReasoning
        nvarchar AllocationFactors
        nvarchar AllocatedBy
        date AllocationDate
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    ValueReconciliation {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        decimal MarketApproachValue
        decimal MarketApproachWeight
        nvarchar MarketApproachReliability
        decimal CostApproachValue
        decimal CostApproachWeight
        nvarchar CostApproachReliability
        decimal IncomeApproachValue
        decimal IncomeApproachWeight
        nvarchar IncomeApproachReliability
        nvarchar ReconciliationMethod
        decimal FinalMarketValue
        decimal ForcedSaleValue
        decimal ForcedSaleDiscount
        nvarchar ApproachJustification
        date ValuationDate
        int ValidityPeriod
        nvarchar CertifiedBy
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    ValuationTemplate {
        uniqueidentifier Id PK
        nvarchar TemplateName
        nvarchar TemplateType
        nvarchar PropertyType
        nvarchar Country
        nvarchar Region
        nvarchar Version
        bit IsActive
        bit IsDefault
        nvarchar Configuration
        nvarchar CreatedBy
        nvarchar ApprovedBy
        date EffectiveDate
        date ExpiryDate
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    %% COLLATERAL GROUPING (part of Appraisal Management BC)
    CollateralGroup {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        int GroupNumber
        nvarchar GroupName
        nvarchar GroupDescription
        nvarchar EvaluationMethod
        nvarchar ValuationStrategy "SumOfParts, AsWhole, Hybrid"
        nvarchar PrimaryValuationLevel "Individual, Group"
        nvarchar GroupingRationale
        bit RequireIndividualAllocation
        nvarchar AllocationMethod
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    CollateralGroupMember {
        uniqueidentifier Id PK
        uniqueidentifier CollateralGroupId FK
        uniqueidentifier CollateralId FK
        int SequenceNumber
        decimal WeightingFactor
        nvarchar Remarks
        datetime2 CreatedAt
    }
    
    %% COLLATERAL MANAGEMENT BC - Orange
    Collateral {
        uniqueidentifier Id PK
        nvarchar CollateralCode UK
        nvarchar CollateralType "Land, Building, Condo, LeaseAgreement, Machine, Vessel, Vehicle"
        nvarchar Status
        nvarchar RegistrationNumber
        nvarchar Owner
        nvarchar OwnershipType
        nvarchar EncumbranceStatus
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    CollateralLand {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        decimal TotalAreaSqm
        nvarchar TotalAreaUnit
        nvarchar LandUse
        nvarchar Shape
        decimal Width
        decimal Length
        nvarchar Province
        nvarchar District
        nvarchar SubDistrict
        nvarchar ZoningCode
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    LandTitleDetail {
        uniqueidentifier Id PK
        uniqueidentifier CollateralLandId FK
        int TitleSequence
        nvarchar TitleType
        nvarchar TitleNumber
        nvarchar LandNumber
        nvarchar SurveyNumber
        decimal LandAreaRai
        decimal LandAreaNgan
        decimal LandAreaWah
        decimal LandAreaSqm
        nvarchar PositionDescription
        nvarchar Adjacent
        nvarchar OwnerName
        nvarchar OwnershipRatio
        nvarchar TransferRestrictions
        nvarchar Encumbrances
        decimal IndividualValue
        decimal ValueRatio
        nvarchar Remarks
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    CollateralBuilding {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        uniqueidentifier OnLandId FK
        nvarchar BuildingName
        nvarchar BuildingType
        nvarchar BuildingNumber
        int NumberOfFloors
        int NumberOfUnits
        int YearBuilt
        decimal BuildingAreaSqm
        decimal UsableAreaSqm
        nvarchar BuildingMaterial
        nvarchar RoofMaterial
        nvarchar BuildingPermitNumber
    }
    
    CollateralVehicle {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        nvarchar VehicleType
        nvarchar Brand
        nvarchar Model
        int Year
        nvarchar Color
        nvarchar EngineNumber
        nvarchar ChassisNumber
        nvarchar PlateNumber
        nvarchar PlateProvince
        int EngineCapacity
        nvarchar FuelType
        int Mileage
    }
    
    CollateralCondo {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        nvarchar ProjectName
        nvarchar BuildingName
        nvarchar UnitNumber
        int Floor
        nvarchar UnitType
        decimal TotalAreaSqm
        decimal UsableAreaSqm
        decimal BalconyAreaSqm
        int BedroomCount
        int BathroomCount
        int TotalFloors
        int TotalUnits
        int YearBuilt
        nvarchar JuristicPersonRegistration
        decimal CommonFee
        int ParkingSpaces
        nvarchar ViewDirection
        nvarchar Position
    }
    
    CollateralLease {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        nvarchar LeaseNumber
        nvarchar LeaseTitle
        nvarchar LeaseType
        nvarchar LessorName
        nvarchar LesseeName
        date LeaseStartDate
        date LeaseEndDate
        int LeaseDurationYears
        decimal MonthlyRent
        decimal SecurityDeposit
        nvarchar PropertyAddress
        decimal LeasedAreaSqm
        nvarchar PermittedUse
        nvarchar RentPaymentStatus
        date LastRentPaymentDate
    }
    
    CollateralMachine {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        nvarchar MachineType
        nvarchar Category
        nvarchar Manufacturer
        nvarchar Model
        nvarchar SerialNumber
        int YearManufactured
        decimal Capacity
        nvarchar CapacityUnit
        nvarchar Dimensions
        decimal Weight
        nvarchar FuelType
        decimal OriginalPurchasePrice
        date PurchaseDate
        nvarchar CurrentLocation
        int UsageHours
        nvarchar OperationalStatus
        int UsefulLifeYears
    }
    
    CollateralVessel {
        uniqueidentifier Id PK
        uniqueidentifier CollateralId FK
        nvarchar VesselName
        nvarchar VesselType
        nvarchar VesselCategory
        nvarchar IMONumber
        nvarchar RegistrationNumber
        nvarchar FlagState
        nvarchar PortOfRegistry
        decimal LengthOverall
        decimal Beam
        decimal GrossTonnage
        int YearBuilt
        nvarchar Shipyard
        nvarchar HullMaterial
        nvarchar EngineType
        nvarchar EngineManufacturer
        decimal EnginePower
        decimal MaxSpeed
        int PassengerCapacity
        nvarchar ClassificationSociety
    }
    
    %% DOCUMENT MANAGEMENT BC - Purple
    Document {
        uniqueidentifier Id PK
        nvarchar RelatedTo
        uniqueidentifier RelatedId
        nvarchar DocumentType
        nvarchar DocumentCategory
        nvarchar FileName
        bigint FileSize
        nvarchar MimeType
        nvarchar StoragePath
        nvarchar StorageType
        nvarchar Checksum UK
        int Version
        uniqueidentifier ParentDocumentId FK
        uniqueidentifier UploadedBy
    }
    
    %% PARAMETER AND CONFIGURATION BC - Orange
    SystemConfiguration {
        uniqueidentifier Id PK
        nvarchar ConfigurationKey UK
        nvarchar ConfigurationValue
        nvarchar DataType
        nvarchar Category
        nvarchar Description
        decimal MinValue
        decimal MaxValue
        bit IsActive
        datetime2 EffectiveFrom
        datetime2 EffectiveTo
        uniqueidentifier CreatedBy FK
        bit RequiresApproval
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    ConfigurationHistory {
        uniqueidentifier Id PK
        uniqueidentifier SystemConfigurationId FK
        nvarchar OldValue
        nvarchar NewValue
        nvarchar ChangeReason
        uniqueidentifier ChangedBy FK
        uniqueidentifier ApprovedBy FK
        datetime2 ChangedDate
        datetime2 ApprovedDate
        nvarchar ChangeStatus
        datetime2 UploadedDate
        date ExpiryDate
        bit IsActive
        nvarchar Tags
        nvarchar Metadata
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    PhotoGallery {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        nvarchar UploadBatchId
        uniqueidentifier UploadedBy
        datetime2 UploadedDate
        nvarchar FileName
        bigint FileSize
        datetime2 TakenDate
        decimal GPSLatitude
        decimal GPSLongitude
        decimal GPSAccuracy
        nvarchar CameraModel
        nvarchar CameraSettings
        int CompassDirection
        nvarchar InitialNotes
        nvarchar Tags
        int QualityRating
        bit IsAssigned
        int AssignmentCount
        nvarchar FinalStatus "Pending, AssignedToCollateral, KeptWithInspection"
        datetime2 FinalizedDate
        uniqueidentifier FinalizedBy
        int ImageWidth
        int ImageHeight
        nvarchar ColorSpace
        uniqueidentifier DocumentId FK
        uniqueidentifier ThumbnailDocumentId FK
        nvarchar PhotoStatus
        nvarchar ReviewStatus
        uniqueidentifier ReviewedBy
        datetime2 ReviewedDate
        nvarchar ReviewNotes
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    PhotoAssignment {
        uniqueidentifier Id PK
        uniqueidentifier PhotoGalleryId FK
        nvarchar AssignmentType "Collateral, Inspection, CollateralGroup, Appraisal"
        uniqueidentifier AssignmentId
        nvarchar SourceType "Original, CopiedFromInspection"
        uniqueidentifier SourceInspectionId FK
        bit IsPrimaryAssignment
        datetime2 CopyDate
        uniqueidentifier AssignedBy
        datetime2 AssignedDate
        nvarchar PhotoCategory
        nvarchar PhotoPurpose
        nvarchar SpecificArea
        nvarchar ViewDirection
        int DisplayOrder
        nvarchar Caption
        bit UseInReport
        bit IsKeyPhoto
        nvarchar ConditionEvidence
        nvarchar ValuationRelevance
        nvarchar ObservationNotes
        nvarchar ComparisonType
        uniqueidentifier RelatedPhotoId FK
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }

    Request ||--o{ RequestTitle : "describes"
    Request ||--o{ RequestApplicant : "has"
    Request ||--o{ RequestProperty : "has"
    Request ||--o{ RequestComment : "has"
    Request ||--o| Appraisal : "creates"
    %% Multiple requests can be grouped in one quotation
    QuotationRequest ||--o{ QuotationRequestItem : "contains"
    QuotationRequestItem }|--|| Request : "includes"
    
    QuotationRequest ||--o{ CompanyQuotation : "receives"
    CompanyQuotation ||--o{ CompanyQuotationItem : "itemizes"
    CompanyQuotationItem }|--|| QuotationRequestItem : "quotes"
    QuotationRequest }|--|| AppraisalCompany : "awards_to"
    CompanyQuotation }|--|| AppraisalCompany : "submitted_by"
    
    Appraisal ||--|| Assignment : "has"
    Appraisal ||--o{ AppraisalInspection : "conducts"
    Appraisal ||--o{ ComparableProperty : "analyzes"
    Appraisal ||--o{ SalesGridAnalysis : "produces"
    Appraisal ||--o{ WqsAnalysis : "evaluates"
    Appraisal ||--o{ ValuationResult : "generates"
    Appraisal ||--o| ValueReconciliation : "concludes"
    Appraisal ||--o{ PhotoGallery : "uploads"
    
    ValuationResult ||--o{ ValuationAllocation : "allocates"
    CollateralGroup ||--o{ ValuationResult : "valued_as_group"
    Collateral ||--o{ ValuationResult : "valued_individually"
    ValuationAllocation }|--|| Collateral : "allocates_to"
    
    PhotoGallery ||--o{ PhotoAssignment : "assigns"
    PhotoGallery }|--|| Document : "stores"
    PhotoGallery }o--|| Document : "thumbnail"
    PhotoAssignment }o--|| Collateral : "assigns_to"
    PhotoAssignment }o--|| AppraisalInspection : "assigns_to"
    PhotoAssignment }o--|| CollateralGroup : "assigns_to"
    PhotoAssignment }o--|| PhotoAssignment : "relates_to"
    
    AppraisalInspection ||--o| AppraisalBuildingDetail : "building_details"
    AppraisalInspection ||--o| AppraisalLandDetail : "land_details"
    AppraisalInspection ||--o| AppraisalCondoDetail : "condo_details"
    AppraisalInspection ||--o| AppraisalMachineDetail : "machine_details"
    AppraisalInspection ||--o| AppraisalVehicleDetail : "vehicle_details"
    AppraisalInspection ||--o| AppraisalLeaseDetail : "lease_details"
    AppraisalInspection ||--o| AppraisalVesselDetail : "vessel_details"
    AppraisalInspection ||--|| Collateral : "inspects"
    
    %% Appointment and Fee Relationships
    Appraisal ||--o| AppraisalAppointment : "schedules"
    Appraisal ||--o| AppraisalFee : "generates"
    AppraisalFee ||--o{ AppraisalFeeItem : "itemizes"
    AppraisalFee ||--o{ FeeAdjustment : "adjusts"
    AppraisalFee ||--o{ FeePayment : "pays"
    FeeStructure ||--o{ AppraisalFeeItem : "defines"
    
    %% Configuration Relationships
    SystemConfiguration ||--o{ ConfigurationHistory : "tracks"
    
    SalesGridAnalysis ||--o{ SalesGridAdjustment : "calculates"
    SalesGridAdjustment }|--|| ComparableProperty : "adjusts"
    
    WqsAnalysis ||--o{ WqsCriteriaScore : "scores"
    WqsAnalysis }|--|| ValuationTemplate : "uses"
    
    SalesGridAnalysis }|--|| ValuationResult : "supports"
    WqsAnalysis }|--|| ValuationResult : "supports"
    
    ValuationResult }o--|| Collateral : "values"
    
    Appraisal ||--o{ CollateralGroup : "organizes"
    CollateralGroup ||--o{ CollateralGroupMember : "contains"
    CollateralGroupMember }|--|| Collateral : "includes"
    
    Collateral ||--o| CollateralLand : "specializes"
    Collateral ||--o| CollateralBuilding : "specializes"
    Collateral ||--o| CollateralVehicle : "specializes"
    Collateral ||--o| CollateralCondo : "specializes"
    Collateral ||--o| CollateralLease : "specializes" 
    Collateral ||--o| CollateralMachine : "specializes"
    Collateral ||--o| CollateralVessel : "specializes"
    
    CollateralLand ||--o{ LandTitleDetail : "composed_of"
    CollateralLand ||--o{ CollateralBuilding : "supports"
    
    Document ||--o| Document : "versions"
    
    %% APPOINTMENT MANAGEMENT (part of Appraisal Management BC) - Green
    AppraisalAppointment {
        uniqueidentifier Id PK
        nvarchar AppointmentNumber UK
        uniqueidentifier AppraisalId FK
        nvarchar Status
        nvarchar AppointmentType
        datetime2 ScheduledDate
        datetime2 ActualDate
        datetime2 CompletedDate
        int EstimatedDuration
        int ActualDuration
        nvarchar MeetingLocation
        uniqueidentifier AppraiserId FK
        nvarchar ContactPerson
        nvarchar ContactPhone
        bit ConfirmedByCustomer
        int RescheduleCount
        int TotalCollaterals
        int CollateralsCompleted
        uniqueidentifier CreatedBy FK
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppointmentHistory {
        uniqueidentifier Id PK
        uniqueidentifier AppointmentId FK
        nvarchar Action
        nvarchar PreviousStatus
        nvarchar NewStatus
        datetime2 PreviousDate
        datetime2 NewDate
        nvarchar Reason
        uniqueidentifier ActionBy FK
        datetime2 ActionDate
    }
    
    %% FEE MANAGEMENT (part of Appraisal Management BC) - Green
    FeeStructure {
        uniqueidentifier Id PK
        nvarchar FeeCode UK
        nvarchar FeeName
        nvarchar FeeCategory
        nvarchar FeeType
        decimal BaseAmount
        decimal PercentageRate
        nvarchar UnitType
        decimal MinPropertyValue
        decimal MaxPropertyValue
        nvarchar ApplicableCollateralTypes
        bit IsActive
        datetime2 EffectiveFrom
        datetime2 EffectiveTo
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalFee {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalId FK
        nvarchar FeeNumber UK
        decimal SubTotal
        decimal DiscountAmount
        decimal AmountBeforeVAT
        decimal VATRate
        decimal VATAmount
        datetime2 TaxCalculationDate
        nvarchar VATConfigKey
        decimal TotalAmount
        nvarchar Currency
        nvarchar Status
        nvarchar PaymentStatus
        decimal TotalPaidAmount
        uniqueidentifier SubmittedBy FK
        uniqueidentifier ApprovedBy FK
        datetime2 SubmittedDate
        datetime2 ApprovedDate
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    AppraisalFeeItem {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        uniqueidentifier FeeStructureId FK
        nvarchar ItemName
        nvarchar ItemCategory
        decimal Quantity
        decimal UnitPrice
        decimal Amount
        bit IsManualEntry
        datetime2 CreatedAt
    }
    
    FeeAdjustment {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        nvarchar AdjustmentNumber UK
        nvarchar AdjustmentType
        decimal AdjustmentAmount
        decimal AmountBefore
        decimal AmountAfter
        nvarchar Reason
        nvarchar Status
        uniqueidentifier RequestedBy FK
        uniqueidentifier ApprovedBy FK
        uniqueidentifier RejectedBy FK
        bit IsApplied
        datetime2 RequestedDate
        datetime2 ApprovedDate
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    FeePayment {
        uniqueidentifier Id PK
        uniqueidentifier AppraisalFeeId FK
        nvarchar PaymentNumber UK
        datetime2 PaymentDate
        decimal Amount
        nvarchar Currency
        nvarchar PaymentMethod
        nvarchar ReferenceNumber
        nvarchar PayerName
        nvarchar Status
        uniqueidentifier VerifiedBy FK
        datetime2 VerifiedDate
        uniqueidentifier CreatedBy FK
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    
    %% APPOINTMENT RELATIONSHIPS
    Appraisal ||--o| AppraisalAppointment : "schedules"
    AppraisalAppointment ||--o{ AppointmentHistory : "tracks_changes"
    User ||--o{ AppraisalAppointment : "conducts"
    User ||--o{ AppointmentHistory : "records"
    
    %% FEE RELATIONSHIPS
    Appraisal ||--o| AppraisalFee : "calculates"
    AppraisalFee ||--o{ AppraisalFeeItem : "itemizes"
    AppraisalFee ||--o{ FeeAdjustment : "adjusts"
    AppraisalFee ||--o{ FeePayment : "pays"
    FeeStructure ||--o{ AppraisalFeeItem : "templates"
    User ||--o{ AppraisalFee : "approves"
    User ||--o{ FeeAdjustment : "requests"
    User ||--o{ FeeAdjustment : "approves_adjustment"
    User ||--o{ FeePayment : "processes"