# Entity Relationship Diagrams

## Overview

This document contains Entity Relationship (ER) diagrams for the Collateral Appraisal System. The diagrams are organized by module to show clear boundaries and relationships.

## Complete System Overview

```mermaid
erDiagram
    %% HIGH-LEVEL MODULE RELATIONSHIPS
    Request ||--|| Appraisal : triggers
    Appraisal ||--|| Collateral : creates
    Request ||--o{ Document : references
    Appraisal ||--o{ Document : references
    Collateral ||--o{ Document : references
    User ||--o{ Request : creates
    User ||--o{ Appraisal : performs
```

## Module 1: Request Management

```mermaid
erDiagram
    Request ||--o{ RequestCustomer : "has customers"
    Request ||--o{ RequestPropertyType : "has property types"
    Request ||--o{ RequestDocument : "has documents"
    Request ||--o{ TitleDeedInfo : "has title deeds"
    Request ||--o{ RequestStatusHistory : "tracks history"
    Request }o--|| User : "requested by"

    TitleDeedInfo }o--o| RequestPropertyType : "linked to property"
    RequestDocument }o--|| Document : references

    Request {
        guid Id PK
        string RequestNumber UK "REQ-2025-00001"
        datetime RequestDate
        enum RequestType "NewLoan, Refinance, Revaluation"
        string LoanApplicationNumber
        decimal LoanAmount
        string LoanCurrency "THB, USD, etc."
        string AppraisalPurpose "Collateral, Insurance, Sale"
        enum Priority "Low, Normal, High, Urgent"
        enum Status "Draft, Submitted, Assigned, InProgress, Completed, Cancelled"
        guid RequestedBy FK
        string RequestedByName
        string RequestedByEmail
        string BranchCode
        string BranchName
        enum SourceSystem "Manual, LOS_CoreBanking"
        datetime CreatedOn
        guid CreatedBy
        rowversion RowVersion
    }

    RequestCustomer {
        guid Id PK
        guid RequestId FK
        string FirstName "Customer first name"
        string LastName "Customer last name"
        string ContactNumber "Phone number"
        int DisplayOrder "For ordering multiple customers"
        datetime CreatedOn
        guid CreatedBy
        datetime UpdatedOn
        guid UpdatedBy
    }

    RequestPropertyType {
        guid Id PK
        guid RequestId FK
        enum PropertyType "Land, Building, LandAndBuilding, Condo, Vehicle, Vessel, Machinery"
        string PropertySubType "SingleHouse, Townhouse, Apartment, Car, etc."
        int DisplayOrder "For ordering multiple properties"
        datetime CreatedOn
        guid CreatedBy
        datetime UpdatedOn
        guid UpdatedBy
    }

    TitleDeedInfo {
        guid Id PK
        guid RequestId FK
        guid RequestPropertyTypeId FK "Optional link"
        string TitleDeedNumber
        enum DeedType "Chanote, NorSor3, NorSor3Kor, NorSor4"
        date IssueDate
        string IssueOffice
        string SurveyNumber
        string SurveySheet
        string ParcelNumber
        string SubDistrict
        string District
        string Province
        decimal AreaRai
        decimal AreaNgan
        decimal AreaSquareWa
        decimal TotalSquareMeters
        string OwnerName
        string OwnerIdCard
        enum OwnershipType "FullOwnership, CoOwnership, Usufruct"
        decimal OwnershipPercentage
        bool HasMortgage
        string MortgageeBank
        decimal MortgageAmount
        bool HasLease
        bool HasEasement
        int DisplayOrder
    }

    RequestStatusHistory {
        guid Id PK
        guid RequestId FK
        enum FromStatus
        enum ToStatus
        datetime StatusChangedAt
        guid StatusChangedBy FK
        string StatusChangedByName
        string ChangeReason
        string Comments
    }

    RequestDocument {
        guid Id PK
        guid RequestId FK
        guid DocumentId FK "References document.Documents"
        enum DocumentType "TitleDeed, IDCard, HouseRegistration"
        enum DocumentCategory "Required, Optional, Supporting"
        bool IsRequired
        string DocumentDescription
        int PageCount
        guid UploadedBy FK
        datetime UploadedAt
        bool IsVerified
        guid VerifiedBy FK
        datetime VerifiedAt
        int DisplayOrder
    }
```

## Module 2: Appraisal Core

```mermaid
erDiagram
    Appraisal ||--o{ AppraisalAssignment : "has assignments"
    Appraisal ||--o{ FieldSurvey : "has surveys"
    Appraisal ||--o{ PropertyInformation : "has info"
    Appraisal ||--o{ ValuationAnalysis : "has analysis"
    Appraisal ||--o{ AppraisalReport : "produces reports"
    Appraisal ||--o{ AppraisalReview : "undergoes review"
    Appraisal ||--o{ AppraisalWorkflow : "follows workflow"

    ValuationAnalysis ||--o{ ComparableProperty : "uses comparables"

    Appraisal {
        guid Id PK
        string AppraisalNumber UK "e.g., APP-2025-0001"
        guid RequestId FK
        enum AppraisalType "Initial, Revaluation, Special"
        enum Status "Pending, Assigned, InProgress, Surveyed, Reported, Reviewed, Approved"
        enum Priority "Normal, Urgent"
        datetime AssignmentDate
        datetime DueDate
        datetime CompletedDate
        json AppraisalFirm "FirmId, Name, Type, Contact"
        datetime CreatedOn
        guid CreatedBy
    }

    AppraisalAssignment {
        guid Id PK
        guid AppraisalId FK
        guid AssignedTo FK
        enum Role "PrimaryAppraiser, SecondaryAppraiser, Checker, Verifier"
        datetime AssignedDate
        datetime AcceptedDate
        enum Status "Pending, Accepted, Declined, Completed"
    }

    FieldSurvey {
        guid Id PK
        guid AppraisalId FK
        datetime SurveyDate
        guid SurveyedBy FK
        json Location "Address, Coordinates"
        json AccessibilityInfo
        json SurroundingArea
        text Observations
        decimal Latitude
        decimal Longitude
    }

    ValuationAnalysis {
        guid Id PK
        guid AppraisalId FK
        enum ValuationMethod "CostApproach, MarketComparison, IncomeApproach"
        decimal MarketValue
        decimal AppraisedValue
        decimal ForcedSaleValue
        decimal LoanToValue
        text Methodology
        text Assumptions
        guid AnalyzedBy FK
    }

    AppraisalReport {
        guid Id PK
        guid AppraisalId FK
        string ReportNumber UK
        datetime ReportDate
        enum ReportType "Preliminary, Final"
        text ExecutiveSummary
        text PropertyDescription
        text ValuationSummary
        text Conclusion
        guid PreparedBy FK
        guid ReviewedBy FK
        guid ApprovedBy FK
    }

    AppraisalReview {
        guid Id PK
        guid AppraisalId FK
        enum ReviewLevel "Checker, Verifier, Committee"
        guid ReviewedBy FK
        datetime ReviewDate
        enum Status "Approved, Rejected, ReturnForRevision"
        text Comments
        json Issues
    }
```

## Module 3: Property Details

```mermaid
erDiagram
    Appraisal ||--o| LandAppraisalDetail : "has detail"
    Appraisal ||--o| BuildingAppraisalDetail : "has detail"
    Appraisal ||--o| CondoAppraisalDetail : "has detail"
    Appraisal ||--o| VehicleAppraisalDetail : "has detail"
    Appraisal ||--o| VesselAppraisalDetail : "has detail"
    Appraisal ||--o| MachineryAppraisalDetail : "has detail"

    LandAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        enum Topography "Flat, Sloped, Hilly"
        enum LandShape "Regular, Irregular, Rectangular"
        enum SoilType "Clay, Sandy, Loam, Rocky"
        decimal FrontageWidth "meters"
        decimal Depth "meters"
        bool CornerLot
        bool HasElectricity
        bool HasWater
        bool HasSewageSystem
        string Zoning
        enum LandUse "Residential, Commercial, Agricultural"
        enum DevelopmentPotential "High, Medium, Low"
        guid LastUpdatedBy FK
    }

    BuildingAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        enum BuildingType "House, Townhouse, Commercial"
        int NumberOfFloors
        decimal TotalBuildingArea "sqm"
        decimal UsableArea "sqm"
        int ConstructionYear
        int EffectiveAge
        enum RoofType
        enum RoofMaterial
        enum RoofCondition
        enum FloorMaterial
        enum FloorCondition
        bool IsDecorated
        enum DecorationLevel "Basic, Standard, Luxury"
        int NumberOfBedrooms
        int NumberOfBathrooms
        bool HasAirConditioner
        int NumberOfACUnits
        enum OverallCondition "Excellent, Good, Fair, Poor"
        text Defects
        decimal EstimatedRepairCost
        guid LastUpdatedBy FK
    }

    CondoAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        string ProjectName
        string Developer
        int TotalUnits
        int BuildYear
        string UnitNumber
        int Floor
        enum UnitType "Studio, OneBedroom, TwoBedroom"
        decimal UnitArea "sqm"
        decimal BalconyArea "sqm"
        int NumberOfBedrooms
        int NumberOfBathrooms
        int ParkingSpaces
        decimal MonthlyFee "Baht"
        enum ViewType "City, Garden, Pool, Mountain"
        bool HasSwimmingPool
        bool HasFitnessCenter
        bool HasSecurity
        guid LastUpdatedBy FK
    }

    VehicleAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        enum VehicleType "Sedan, SUV, Truck, Pickup"
        string Make
        string Model
        int Year
        string RegistrationNumber
        string ChassisNumber
        string EngineNumber
        decimal Mileage "km"
        enum BodyCondition
        enum PaintCondition
        enum EngineCondition
        enum TransmissionCondition
        bool HasRust
        enum AccidentHistory "None, Minor, Major"
        enum OverallCondition
        guid LastUpdatedBy FK
    }

    VesselAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        enum VesselType "SpeedBoat, SailingYacht, MotorYacht"
        string VesselName
        string RegistrationNumber
        string HullNumber
        int YearBuilt
        enum HullMaterial "Fiberglass, Steel, Aluminum"
        decimal Length "meters"
        decimal GrossTonnage
        int NumberOfEngines
        decimal EngineHours
        enum HullCondition
        bool HasOsmosisBlisters
        enum OverallCondition
        guid LastUpdatedBy FK
    }

    MachineryAppraisalDetail {
        guid Id PK
        guid AppraisalId FK
        enum MachineryType "CNC, Press, Lathe, Generator"
        enum Category "Production, Construction, Agricultural"
        string Manufacturer
        string Model
        string SerialNumber
        int YearOfManufacture
        string Capacity
        decimal HoursUsed
        enum OperationalStatus "Operational, Idle, UnderRepair"
        enum OverallCondition
        enum MaintenanceHistory "Excellent, Good, Poor"
        enum DemandLevel "High, Medium, Low, Obsolete"
        guid LastUpdatedBy FK
    }
```

## Module 4: Photo Gallery & Media

```mermaid
erDiagram
    Appraisal ||--o{ AppraisalGallery : "has galleries"
    Appraisal ||--o{ VideoRecording : "has videos"
    Appraisal ||--o{ AudioNote : "has audio"
    Appraisal ||--o{ UploadSession : "tracks sessions"

    AppraisalGallery ||--o{ GalleryPhoto : "contains photos"

    GalleryPhoto ||--o{ PhotoAnnotation : "has annotations"
    GalleryPhoto ||--o{ PropertyPhotoMapping : "mapped to sections"
    GalleryPhoto }o--|| Document : references

    PropertyPhotoMapping }o--|| LandAppraisalDetail : "links to"
    PropertyPhotoMapping }o--|| BuildingAppraisalDetail : "links to"
    PropertyPhotoMapping }o--|| CondoAppraisalDetail : "links to"
    PropertyPhotoMapping }o--|| VehicleAppraisalDetail : "links to"
    PropertyPhotoMapping }o--|| VesselAppraisalDetail : "links to"
    PropertyPhotoMapping }o--|| MachineryAppraisalDetail : "links to"

    AppraisalGallery {
        guid Id PK
        guid AppraisalId FK
        string GalleryName "e.g., Site Visit - 2025-01-15"
        string Description
        datetime CreatedOn
        guid CreatedBy FK
    }

    GalleryPhoto {
        guid Id PK
        guid GalleryId FK
        guid DocumentId FK
        int PhotoNumber
        int DisplayOrder
        enum PhotoType "Exterior, Interior, Land, Defect"
        enum PhotoCategory "Front, Back, Kitchen, Bathroom"
        string Caption
        decimal Latitude "GPS"
        decimal Longitude "GPS"
        datetime CapturedAt
        guid CapturedBy FK
        string ThumbnailUrl
        string FullSizeUrl
        long FileSize
        int Width "pixels"
        int Height "pixels"
        bool IsSelected
        bool IsUsedInReport
        json Tags
        datetime UploadedAt
        guid UploadedBy FK
    }

    PhotoAnnotation {
        guid Id PK
        guid PhotoId FK
        enum AnnotationType "Arrow, Circle, Rectangle, Text"
        json CoordinatesData "x, y, width, height"
        string AnnotationText
        string Color "hex code"
        int StrokeWidth
        enum IssueType "Defect, Damage, Concern, Note"
        enum Severity "Critical, Major, Minor, Info"
        guid CreatedBy FK
        datetime CreatedAt
    }

    PropertyPhotoMapping {
        guid Id PK
        guid PhotoId FK
        enum PropertyDetailType "Land, Building, Condo, etc."
        guid PropertyDetailId FK "Polymorphic"
        enum PhotoPurpose "Overview, Detail, Defect"
        string SectionReference "Roof, Kitchen, Engine"
        text Notes
        guid LinkedBy FK
        datetime LinkedAt
    }

    VideoRecording {
        guid Id PK
        guid AppraisalId FK
        guid DocumentId FK
        string VideoTitle
        enum VideoType "Walkthrough, Demonstration"
        int Duration "seconds"
        string VideoUrl
        string ThumbnailUrl
        datetime RecordedAt
        guid RecordedBy FK
    }

    AudioNote {
        guid Id PK
        guid AppraisalId FK
        guid DocumentId FK
        string AudioTitle
        enum AudioType "VoiceNote, Interview, Observation"
        int Duration "seconds"
        string AudioUrl
        text TranscriptionText
        enum TranscriptionStatus "None, InProgress, Completed"
        guid RelatedPhotoId FK
        datetime RecordedAt
        guid RecordedBy FK
    }

    UploadSession {
        guid Id PK
        guid AppraisalId FK
        enum SessionType "SiteVisit, OfficeUpload"
        string SessionName
        enum UploadMethod "Mobile, Web"
        string DeviceType "iOS, Android"
        string DeviceModel
        int TotalPhotos
        int TotalVideos
        long TotalSize "bytes"
        datetime UploadStarted
        datetime UploadCompleted
        enum Status "InProgress, Completed, Failed"
        guid UploadedBy FK
    }
```

## Module 5: Collateral Management

```mermaid
erDiagram
    Collateral ||--o{ CollateralValuationHistory : "has history"
    Collateral ||--o{ CollateralDocument : "has documents"
    Collateral ||--o| LandCollateral : "is a"
    Collateral ||--o| BuildingCollateral : "is a"
    Collateral ||--o| CondoCollateral : "is a"
    Collateral ||--o| VehicleCollateral : "is a"
    Collateral ||--o| VesselCollateral : "is a"
    Collateral ||--o| MachineryCollateral : "is a"

    CollateralDocument }o--|| Document : references

    Collateral {
        guid Id PK
        string CollateralNumber UK "e.g., COL-2025-0001"
        guid AppraisalId FK
        enum CollateralType "Land, Building, Condo, etc."
        enum Status "Draft, Active, Inactive, Disposed"
        enum OwnershipStatus "Owned, Mortgaged, Leased"
        json OwnerInfo
        json Location
        decimal AppraisedValue
        decimal MarketValue
        decimal ForcedSaleValue
        datetime ValuationDate
        datetime NextRevaluationDate
        json LegalStatus
        datetime CreatedOn
        guid CreatedBy
    }

    LandCollateral {
        guid CollateralId PK
        string DeedNumber
        enum DeedType
        json LandArea "Rai, Ngan, SquareWa"
        json LandCharacteristics
        json Utilities
        string Zoning
    }

    BuildingCollateral {
        guid CollateralId PK
        enum BuildingType
        int NumberOfFloors
        decimal TotalArea "sqm"
        decimal UsableArea "sqm"
        int BuildingAge
        enum BuildingCondition
        json ConstructionMaterial
        json Facilities
    }

    CondoCollateral {
        guid CollateralId PK
        string ProjectName
        string UnitNumber
        int Floor
        decimal UnitArea "sqm"
        int NumberOfBedrooms
        int NumberOfBathrooms
        int ParkingSpaces
        decimal CondoFee
        json ProjectInfo
    }

    VehicleCollateral {
        guid CollateralId PK
        enum VehicleType
        string Make
        string Model
        int Year
        string RegistrationNumber
        string ChassisNumber
        string EngineNumber
        decimal Mileage
        string Color
        enum FuelType
        enum VehicleCondition
    }

    VesselCollateral {
        guid CollateralId PK
        enum VesselType
        string VesselName
        string RegistrationNumber
        string HullNumber
        int YearBuilt
        decimal Length
        decimal GrossTonnage
        json EngineInfo
        enum VesselCondition
    }

    MachineryCollateral {
        guid CollateralId PK
        enum MachineryType
        string Manufacturer
        string Model
        string SerialNumber
        int YearOfManufacture
        string Capacity
        enum Condition
        enum OperationalStatus
    }

    CollateralValuationHistory {
        guid Id PK
        guid CollateralId FK
        datetime ValuationDate
        decimal AppraisedValue
        decimal MarketValue
        decimal ForcedSaleValue
        guid ValuedBy FK
        string Methodology
    }
```

## Module 6: Document Management

```mermaid
erDiagram
    Document ||--o{ DocumentVersion : "has versions"
    Document ||--o{ DocumentRelationship : "has relationships"
    Document ||--o{ DocumentAccess : "controls access"
    Document ||--o{ DocumentAccessLog : "logs access"
    Document }o--|| User : "uploaded by"

    DocumentRelationship }o--|| Request : "relates to"
    DocumentRelationship }o--|| Appraisal : "relates to"
    DocumentRelationship }o--|| Collateral : "relates to"

    Document {
        guid Id PK
        string DocumentNumber UK "e.g., DOC-2025-0001"
        string FileName
        string OriginalFileName
        string FileExtension
        long FileSize "bytes"
        string MimeType
        json StorageLocation "Type, Bucket, Path"
        enum DocumentCategory "Request, Appraisal, Collateral"
        enum DocumentType "TitleDeed, ID, Photo, Report"
        enum Status "Active, Archived, Deleted"
        json Metadata "Title, Description, Tags"
        json Security "IsConfidential, AccessLevel"
        int Version
        string Checksum "SHA256"
        guid UploadedBy FK
        datetime UploadedAt
        datetime CreatedOn
    }

    DocumentVersion {
        guid Id PK
        guid DocumentId FK
        int VersionNumber
        string FileName
        long FileSize
        json StorageLocation
        string Checksum
        string ChangeReason
        guid CreatedBy FK
        datetime CreatedOn
    }

    DocumentRelationship {
        guid Id PK
        guid DocumentId FK
        enum RelatedEntityType "Request, Appraisal, Collateral"
        guid RelatedEntityId "Polymorphic"
        enum RelationshipType "Attachment, Reference"
        int DisplayOrder
        bool IsMandatory
        guid LinkedBy FK
        datetime LinkedAt
    }

    DocumentAccess {
        guid Id PK
        guid DocumentId FK
        guid UserId FK
        enum AccessType "View, Download, Edit, Delete"
        guid AccessGrantedBy FK
        datetime GrantedAt
        datetime ExpiresAt
        datetime RevokedAt
    }

    DocumentAccessLog {
        guid Id PK
        guid DocumentId FK
        guid UserId FK
        enum Action "View, Download, Upload, Edit, Delete"
        datetime AccessedAt
        string IpAddress
        bool Success
    }
```

## Module 7: Authentication & Authorization

```mermaid
erDiagram
    User ||--o{ UserRole : "has roles"
    User ||--o{ UserPermission : "has permissions"
    User ||--o{ UserOrganization : "belongs to orgs"
    User ||--o{ AuditLog : "performs actions"

    Role ||--o{ UserRole : "assigned to users"
    Role ||--o{ RolePermission : "has permissions"

    Permission ||--o{ RolePermission : "granted to roles"
    Permission ||--o{ UserPermission : "granted to users"

    Organization ||--o{ UserOrganization : "employs users"

    User {
        guid Id PK
        string Username UK
        string Email UK
        string FirstName
        string LastName
        string EmployeeId UK
        string PhoneNumber
        enum Status "Active, Inactive, Suspended"
        enum UserType "Internal, External"
        string Department
        string Position
        datetime LastLoginAt
        datetime CreatedOn
    }

    Role {
        guid Id PK
        string Name UK "RM, Admin, InternalStaff, etc."
        string NormalizedName
        string Description
        enum RoleType "System, Custom"
        bool IsActive
        datetime CreatedOn
    }

    Permission {
        guid Id PK
        string Name UK
        string Resource "Request, Appraisal, Collateral"
        enum Action "Create, Read, Update, Delete"
        enum Module
        string Description
    }

    UserRole {
        guid Id PK
        guid UserId FK
        guid RoleId FK
        guid AssignedBy FK
        datetime AssignedAt
        datetime ExpiresAt
        bool IsActive
    }

    RolePermission {
        guid Id PK
        guid RoleId FK
        guid PermissionId FK
        datetime GrantedAt
    }

    UserPermission {
        guid Id PK
        guid UserId FK
        guid PermissionId FK
        bool IsGranted "true=grant, false=revoke"
        guid GrantedBy FK
        datetime GrantedAt
        datetime ExpiresAt
    }

    Organization {
        guid Id PK
        string Name
        string RegistrationNumber UK
        enum OrganizationType "AppraisalFirm, Bank"
        enum Status "Active, Inactive"
        json ContactInfo
        json Address
        json LicenseInfo
        datetime CreatedOn
    }

    UserOrganization {
        guid Id PK
        guid UserId FK
        guid OrganizationId FK
        string Position
        bool IsActive
        datetime JoinedDate
        datetime LeftDate
    }

    AuditLog {
        guid Id PK
        guid UserId FK
        string Action
        string EntityType
        string EntityId
        json Changes "Old/New values"
        string IpAddress
        datetime Timestamp
        string Module
    }
```

## Cross-Module Integration Flow

```mermaid
erDiagram
    %% COMPLETE WORKFLOW
    User ||--o{ Request : creates
    Request ||--|| Appraisal : triggers
    Appraisal ||--o{ AppraisalGallery : captures_photos
    Appraisal ||--o| BuildingAppraisalDetail : analyzes_property
    GalleryPhoto ||--o{ PropertyPhotoMapping : links_to
    PropertyPhotoMapping }o--|| BuildingAppraisalDetail : documents
    Appraisal ||--|| Collateral : creates_final_record

    %% WORKFLOW STEPS
    Request {
        string Status "1. RM creates request"
    }

    Appraisal {
        string Status "2. System creates appraisal"
        string Phase "3. Appraiser site visit"
    }

    AppraisalGallery {
        string Purpose "3a. Upload photos mobile"
    }

    BuildingAppraisalDetail {
        string Purpose "4. Back at office create detail"
    }

    PropertyPhotoMapping {
        string Purpose "5. Link photos to sections"
    }

    Collateral {
        string Status "6. Appraisal approved create collateral"
    }
```

## Data Flow Diagram

```
┌──────────────┐
│   Request    │ Step 1: RM creates request
│   Created    │ ────────┐
└──────────────┘         │
                         ▼
                  ┌─────────────┐
                  │  Domain     │
                  │   Event     │
                  │  Published  │
                  └──────┬──────┘
                         │
                         ▼
                  ┌─────────────┐
                  │  Appraisal  │ Step 2: System creates appraisal
                  │  Created    │
                  └──────┬──────┘
                         │
                         ▼
                  ┌─────────────┐
                  │ Appraiser   │ Step 3: Site visit
                  │ Site Visit  │
                  └──────┬──────┘
                         │
                    ┌────┴────┐
                    │         │
                    ▼         ▼
        ┌──────────────┐ ┌──────────────┐
        │Upload Photos │ │Upload Videos │ Step 3a: Mobile capture
        │to Gallery    │ │& Audio Notes │
        └──────┬───────┘ └──────────────┘
               │
               ▼
        ┌──────────────┐
        │Back at Office│ Step 4: Create property detail
        │Create Detail │
        └──────┬───────┘
               │
               ▼
        ┌──────────────┐
        │Link Photos to│ Step 5: Map photos to sections
        │Property      │
        │Sections      │
        └──────┬───────┘
               │
               ▼
        ┌──────────────┐
        │Add           │ Step 6: Annotate issues
        │Annotations   │
        └──────┬───────┘
               │
               ▼
        ┌──────────────┐
        │Create        │ Step 7: Generate report
        │Appraisal     │
        │Report        │
        └──────┬───────┘
               │
               ▼
        ┌──────────────┐
        │Review &      │ Step 8: Approval workflow
        │Approval      │
        └──────┬───────┘
               │
               ▼
        ┌──────────────┐
        │Collateral    │ Step 9: Create final collateral record
        │Created       │
        └──────────────┘
```

## Indexing Strategy

### Critical Indexes

```sql
-- Request Module
CREATE INDEX IX_Request_Status ON request.Requests(Status);
CREATE INDEX IX_Request_RequestedBy ON request.Requests(RequestedBy);
CREATE INDEX IX_Request_CreatedOn ON request.Requests(CreatedOn DESC);
CREATE INDEX IX_Request_RequestNumber ON request.Requests(RequestNumber);

-- Appraisal Module
CREATE INDEX IX_Appraisal_Status_DueDate ON appraisal.Appraisals(Status, DueDate);
CREATE INDEX IX_Appraisal_RequestId ON appraisal.Appraisals(RequestId);
CREATE INDEX IX_Appraisal_CreatedOn ON appraisal.Appraisals(CreatedOn DESC);

-- Gallery Photos
CREATE INDEX IX_GalleryPhoto_GalleryId ON appraisal.GalleryPhotos(GalleryId);
CREATE INDEX IX_GalleryPhoto_PhotoType ON appraisal.GalleryPhotos(PhotoType);
CREATE INDEX IX_GalleryPhoto_IsUsedInReport ON appraisal.GalleryPhotos(IsUsedInReport)
    WHERE IsUsedInReport = 1;

-- Property Photo Mapping
CREATE INDEX IX_PropertyPhotoMapping_PhotoId ON appraisal.PropertyPhotoMappings(PhotoId);
CREATE INDEX IX_PropertyPhotoMapping_PropertyDetail
    ON appraisal.PropertyPhotoMappings(PropertyDetailType, PropertyDetailId);
CREATE INDEX IX_PropertyPhotoMapping_SectionReference
    ON appraisal.PropertyPhotoMappings(SectionReference);

-- Collateral Module
CREATE INDEX IX_Collateral_AppraisalId ON collateral.Collaterals(AppraisalId);
CREATE INDEX IX_Collateral_Status ON collateral.Collaterals(Status);
CREATE INDEX IX_Collateral_CollateralType ON collateral.Collaterals(CollateralType);

-- Document Module
CREATE INDEX IX_Document_DocumentCategory ON document.Documents(DocumentCategory);
CREATE INDEX IX_Document_Status ON document.Documents(Status);
CREATE INDEX IX_DocumentRelationship_RelatedEntity
    ON document.DocumentRelationships(RelatedEntityType, RelatedEntityId);

-- Auth Module
CREATE INDEX IX_User_Email ON auth.Users(Email);
CREATE INDEX IX_User_Status ON auth.Users(Status);
CREATE INDEX IX_UserRole_UserId ON auth.UserRoles(UserId);
CREATE INDEX IX_UserRole_RoleId ON auth.UserRoles(RoleId);
CREATE INDEX IX_AuditLog_Timestamp ON auth.AuditLogs(Timestamp DESC);
CREATE INDEX IX_AuditLog_UserId ON auth.AuditLogs(UserId);
```

## Foreign Key Relationships

### Within Module
```sql
-- Use database foreign keys
ALTER TABLE appraisal.GalleryPhotos
    ADD CONSTRAINT FK_GalleryPhoto_Gallery
    FOREIGN KEY (GalleryId) REFERENCES appraisal.AppraisalGalleries(Id)
    ON DELETE CASCADE;
```

### Cross Module
```sql
-- Store only ID, no FK constraint (eventual consistency)
-- Request.Id stored in Appraisal.RequestId
-- Appraisal.Id stored in Collateral.AppraisalId
-- No database FK, verified in application code
```

## Cardinality Notation

```
||--||  : One to One
||--o{  : One to Many
}o--||  : Many to One
}o--o{  : Many to Many
||--o|  : One to Zero or One
```

## Best Practices

### 1. Entity Naming
- Use singular nouns: `Request`, `Appraisal`, `Photo`
- PascalCase for multi-word: `AppraisalGallery`, `PropertyPhotoMapping`

### 2. Primary Keys
- Always GUID (UNIQUEIDENTIFIER)
- Always named `Id`
- Use NEWSEQUENTIALID() for SQL Server performance

### 3. Foreign Keys
- Always named `{Entity}Id`: `AppraisalId`, `UserId`, `DocumentId`
- Include in indexes for join performance

### 4. Business Keys
- Unique constraint on business identifiers
- Indexed for lookup performance
- Human-readable format: `REQ-2025-0001`, `APP-2025-0123`

### 5. Audit Fields
- Standard fields on all entities: `CreatedOn`, `CreatedBy`, `UpdatedOn`, `UpdatedBy`
- Implement via EF Core SaveChanges interceptor

### 6. Soft Delete
- Optional `IsDeleted`, `DeletedOn`, `DeletedBy` fields
- Use global query filter in EF Core
- Archive to separate table for historical data

### 7. Optimistic Concurrency
- Use `RowVersion` (timestamp) column
- Handle concurrency exceptions gracefully

---

**Next**: See [11-design-decisions.md](11-design-decisions.md) for detailed rationale behind design choices.
