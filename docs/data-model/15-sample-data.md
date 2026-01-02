# Sample Data - Complete Dataset

## Overview

This document provides comprehensive sample data for all modules in the Collateral Appraisal System. The data represents realistic scenarios and demonstrates the relationships between entities.

## Table of Contents

1. [Request Management Module](#request-management-module)
2. [Appraisal Module](#appraisal-module)
3. [Property Details](#property-details)
4. [Photo Gallery & Media](#photo-gallery--media)
5. [Collateral Module](#collateral-module)
6. [Document Module](#document-module)
7. [Authentication & Authorization Module](#authentication--authorization-module)

---

## Request Management Module

### Scenario 1: Land and Building - Single House

#### Request

```sql
INSERT INTO request.Requests (
    Id, RequestNumber, RequestDate, RequestType, LoanApplicationNumber, LoanAmount, LoanCurrency,
    AppraisalPurpose, Priority, Status, RequestedBy, RequestedByName, RequestedByEmail, RequestedByPhone,
    BranchCode, BranchName, SourceSystem, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'REQ-2025-00001',
    '2025-01-15 09:30:00',
    'NewLoan',
    'LOAN-2025-12345',
    5000000.00,
    'THB',
    'Collateral',
    'Normal',
    'Submitted',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',  -- RM User ID
    'Somchai Jaidee',
    'somchai.j@bank.com',
    '081-234-5678',
    'BKK001',
    'Bangkok Main Branch',
    'Manual',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Customers (Multiple customers - simplified)

```sql
-- Customer 1: Nattapong Sawasdee
INSERT INTO request.RequestCustomers (
    Id, RequestId,
    FirstName, LastName, ContactNumber,
    DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-2222-1111-1111-111111111111',
    '11111111-1111-1111-1111-111111111111',
    'Nattapong',
    'Sawasdee',
    '089-123-4567',
    1,
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);

-- Customer 2: Siriwan Sawasdee (Co-borrower/Wife)
INSERT INTO request.RequestCustomers (
    Id, RequestId,
    FirstName, LastName, ContactNumber,
    DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-2222-2222-1111-111111111111',
    '11111111-1111-1111-1111-111111111111',
    'Siriwan',
    'Sawasdee',
    '089-765-4321',
    2,
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Property Types (Simplified)

```sql
-- Property: Land and Building - Single House
INSERT INTO request.RequestPropertyTypes (
    Id, RequestId,
    PropertyType, PropertySubType,
    DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-3333-1111-1111-111111111111',
    '11111111-1111-1111-1111-111111111111',
    'LandAndBuilding',
    'SingleHouse',
    1,
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Title Deed Information

```sql
INSERT INTO request.TitleDeedInfo (
    Id, RequestId, RequestPropertyTypeId,
    TitleDeedNumber, DeedType, IssueDate, IssueOffice,
    SurveyNumber, SurveySheet, ParcelNumber,
    SubDistrict, District, Province,
    AreaRai, AreaNgan, AreaSquareWa, TotalSquareMeters,
    OwnerName, OwnerIdCard, OwnershipType, OwnershipPercentage,
    HasMortgage, HasLease, HasEasement,
    LandUseType, ZoningDesignation,
    DisplayOrder, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-4444-1111-1111-111111111111',
    '11111111-1111-1111-1111-111111111111',
    '11111111-3333-1111-1111-111111111111',
    '12345',
    'Chanote',
    '2015-03-20',
    'Land Office - Samut Prakan',
    '678',
    'SAMUT-2015',
    '45',
    'Bang Phli Yai',
    'Bang Phli',
    'Samut Prakan',
    0.00,
    0.00,
    75.00,  -- Square Wa
    300.00,  -- Square Meters (75 sq.wa * 4 = 300 sq.m)
    'นายณัฐพงษ์ สวัสดี และ นางสิริวรรณ สวัสดี',
    '1-1234-56789-12-3',
    'CoOwnership',
    50.00,
    0,
    0,
    0,
    'Residential',
    'Low-density residential zone',
    1,
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 09:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Status History

```sql
INSERT INTO request.RequestStatusHistory (
    Id, RequestId, FromStatus, ToStatus, StatusChangedAt, StatusChangedBy, StatusChangedByName, ChangeReason, CreatedOn
)
VALUES
    ('11111111-5555-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', NULL, 'Draft', '2025-01-15 09:30:00', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Somchai Jaidee', 'Request created', '2025-01-15 09:30:00'),
    ('11111111-5555-2222-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'Draft', 'Submitted', '2025-01-15 14:00:00', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Somchai Jaidee', 'Request submitted after attaching all documents', '2025-01-15 14:00:00');
```

---

### Scenario 2: Condo Unit

#### Request

```sql
INSERT INTO request.Requests (
    Id, RequestNumber, RequestDate, RequestType, LoanApplicationNumber, LoanAmount, LoanCurrency,
    AppraisalPurpose, Priority, Status, RequestedBy, RequestedByName, RequestedByEmail,
    BranchCode, BranchName, SourceSystem, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '22222222-1111-1111-1111-111111111111',
    'REQ-2025-00002',
    '2025-01-16 10:15:00',
    'Refinance',
    'LOAN-2025-12346',
    3500000.00,
    'THB',
    'Collateral',
    'High',
    'Submitted',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'Somchai Jaidee',
    'somchai.j@bank.com',
    'BKK001',
    'Bangkok Main Branch',
    'Manual',
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Customer

```sql
INSERT INTO request.RequestCustomers (
    Id, RequestId,
    FirstName, LastName, ContactNumber,
    DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '22222222-2222-1111-1111-111111111111',
    '22222222-1111-1111-1111-111111111111',
    'Chanida',
    'Wongsawat',
    '092-345-6789',
    1,
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Property Type (Condo)

```sql
INSERT INTO request.RequestPropertyTypes (
    Id, RequestId,
    PropertyType, PropertySubType,
    DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '22222222-3333-1111-1111-111111111111',
    '22222222-1111-1111-1111-111111111111',
    'Condo',
    'Apartment',
    1,
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-16 10:15:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

---

### Scenario 3: Corporate Borrower - Commercial Building

#### Request

```sql
INSERT INTO request.Requests (
    Id, RequestNumber, RequestDate, RequestType, LoanApplicationNumber, LoanAmount, LoanCurrency,
    AppraisalPurpose, SpecialInstructions, Priority, Status,
    RequestedBy, RequestedByName, RequestedByEmail,
    BranchCode, BranchName, SourceSystem, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '33333333-1111-1111-1111-111111111111',
    'REQ-2025-00003',
    '2025-01-17 08:00:00',
    'NewLoan',
    'LOAN-2025-12347',
    25000000.00,
    'THB',
    'Collateral',
    'Commercial property with tenants. Please coordinate site visit with property manager.',
    'Urgent',
    'Submitted',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'Somchai Jaidee',
    'somchai.j@bank.com',
    'BKK001',
    'Bangkok Main Branch',
    'Manual',
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Customer (Corporate)

```sql
INSERT INTO request.RequestCustomers (
    Id, RequestId, CustomerType, CustomerCategory,
    CompanyName, CompanyRegistrationNumber, TaxId, CompanyType,
    Email, OfficePhone, FaxNumber,
    RegisteredAddressLine1, RegisteredAddressLine2, RegisteredSubDistrict, RegisteredDistrict, RegisteredProvince, RegisteredPostalCode,
    IsPrimaryContact, DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '33333333-2222-1111-1111-111111111111',
    '33333333-1111-1111-1111-111111111111',
    'PrimaryBorrower',
    'Corporate',
    'Bangkok Commerce Co., Ltd.',
    '0105558901234',
    '0105558901234',
    'Limited',
    'contact@bangkokcommerce.com',
    '02-123-4567',
    '02-123-4568',
    '99/1 Sathorn Road',
    'Sathorn Tower, 20th Floor',
    'Silom',
    'Bang Rak',
    'Bangkok',
    '10500',
    1,
    1,
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Property Type (Commercial Building)

```sql
INSERT INTO request.RequestPropertyTypes (
    Id, RequestId, PropertyType, PropertySubType, PropertyDescription,
    TitleDeedNumber, LandDeedType, SurveyNumber, SubDistrict, District, Province,
    EstimatedLandArea, EstimatedBuildingArea, AreaUnit,
    EstimatedValue, EstimatedValueCurrency,
    IsMainProperty, CurrentUsage, Condition, SpecialNotes,
    DisplayOrder, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '33333333-3333-1111-1111-111111111111',
    '33333333-1111-1111-1111-111111111111',
    'Building',
    'Commercial',
    'Four-story commercial building',
    '54321',
    'Chanote',
    '999',
    'Silom',
    'Bang Rak',
    'Bangkok',
    500.00,
    1200.00,
    'SquareMeter',
    30000000.00,
    'THB',
    1,
    'Commercial',
    'Good',
    'Currently rented to 5 tenants. Occupancy rate: 90%',
    1,
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-17 08:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

---

### Scenario 4: Vehicle Appraisal

#### Request

```sql
INSERT INTO request.Requests (
    Id, RequestNumber, RequestDate, RequestType, LoanApplicationNumber, LoanAmount, LoanCurrency,
    AppraisalPurpose, Priority, Status, RequestedBy, RequestedByName, RequestedByEmail,
    BranchCode, BranchName, SourceSystem, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '44444444-1111-1111-1111-111111111111',
    'REQ-2025-00004',
    '2025-01-18 11:00:00',
    'NewLoan',
    'LOAN-2025-12348',
    800000.00,
    'THB',
    'Collateral',
    'Normal',
    'Submitted',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'Somchai Jaidee',
    'somchai.j@bank.com',
    'BKK001',
    'Bangkok Main Branch',
    'Manual',
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Customer

```sql
INSERT INTO request.RequestCustomers (
    Id, RequestId, CustomerType, CustomerCategory,
    TitleName, FirstName, LastName, NationalId, DateOfBirth, Nationality,
    Email, MobilePhone,
    CurrentAddressLine1, CurrentSubDistrict, CurrentDistrict, CurrentProvince, CurrentPostalCode,
    IsPrimaryContact, IsPropertyOwner, DisplayOrder,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '44444444-2222-1111-1111-111111111111',
    '44444444-1111-1111-1111-111111111111',
    'PrimaryBorrower',
    'Individual',
    'Mr.',
    'Somkiat',
    'Pattana',
    '1-3456-78901-34-5',
    '1988-03-25',
    'Thai',
    'somkiat.p@email.com',
    '091-234-5678',
    '45/12 Phaholyothin Road',
    'Chatuchak',
    'Chatuchak',
    'Bangkok',
    '10900',
    1,
    1,
    1,
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Request Property Type (Vehicle)

```sql
INSERT INTO request.RequestPropertyTypes (
    Id, RequestId, PropertyType, PropertySubType, PropertyDescription,
    VehicleType, Brand, Model, Year, LicensePlate, ChassisNumber,
    EstimatedValue, EstimatedValueCurrency,
    IsMainProperty, Condition,
    DisplayOrder, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '44444444-3333-1111-1111-111111111111',
    '44444444-1111-1111-1111-111111111111',
    'Vehicle',
    'Car',
    'Sedan, 4-door',
    'Car',
    'Toyota',
    'Camry 2.5 Hybrid',
    2020,
    'กจ-1234 กรุงเทพฯ',
    'JTNB11HK3L0012345',
    950000.00,
    'THB',
    1,
    'Good',
    1,
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-18 11:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

---

## Appraisal Module

### Scenario 1: Land and Building Appraisal (from REQ-2025-00001)

#### Appraisal

```sql
INSERT INTO appraisal.Appraisals (
    Id, AppraisalNumber, RequestId, RequestNumber, AppraisalType, Status,
    DueDate, Priority, AssignedTo, AssignedToName, AssignedAt, AssignedBy,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-1111-1111-111111111111',
    'APR-2025-00001',
    '11111111-1111-1111-1111-111111111111',
    'REQ-2025-00001',
    'Property',
    'FieldSurvey',
    '2025-01-25 17:00:00',
    'Normal',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',  -- Appraiser User ID
    'Apinya Suwan',
    '2025-01-15 15:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 14:30:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 15:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

#### Appraisal Assignment

```sql
INSERT INTO appraisal.AppraisalAssignments (
    Id, AppraisalId, AssignmentType, AssignmentStatus,
    AssignedTo, AssignedToName, AssignedBy, AssignedByName, AssignedAt,
    AcceptedAt, RejectionReason, Notes,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-2222-1111-111111111111',
    '11111111-aaaa-1111-1111-111111111111',
    'Initial',
    'Accepted',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'Somchai Jaidee',
    '2025-01-15 15:00:00',
    '2025-01-15 15:30:00',
    NULL,
    'Auto-assigned based on workload and location',
    '2025-01-15 15:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 15:30:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

#### Field Survey

```sql
INSERT INTO appraisal.FieldSurveys (
    Id, AppraisalId, SurveyNumber, SurveyType, SurveyStatus,
    ScheduledDate, ScheduledStartTime, ScheduledEndTime,
    ActualStartTime, ActualEndTime,
    SurveyLocation, Latitude, Longitude,
    WeatherCondition, Accessibility,
    SurveyedBy, SurveyedByName,
    Notes, SurveyorRemarks,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-3333-1111-111111111111',
    '11111111-aaaa-1111-1111-111111111111',
    'FS-2025-00001',
    'PropertyInspection',
    'Completed',
    '2025-01-18',
    '2025-01-18 09:00:00',
    '2025-01-18 11:00:00',
    '2025-01-18 09:15:00',
    '2025-01-18 11:45:00',
    '123/45 Sukhumvit Soi 23, Bang Phli, Samut Prakan',
    13.599250,
    100.658000,
    'Sunny',
    'Good',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    'Owners were present and cooperative. All areas accessible.',
    'Property in good condition. Minor repairs needed on external paint.',
    '2025-01-18 09:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-18 12:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

---

## Property Details

### Building Appraisal Detail (for APR-2025-00001)

```sql
INSERT INTO appraisal.BuildingAppraisalDetails (
    Id, AppraisalId, BuildingType, BuildingSubType, NumberOfStories,

    -- Structure
    StructureType, StructureCondition,
    FoundationType, FoundationCondition,

    -- Roof
    RoofType, RoofMaterial, RoofCondition,

    -- Walls
    WallMaterial, WallFinish, WallCondition,

    -- Floor
    FloorMaterial, FloorFinish, FloorCondition,

    -- Decoration
    IsDecorated, DecorationLevel, DecorationCondition, DecorationStyle,

    -- Rooms
    NumberOfBedrooms, NumberOfBathrooms, NumberOfLivingRooms, NumberOfKitchens,

    -- Kitchen
    KitchenType, KitchenSize, HasBuiltInCabinets, CounterTopMaterial,
    HasHood, HasStove, KitchenCondition,

    -- Bathrooms
    BathroomType, BathroomSize, BathroomFixtures, HasWaterHeater,
    BathroomCondition,

    -- Electrical
    ElectricalSystemType, ElectricalCapacity, NumberOfElectricalOutlets,
    HasCircuitBreaker, ElectricalCondition,

    -- Plumbing
    WaterSupplyType, HasWaterPressurePump, PlumbingMaterial,
    DrainageType, PlumbingCondition,

    -- Air Conditioning
    HasAirConditioner, NumberOfACUnits, ACType,

    -- Security
    HasSecuritySystem, SecurityFeatures,

    -- Parking
    ParkingType, NumberOfParkingSpaces,

    -- Overall
    YearBuilt, EffectiveAge, EstimatedRemainingLife,
    OverallCondition, MaintenanceLevel,

    -- Defects
    HasStructuralDefects, StructuralDefectsDescription,
    HasWaterDamage, WaterDamageDescription,
    HasCracks, CracksDescription,
    OtherDefects, EstimatedRepairCost,

    -- Areas
    TotalBuildingArea, UsableArea, TotalFloorArea,

    -- Remarks
    AppraisalRemarks, InternalNotes,

    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-4444-1111-111111111111',
    '11111111-aaaa-1111-1111-111111111111',
    'SingleHouse',
    'Detached',
    2,

    -- Structure
    'ReinforcedConcrete',
    'Good',
    'PiledFoundation',
    'Good',

    -- Roof
    'Gable',
    'ClayTile',
    'Good',

    -- Walls
    'Brick',
    'PaintedPlaster',
    'Good',

    -- Floor
    'Tile',
    'CeramicTile',
    'Good',

    -- Decoration
    1,
    'Moderate',
    'Good',
    'Contemporary',

    -- Rooms
    4,
    3,
    2,
    1,

    -- Kitchen
    'BuiltIn',
    'Large',
    1,
    'Granite',
    1,
    1,
    'Good',

    -- Bathrooms
    'Standard',
    'Medium',
    'Complete',
    1,
    'Good',

    -- Electrical
    'ThreePhase',
    '50A',
    35,
    1,
    'Good',

    -- Plumbing
    'Municipal',
    1,
    'PVC',
    'SewerSystem',
    'Good',

    -- Air Conditioning
    1,
    4,
    'SplitType',

    -- Security
    1,
    'CCTV, Motion sensors, Smart lock',

    -- Parking
    'Covered',
    2,

    -- Overall
    2018,
    7,
    43,
    'Good',
    'WellMaintained',

    -- Defects
    0,
    NULL,
    0,
    NULL,
    0,
    NULL,
    'Minor paint peeling on external walls',
    15000.00,

    -- Areas
    250.00,
    220.00,
    250.00,

    -- Remarks
    'Well-maintained two-story single house. Modern design with good quality materials. Located in a quiet residential area.',
    'Property visited on 2025-01-18. Owners cooperative. All areas inspected.',

    '2025-01-19 10:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 10:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

### Land Appraisal Detail (for APR-2025-00001)

```sql
INSERT INTO appraisal.LandAppraisalDetails (
    Id, AppraisalId,
    Topography, LandShape, SoilType,
    FrontageWidth, Depth, TotalArea, AreaUnit,

    -- Utilities
    HasElectricity, ElectricityType, ElectricityCapacity,
    HasWater, WaterSupplyType,
    HasSewerage, SewerageType,
    HasTelephone, HasInternet,

    -- Access
    RoadAccess, RoadType, RoadWidth, RoadCondition,
    AccessFromPublicRoad, DistanceFromPublicRoad,

    -- Location
    LocationType, Neighborhood, ProximityToAmenities,

    -- Zoning
    Zoning, ZoningDesignation, BuildingCoverage, FloorAreaRatio,
    MaxBuildingHeight, LandUseRestrictions,

    -- Physical Characteristics
    SlopePercentage, DrainageCondition, FloodRisk,
    SoilBearingCapacity, EnvironmentalIssues,

    -- View & Orientation
    ViewQuality, Orientation,

    -- Development Potential
    DevelopmentPotential, DevelopmentConstraints,

    -- Remarks
    AppraisalRemarks, InternalNotes,

    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-5555-1111-111111111111',
    '11111111-aaaa-1111-1111-111111111111',
    'Flat',
    'Regular',
    'Clay',
    15.00,
    20.00,
    300.00,
    'SquareMeter',

    -- Utilities
    1,
    'ThreePhase',
    '50A',
    1,
    'Municipal',
    1,
    'PublicSewer',
    1,
    1,

    -- Access
    'Direct',
    'ConcreteRoad',
    6.00,
    'Good',
    1,
    0,

    -- Location
    'Residential',
    'Quiet residential area with good neighbors',
    'Close to schools, markets, and shopping centers',

    -- Zoning
    'Residential',
    'Low-density residential',
    60.00,
    2.00,
    12.00,
    'Residential use only',

    -- Physical
    2.00,
    'Good',
    'Low',
    'Medium',
    'None',

    -- View
    'Moderate',
    'North',

    -- Development
    'Moderate',
    'Limited by zoning regulations',

    -- Remarks
    'Regular-shaped plot with good access and utilities. Suitable for residential development.',
    'Land is already developed with existing building.',

    '2025-01-19 10:30:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 10:30:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

---

## Photo Gallery & Media

### Appraisal Gallery

```sql
INSERT INTO appraisal.AppraisalGallery (
    Id, AppraisalId, GalleryName, GalleryType, Description,
    TotalPhotos, TotalVideos, TotalAudioNotes,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-6666-1111-111111111111',
    '11111111-aaaa-1111-1111-111111111111',
    'Field Survey - Single House',
    'FieldSurvey',
    'Photos and videos from site visit on 2025-01-18',
    25,
    2,
    3,
    '2025-01-18 09:15:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-18 12:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

### Upload Session

```sql
INSERT INTO appraisal.UploadSessions (
    Id, GalleryId, SessionName, UploadedBy, UploadedByName,
    DeviceInfo, DeviceType, DeviceLocation,
    SessionStartedAt, SessionEndedAt, SessionDuration,
    TotalPhotos, TotalVideos, TotalAudioNotes,
    TotalSizeBytes, UploadStatus,
    CreatedOn
)
VALUES (
    '11111111-aaaa-7777-1111-111111111111',
    '11111111-aaaa-6666-1111-111111111111',
    'Site Visit - Morning Session',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    'iPhone 14 Pro, iOS 17.2',
    'Mobile',
    'On-site at property location',
    '2025-01-18 09:20:00',
    '2025-01-18 11:30:00',
    130,
    18,
    1,
    2,
    156789450,
    'Completed',
    '2025-01-18 09:20:00'
);
```

### Gallery Photos (Sample)

```sql
-- Photo 1: Front exterior
INSERT INTO appraisal.GalleryPhotos (
    Id, GalleryId, DocumentId, PhotoNumber, DisplayOrder,
    PhotoType, PhotoCategory, Caption,
    Latitude, Longitude, Altitude, Compass,
    CapturedAt, CapturedBy, CapturedByName,
    UploadSessionId,
    ThumbnailUrl, FullSizeUrl, OriginalFileName, FileSizeBytes,
    ImageWidth, ImageHeight, ImageFormat, IsPortrait,
    IsSelected, IsUsedInReport, IsFeaturedPhoto,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-8888-0001-111111111111',
    '11111111-aaaa-6666-1111-111111111111',
    'dddddddd-dddd-0001-dddd-dddddddddddd',  -- Document ID
    1,
    1,
    'Exterior',
    'Front',
    'Front view of the house',
    13.599250,
    100.658000,
    5.0,
    'N',
    '2025-01-18 09:25:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    '11111111-aaaa-7777-1111-111111111111',
    'https://storage.example.com/thumbnails/photo_001_thumb.jpg',
    'https://storage.example.com/photos/photo_001_full.jpg',
    'IMG_0001.jpg',
    4523000,
    4032,
    3024,
    'JPEG',
    0,
    1,
    1,
    1,
    '2025-01-18 09:25:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);

-- Photo 2: Living room
INSERT INTO appraisal.GalleryPhotos (
    Id, GalleryId, DocumentId, PhotoNumber, DisplayOrder,
    PhotoType, PhotoCategory, Caption,
    Latitude, Longitude,
    CapturedAt, CapturedBy, CapturedByName,
    UploadSessionId,
    ThumbnailUrl, FullSizeUrl, OriginalFileName, FileSizeBytes,
    ImageWidth, ImageHeight, ImageFormat, IsPortrait,
    IsSelected, IsUsedInReport,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-8888-0002-111111111111',
    '11111111-aaaa-6666-1111-111111111111',
    'dddddddd-dddd-0002-dddd-dddddddddddd',
    2,
    2,
    'Interior',
    'LivingRoom',
    'Main living room on ground floor',
    13.599250,
    100.658000,
    '2025-01-18 09:35:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    '11111111-aaaa-7777-1111-111111111111',
    'https://storage.example.com/thumbnails/photo_002_thumb.jpg',
    'https://storage.example.com/photos/photo_002_full.jpg',
    'IMG_0002.jpg',
    3876000,
    4032,
    3024,
    'JPEG',
    0,
    1,
    1,
    '2025-01-18 09:35:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:05:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);

-- Photo 3: Kitchen
INSERT INTO appraisal.GalleryPhotos (
    Id, GalleryId, DocumentId, PhotoNumber, DisplayOrder,
    PhotoType, PhotoCategory, Caption,
    Latitude, Longitude,
    CapturedAt, CapturedBy, CapturedByName,
    UploadSessionId,
    ThumbnailUrl, FullSizeUrl, OriginalFileName, FileSizeBytes,
    ImageWidth, ImageHeight, ImageFormat,
    IsSelected, IsUsedInReport,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-8888-0003-111111111111',
    '11111111-aaaa-6666-1111-111111111111',
    'dddddddd-dddd-0003-dddd-dddddddddddd',
    3,
    3,
    'Interior',
    'Kitchen',
    'Built-in kitchen with granite countertop',
    13.599250,
    100.658000,
    '2025-01-18 09:40:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    '11111111-aaaa-7777-1111-111111111111',
    'https://storage.example.com/thumbnails/photo_003_thumb.jpg',
    'https://storage.example.com/photos/photo_003_full.jpg',
    'IMG_0003.jpg',
    4100000,
    4032,
    3024,
    'JPEG',
    1,
    1,
    '2025-01-18 09:40:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:10:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

### Property Photo Mapping

```sql
-- Map front photo to Building exterior
INSERT INTO appraisal.PropertyPhotoMappings (
    Id, PhotoId, PropertyDetailType, PropertyDetailId,
    PhotoPurpose, SectionReference, SectionDescription,
    Notes, LinkedBy, LinkedAt,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-9999-0001-111111111111',
    '11111111-aaaa-8888-0001-111111111111',
    'Building',
    '11111111-aaaa-4444-1111-111111111111',
    'Overview',
    'Exterior_Front',
    'Front elevation of the building',
    'Featured photo for report cover',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:00:00',
    '2025-01-19 14:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:00:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);

-- Map living room photo to Building interior
INSERT INTO appraisal.PropertyPhotoMappings (
    Id, PhotoId, PropertyDetailType, PropertyDetailId,
    PhotoPurpose, SectionReference, SectionDescription,
    LinkedBy, LinkedAt,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-9999-0002-111111111111',
    '11111111-aaaa-8888-0002-111111111111',
    'Building',
    '11111111-aaaa-4444-1111-111111111111',
    'Detail',
    'Interior_LivingRoom',
    'Main living room showing floor and wall finishes',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:05:00',
    '2025-01-19 14:05:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:05:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);

-- Map kitchen photo to Building kitchen section
INSERT INTO appraisal.PropertyPhotoMappings (
    Id, PhotoId, PropertyDetailType, PropertyDetailId,
    PhotoPurpose, SectionReference, SectionDescription,
    Notes, LinkedBy, LinkedAt,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-9999-0003-111111111111',
    '11111111-aaaa-8888-0003-111111111111',
    'Building',
    '11111111-aaaa-4444-1111-111111111111',
    'Detail',
    'Kitchen',
    'Built-in kitchen with fixtures and fittings',
    'Shows granite countertop and modern appliances',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:10:00',
    '2025-01-19 14:10:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:10:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

### Photo Annotations

```sql
INSERT INTO appraisal.PhotoAnnotations (
    Id, PhotoId, AnnotationType, AnnotationData,
    Description, AnnotatedBy, AnnotatedByName, AnnotatedAt,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-aaaa-aaaa-0001-111111111111',
    '11111111-aaaa-8888-0001-111111111111',
    'Arrow',
    '{"x1": 120, "y1": 450, "x2": 180, "y2": 500, "color": "red", "thickness": 3}',
    'Minor paint peeling on external wall',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    '2025-01-19 14:15:00',
    '2025-01-19 14:15:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-19 14:15:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

---

## Collateral Module

### Collateral (Created after Appraisal Approval)

```sql
INSERT INTO collateral.Collaterals (
    Id, CollateralNumber, CollateralType, CollateralStatus,
    RequestId, RequestNumber, AppraisalId, AppraisalNumber,
    MarketValue, AppraisedValue, ForcedSaleValue, Currency,
    ValuationDate, NextRevaluationDate,
    OwnerName, OwnerType,
    IsActive, ActivatedAt,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-cccc-1111-1111-111111111111',
    'COL-2025-00001',
    'Building',
    'Active',
    '11111111-1111-1111-1111-111111111111',
    'REQ-2025-00001',
    '11111111-aaaa-1111-1111-111111111111',
    'APR-2025-00001',
    6500000.00,
    6000000.00,
    4800000.00,
    'THB',
    '2025-01-20',
    '2026-01-20',
    'นายณัฐพงษ์ สวัสดี และ นางสิริวรรณ สวัสดี',
    'Individual',
    1,
    '2025-01-21 10:00:00',
    '2025-01-21 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-21 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

### Building Collateral

```sql
INSERT INTO collateral.BuildingCollaterals (
    Id, CollateralId,
    BuildingType, NumberOfStories, YearBuilt, EffectiveAge,
    TotalBuildingArea, UsableArea,
    StructureType, RoofType, WallMaterial, FloorMaterial,
    OverallCondition, MaintenanceLevel,

    -- Location
    AddressLine1, AddressLine2, SubDistrict, District, Province, PostalCode,

    -- Legal
    TitleDeedNumber, DeedType, SurveyNumber,
    LandAreaRai, LandAreaNgan, LandAreaSquareWa, TotalLandSquareMeters,

    -- Ownership
    OwnershipType, OwnershipPercentage,
    HasMortgage, HasLease, HasEasement,

    -- Valuation
    LandValue, BuildingValue, TotalValue,

    Notes,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    '11111111-cccc-2222-1111-111111111111',
    '11111111-cccc-1111-1111-111111111111',
    'SingleHouse',
    2,
    2018,
    7,
    250.00,
    220.00,
    'ReinforcedConcrete',
    'ClayTile',
    'Brick',
    'CeramicTile',
    'Good',
    'WellMaintained',

    -- Location
    '123/45 Sukhumvit Soi 23',
    NULL,
    'Bang Phli Yai',
    'Bang Phli',
    'Samut Prakan',
    '10540',

    -- Legal
    '12345',
    'Chanote',
    '678',
    0.00,
    0.00,
    75.00,
    300.00,

    -- Ownership
    'CoOwnership',
    50.00,
    0,
    0,
    0,

    -- Valuation
    2500000.00,
    3500000.00,
    6000000.00,

    'Well-maintained residential property in good location',
    '2025-01-21 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-21 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);
```

---

## Document Module

### Documents (Sample)

```sql
-- Document 1: Title Deed Copy
INSERT INTO document.Documents (
    Id, DocumentNumber, DocumentType, DocumentCategory,
    FileName, FileExtension, FileSizeBytes, MimeType,
    StorageProvider, StoragePath, StorageUrl,
    UploadedBy, UploadedByName, UploadedAt,
    IsEncrypted, EncryptionAlgorithm,
    CurrentVersion, TotalVersions,
    IsActive,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    'dddddddd-0001-dddd-dddd-dddddddddddd',
    'DOC-2025-00001',
    'TitleDeed',
    'Legal',
    'title_deed_12345.pdf',
    'pdf',
    523456,
    'application/pdf',
    'AzureBlobStorage',
    '/documents/2025/01/title_deed_12345.pdf',
    'https://storage.azure.com/documents/2025/01/title_deed_12345.pdf',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'Somchai Jaidee',
    '2025-01-15 10:00:00',
    1,
    'AES256',
    1,
    1,
    1,
    '2025-01-15 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    '2025-01-15 10:00:00',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
);

-- Document 2: Photo (front view)
INSERT INTO document.Documents (
    Id, DocumentNumber, DocumentType, DocumentCategory,
    FileName, FileExtension, FileSizeBytes, MimeType,
    StorageProvider, StoragePath, StorageUrl,
    UploadedBy, UploadedByName, UploadedAt,
    CurrentVersion, TotalVersions,
    IsActive,
    CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
)
VALUES (
    'dddddddd-dddd-0001-dddd-dddddddddddd',
    'DOC-2025-00101',
    'Photo',
    'AppraisalMedia',
    'photo_001_full.jpg',
    'jpg',
    4523000,
    'image/jpeg',
    'AzureBlobStorage',
    '/photos/2025/01/photo_001_full.jpg',
    'https://storage.azure.com/photos/2025/01/photo_001_full.jpg',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'Apinya Suwan',
    '2025-01-18 09:25:00',
    1,
    1,
    1,
    '2025-01-18 09:25:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    '2025-01-18 09:25:00',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
);
```

---

## Authentication & Authorization Module

### Users

```sql
-- User 1: RM (Relationship Manager)
INSERT INTO auth.Users (
    Id, Username, Email, EmailConfirmed,
    FirstName, LastName, DisplayName,
    PhoneNumber, PhoneNumberConfirmed,
    EmployeeId, Department, Position,
    BranchCode, BranchName,
    IsActive, IsSystemUser,
    CreatedOn, UpdatedOn
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    'somchai.j',
    'somchai.j@bank.com',
    1,
    'Somchai',
    'Jaidee',
    'Somchai Jaidee',
    '081-234-5678',
    1,
    'EMP-001',
    'Loan Department',
    'Relationship Manager',
    'BKK001',
    'Bangkok Main Branch',
    1,
    0,
    '2024-01-01 00:00:00',
    '2025-01-15 00:00:00'
);

-- User 2: Appraiser
INSERT INTO auth.Users (
    Id, Username, Email, EmailConfirmed,
    FirstName, LastName, DisplayName,
    PhoneNumber, PhoneNumberConfirmed,
    EmployeeId, Department, Position,
    IsActive, IsSystemUser,
    CreatedOn, UpdatedOn
)
VALUES (
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'apinya.s',
    'apinya.s@bank.com',
    1,
    'Apinya',
    'Suwan',
    'Apinya Suwan',
    '082-345-6789',
    1,
    'EMP-101',
    'Appraisal Department',
    'Senior Appraiser',
    1,
    0,
    '2024-01-01 00:00:00',
    '2025-01-15 00:00:00'
);
```

### Roles

```sql
INSERT INTO auth.Roles (Id, RoleName, RoleDescription, IsSystemRole, IsActive, CreatedOn, UpdatedOn)
VALUES
    ('role-0001-0001-0001-000000000001', 'RelationshipManager', 'Can create and manage appraisal requests', 1, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('role-0002-0002-0002-000000000002', 'Appraiser', 'Can conduct appraisals and create reports', 1, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00');
```

### User Roles

```sql
INSERT INTO auth.UserRoles (Id, UserId, RoleId, AssignedBy, AssignedAt, CreatedOn)
VALUES
    ('ur-0001-0001-0001-000000000001', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'role-0001-0001-0001-000000000001', 'admin-000-000-000-000000000000', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('ur-0002-0002-0002-000000000002', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'role-0002-0002-0002-000000000002', 'admin-000-000-000-000000000000', '2024-01-01 00:00:00', '2024-01-01 00:00:00');
```

---

## Summary

This sample dataset demonstrates:

✅ **Complete request lifecycle** from creation to collateral
✅ **Simplified customer structure** (FirstName, LastName, ContactNumber only)
✅ **Simplified property types** (PropertyType and PropertySubType only)
✅ **Multiple property type examples** (LandAndBuilding, Condo, Vehicle, Machinery)
✅ **Two-phase photo workflow** (site visit upload → office linking)
✅ **Property detail tables** with realistic building/land characteristics
✅ **Photo gallery system** with GPS metadata and annotations
✅ **Cross-module relationships** (Request → Appraisal → Collateral)
✅ **User roles and permissions** (RM, Appraiser)
✅ **Document management** with encryption and versioning
✅ **Audit trails** with status history

## Important Notes

**Simplified Request Module Tables (Updated Jan 2025)**:
- `RequestCustomers`: Now only includes FirstName, LastName, ContactNumber
- `RequestPropertyTypes`: Now only includes PropertyType and PropertySubType
- Detailed property information (title deed, measurements, etc.) stored in TitleDeedInfo table
- Full customer information managed in external CRM/LOS systems

## Usage

You can use these sample data scripts to:

1. **Seed development database** for testing
2. **Create demo data** for presentations
3. **Validate data model** design
4. **Test API endpoints** with realistic data
5. **Train users** on the system

---

**Next**: See [02-request-module.md](02-request-module.md) for detailed Request module documentation.
