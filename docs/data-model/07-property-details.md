# Property-Specific Appraisal Detail Tables

## Overview

The Appraisal module supports six different collateral types, each requiring specific fields for comprehensive property evaluation. Instead of using a generic table with many nullable columns, we use **separate tables for each property type** to ensure type safety, better performance, and clearer schema.

## Design Approach

### One-to-One Relationship

Each appraisal has **exactly one** property detail record based on the collateral type:

```
Appraisal (CollateralType = Land) → LandAppraisalDetail
Appraisal (CollateralType = Building) → BuildingAppraisalDetail
Appraisal (CollateralType = Condo) → CondoAppraisalDetail
Appraisal (CollateralType = Vehicle) → VehicleAppraisalDetail
Appraisal (CollateralType = Vessel) → VesselAppraisalDetail
Appraisal (CollateralType = Machinery) → MachineryAppraisalDetail
```

### Why Separate Tables?

| Aspect | Single Generic Table | Separate Tables (Our Choice) |
|--------|---------------------|------------------------------|
| **Type Safety** | ❌ No compile-time checks | ✅ Strong typing per property |
| **Null Columns** | ❌ Many nullable columns | ✅ All columns relevant |
| **Performance** | ❌ Sparse data, wasted space | ✅ Compact, optimized storage |
| **Validation** | ❌ Complex conditional logic | ✅ Simple field-level validation |
| **Maintainability** | ❌ Hard to modify | ✅ Easy to evolve independently |
| **Documentation** | ❌ Unclear which fields apply | ✅ Self-documenting structure |

## Common Patterns Across All Tables

### Standard Fields

All property detail tables include:

```sql
Id               UNIQUEIDENTIFIER PRIMARY KEY
AppraisalId      UNIQUEIDENTIFIER NOT NULL FOREIGN KEY → Appraisal.Id
LastUpdatedBy    UNIQUEIDENTIFIER NOT NULL FOREIGN KEY → User.Id
LastUpdatedOn    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
```

### Value Object Pattern

Complex related fields are grouped into Value Objects:

```csharp
// Instead of individual columns:
// RoofType, RoofMaterial, RoofCondition

// Use Value Object:
public class RoofStructure
{
    public RoofType Type { get; set; }
    public RoofMaterial Material { get; set; }
    public RoofCondition Condition { get; set; }
}

// In Entity Framework:
entity.OwnsOne(e => e.RoofStructure, roof =>
{
    roof.Property(r => r.Type).HasColumnName("RoofType");
    roof.Property(r => r.Material).HasColumnName("RoofMaterial");
    roof.Property(r => r.Condition).HasColumnName("RoofCondition");
});
```

### Condition Assessment Pattern

Most physical components follow this pattern:

```csharp
public enum Condition
{
    Excellent,  // Like new, no issues
    Good,       // Minor wear, fully functional
    Fair,       // Visible wear, may need attention
    Poor        // Significant issues, repairs needed
}
```

---

## 1. LandAppraisalDetail

### Purpose
Capture detailed information about land parcels including topography, utilities, access, and development potential.

### Schema

```sql
CREATE TABLE appraisal.LandAppraisalDetails
(
    Id                      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId             UNIQUEIDENTIFIER NOT NULL,

    -- Land Characteristics (Value Object)
    Topography              NVARCHAR(50) NOT NULL,      -- Flat, Sloped, Hilly, Mountainous
    LandShape               NVARCHAR(50) NOT NULL,      -- Regular, Irregular, Rectangular, Square, L-Shaped
    SoilType                NVARCHAR(50) NOT NULL,      -- Clay, Sandy, Loam, Rocky, Mixed
    DrainageCondition       NVARCHAR(50) NOT NULL,      -- Excellent, Good, Fair, Poor
    FloodRisk               NVARCHAR(50) NOT NULL,      -- None, Low, Medium, High

    -- Dimensions
    FrontageWidth           DECIMAL(18,2) NULL,         -- meters
    Depth                   DECIMAL(18,2) NULL,         -- meters
    CornerLot               BIT NOT NULL DEFAULT 0,

    -- Access Road (Value Object)
    HasAccessRoad           BIT NOT NULL DEFAULT 0,
    RoadType                NVARCHAR(50) NULL,          -- PublicRoad, PrivateRoad, Soi, Highway
    RoadWidth               DECIMAL(18,2) NULL,         -- meters
    RoadSurfaceType         NVARCHAR(50) NULL,          -- Asphalt, Concrete, Gravel, Dirt
    RoadCondition           NVARCHAR(50) NULL,          -- Excellent, Good, Fair, Poor

    -- Utilities (Value Object)
    HasElectricity          BIT NOT NULL DEFAULT 0,
    ElectricityType         NVARCHAR(50) NULL,          -- Public, Private
    HasWater                BIT NOT NULL DEFAULT 0,
    WaterSource             NVARCHAR(50) NULL,          -- PublicSupply, Well, Natural
    HasSewageSystem         BIT NOT NULL DEFAULT 0,
    SewageType              NVARCHAR(50) NULL,          -- PublicSewer, SepticTank, None
    HasTelephone            BIT NOT NULL DEFAULT 0,
    HasInternet             BIT NOT NULL DEFAULT 0,

    -- Land Use
    Zoning                  NVARCHAR(100) NULL,
    LandUse                 NVARCHAR(50) NOT NULL,      -- Residential, Commercial, Agricultural, Industrial, Mixed
    CurrentUse              NVARCHAR(200) NULL,
    DevelopmentPotential    NVARCHAR(50) NULL,          -- High, Medium, Low, None

    -- Legal Issues
    EnvironmentalIssues     NVARCHAR(MAX) NULL,
    Encroachment            BIT NOT NULL DEFAULT 0,
    EncroachmentDetails     NVARCHAR(MAX) NULL,
    EasementExists          BIT NOT NULL DEFAULT 0,
    EasementDetails         NVARCHAR(MAX) NULL,

    -- Additional Information
    Remarks                 NVARCHAR(MAX) NULL,
    LastUpdatedBy           UNIQUEIDENTIFIER NOT NULL,
    LastUpdatedOn           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_LandAppraisalDetail_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id),
    CONSTRAINT FK_LandAppraisalDetail_User FOREIGN KEY (LastUpdatedBy)
        REFERENCES auth.Users(Id)
);

CREATE INDEX IX_LandAppraisalDetail_AppraisalId ON appraisal.LandAppraisalDetails(AppraisalId);
```

### Key Fields Explanation

**Topography**: Critical for construction planning and drainage
- Flat: Ideal for development
- Sloped: May require terracing
- Hilly/Mountainous: Limited development potential

**Land Shape**: Affects usability and value
- Regular/Rectangular: Highest value, easy to develop
- Irregular/L-Shaped: May have unusable areas

**Utilities**: Essential for development
- Electricity and water availability significantly impact value
- Sewage system presence affects development costs

**Development Potential**: Forward-looking assessment
- High: Prime location, good zoning
- Low: Limited by regulations or physical constraints

### Usage Example

```csharp
var landDetail = new LandAppraisalDetail
{
    AppraisalId = appraisalId,
    LandCharacteristics = new LandCharacteristics
    {
        Topography = Topography.Flat,
        LandShape = LandShape.Rectangular,
        SoilType = SoilType.Loam,
        DrainageCondition = Condition.Good,
        FloodRisk = FloodRisk.Low
    },
    FrontageWidth = 20.5m,
    Depth = 30.0m,
    CornerLot = false,
    AccessRoad = new AccessRoad
    {
        HasAccessRoad = true,
        RoadType = RoadType.PublicRoad,
        RoadWidth = 8.0m,
        RoadSurfaceType = SurfaceType.Asphalt,
        RoadCondition = Condition.Good
    },
    Utilities = new Utilities
    {
        HasElectricity = true,
        ElectricityType = ElectricityType.Public,
        HasWater = true,
        WaterSource = WaterSource.PublicSupply,
        HasSewageSystem = true,
        SewageType = SewageType.PublicSewer
    },
    Zoning = "Residential Low Density",
    LandUse = LandUse.Residential,
    DevelopmentPotential = DevelopmentPotential.High
};
```

---

## 2. BuildingAppraisalDetail

### Purpose
Comprehensive assessment of building structures including construction quality, condition of components, fixtures, and overall state.

### Schema Overview

```sql
CREATE TABLE appraisal.BuildingAppraisalDetails
(
    Id                          UNIQUEIDENTIFIER PRIMARY KEY,
    AppraisalId                 UNIQUEIDENTIFIER NOT NULL,

    -- Building Type & Style
    BuildingType                NVARCHAR(50) NOT NULL,      -- House, Townhouse, Shophouse, Commercial, Warehouse, Factory
    BuildingStyle               NVARCHAR(50) NULL,          -- Modern, Contemporary, Traditional, Thai, Colonial

    -- Structure (Value Object)
    NumberOfFloors              INT NOT NULL,
    NumberOfBuildings           INT NOT NULL DEFAULT 1,
    TotalBuildingArea           DECIMAL(18,2) NOT NULL,     -- sqm
    UsableArea                  DECIMAL(18,2) NOT NULL,     -- sqm
    CeilingHeight               DECIMAL(18,2) NULL,         -- meters
    StructureType               NVARCHAR(50) NOT NULL,      -- ConcreteFrame, Steel, Wood, Brick, Mixed

    -- Age & Life
    ConstructionYear            INT NOT NULL,
    RenovationYear              INT NULL,
    EffectiveAge                INT NOT NULL,               -- Years (may differ from actual age)
    EstimatedRemainingLife      INT NULL,                   -- Years

    -- Foundation (Value Object)
    FoundationType              NVARCHAR(50) NOT NULL,      -- Piles, Raft, Strip, Pad
    FoundationCondition         NVARCHAR(50) NOT NULL,

    -- Roof Structure (Value Object)
    RoofType                    NVARCHAR(50) NOT NULL,      -- Gable, Hip, Flat, Shed, Mansard
    RoofMaterial                NVARCHAR(50) NOT NULL,      -- ConcreteSlab, Tiles, Metal, Shingles, Thatch
    RoofCondition               NVARCHAR(50) NOT NULL,

    -- Walls (Value Object)
    ExteriorWallMaterial        NVARCHAR(50) NOT NULL,      -- Concrete, Brick, Wood, Cement, Metal
    InteriorWallMaterial        NVARCHAR(50) NOT NULL,      -- Concrete, Brick, Drywall, Wood, Partition
    WallFinish                  NVARCHAR(50) NOT NULL,      -- Paint, Wallpaper, Tiles, Stone, Wood
    WallCondition               NVARCHAR(50) NOT NULL,

    -- Flooring (Value Object)
    FloorMaterial               NVARCHAR(50) NOT NULL,      -- Tiles, Marble, Granite, Parquet, Carpet, Concrete, Vinyl
    FloorFinish                 NVARCHAR(50) NULL,          -- Polished, Unpolished, Painted
    FloorCondition              NVARCHAR(50) NOT NULL,

    -- Doors & Windows (Value Object)
    DoorMaterial                NVARCHAR(50) NOT NULL,      -- Wood, Aluminum, UPVC, Steel, Glass
    WindowMaterial              NVARCHAR(50) NOT NULL,      -- Wood, Aluminum, UPVC, Steel
    WindowType                  NVARCHAR(50) NULL,          -- Sliding, Casement, Fixed, Awning
    DoorsWindowsCondition       NVARCHAR(50) NOT NULL,

    -- Decoration (Value Object)
    IsDecorated                 BIT NOT NULL DEFAULT 0,
    DecorationLevel             NVARCHAR(50) NULL,          -- Basic, Standard, Luxury, Premium
    DecorationCondition         NVARCHAR(50) NULL,
    DecorationYear              INT NULL,

    -- Electrical System (Value Object)
    WiringType                  NVARCHAR(50) NOT NULL,      -- Concealed, Surface, Mixed
    WiringCondition             NVARCHAR(50) NOT NULL,
    CircuitBreakerCapacity      INT NULL,                   -- Amperes
    HasGrounding                BIT NOT NULL DEFAULT 0,
    LastElectricalInspection    DATETIME2 NULL,

    -- Plumbing System (Value Object)
    PipeType                    NVARCHAR(50) NOT NULL,      -- PVC, Copper, Galvanized, PPR
    PipeCondition               NVARCHAR(50) NOT NULL,
    WaterPressure               NVARCHAR(50) NULL,          -- Adequate, Low, High
    HasHotWater                 BIT NOT NULL DEFAULT 0,

    -- Sanitary (Value Object)
    NumberOfBathrooms           INT NOT NULL,
    NumberOfToilets             INT NOT NULL,
    BathroomFixtures            NVARCHAR(50) NULL,          -- Basic, Standard, Premium
    SanitaryCondition           NVARCHAR(50) NOT NULL,

    -- Kitchen (Value Object)
    NumberOfKitchens            INT NOT NULL DEFAULT 1,
    KitchenType                 NVARCHAR(50) NULL,          -- European, Thai, Pantry, Combined
    BuiltInCabinets             BIT NOT NULL DEFAULT 0,
    CounterTopMaterial          NVARCHAR(50) NULL,          -- Granite, Marble, Laminate, Stainless
    KitchenCondition            NVARCHAR(50) NOT NULL,

    -- Bedrooms (Value Object)
    NumberOfBedrooms            INT NOT NULL,
    MasterBedroomArea           DECIMAL(18,2) NULL,         -- sqm
    HasBuiltInWardrobe          BIT NOT NULL DEFAULT 0,
    HasEnsuiteBathroom          BIT NOT NULL DEFAULT 0,

    -- Air Conditioning (Value Object)
    HasAirConditioner           BIT NOT NULL DEFAULT 0,
    ACType                      NVARCHAR(50) NULL,          -- Split, Central, VRV, Window
    NumberOfACUnits             INT NULL,
    TotalBTU                    INT NULL,
    ACCondition                 NVARCHAR(50) NULL,

    -- Overall Assessment
    OverallCondition            NVARCHAR(50) NOT NULL,      -- Excellent, Good, Fair, Poor, Dilapidated
    Defects                     NVARCHAR(MAX) NULL,
    RequiredRepairs             NVARCHAR(MAX) NULL,
    EstimatedRepairCost         DECIMAL(18,2) NULL,

    -- Metadata
    Remarks                     NVARCHAR(MAX) NULL,
    LastUpdatedBy               UNIQUEIDENTIFIER NOT NULL,
    LastUpdatedOn               DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_BuildingAppraisalDetail_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id)
);
```

### Key Sections

#### Structure Assessment
- **TotalBuildingArea vs UsableArea**: Important for valuation per sqm
- **EffectiveAge**: May differ from actual age due to maintenance/renovation
- **EstimatedRemainingLife**: Critical for long-term value assessment

#### Component Condition Assessment
Each major component (roof, walls, floor, etc.) has:
- Material type: Affects durability and maintenance
- Condition: Current state (Excellent → Poor)
- Some have additional specific fields

#### Systems Evaluation
- **Electrical**: Safety and capacity considerations
- **Plumbing**: Functionality and water pressure
- **Air Conditioning**: Comfort and energy efficiency

#### Decoration Level
- **Basic**: Minimal decoration, functional only
- **Standard**: Normal residential decoration
- **Luxury**: High-end finishes and fixtures
- **Premium**: Top-tier materials and design

### Photo Mapping Example

```csharp
// Link kitchen photo to multiple sections
new PropertyPhotoMapping
{
    PhotoId = kitchenPhotoId,
    PropertyDetailType = PropertyDetailType.Building,
    PropertyDetailId = buildingDetailId,
    PhotoPurpose = PhotoPurpose.Detail,
    SectionReference = "Kitchen"
},
new PropertyPhotoMapping
{
    PhotoId = kitchenPhotoId,
    PropertyDetailId = buildingDetailId,
    SectionReference = "Flooring"  // Same photo shows floor tiles
}
```

---

## 3. CondoAppraisalDetail

### Purpose
Specific assessment for condominium units including project information, unit configuration, common facilities, and juristic management.

### Key Differences from Building

Condos have unique characteristics:
- **Project-level information** (developer, total units, facilities)
- **Unit-specific details** (floor, unit number, direction)
- **Common area and facilities**
- **Juristic person management**
- **Monthly fees and sinking fund**
- **Parking space allocation**

### Schema Highlights

```sql
CREATE TABLE appraisal.CondoAppraisalDetails
(
    Id                          UNIQUEIDENTIFIER PRIMARY KEY,
    AppraisalId                 UNIQUEIDENTIFIER NOT NULL,

    -- Project Information (Value Object)
    ProjectName                 NVARCHAR(200) NOT NULL,
    Developer                   NVARCHAR(200) NULL,
    TotalUnits                  INT NULL,
    TotalFloors                 INT NULL,
    BuildYear                   INT NOT NULL,
    ProjectStatus               NVARCHAR(50) NOT NULL,      -- Completed, UnderConstruction, Planned

    -- Unit Information (Value Object)
    UnitNumber                  NVARCHAR(50) NOT NULL,
    Floor                       INT NOT NULL,
    BuildingNumber              NVARCHAR(50) NULL,          -- For multi-building projects
    UnitType                    NVARCHAR(50) NOT NULL,      -- Studio, OneBedroom, TwoBedroom, Penthouse
    UnitArea                    DECIMAL(18,2) NOT NULL,     -- sqm
    BalconyArea                 DECIMAL(18,2) NULL,         -- sqm
    CommonArea                  DECIMAL(18,2) NULL,         -- sqm (proportional)
    Direction                   NVARCHAR(50) NULL,          -- North, South, East, West, Northeast, etc.

    -- Room Configuration (Value Object)
    NumberOfBedrooms            INT NOT NULL,
    NumberOfBathrooms           INT NOT NULL,
    HasLivingRoom               BIT NOT NULL DEFAULT 1,
    HasDiningArea               BIT NOT NULL DEFAULT 0,
    HasKitchen                  BIT NOT NULL DEFAULT 1,
    HasMaidRoom                 BIT NOT NULL DEFAULT 0,

    -- Unit Condition (Value Object)
    OverallCondition            NVARCHAR(50) NOT NULL,
    FloorMaterial               NVARCHAR(50) NOT NULL,
    FloorCondition              NVARCHAR(50) NOT NULL,
    WallFinish                  NVARCHAR(50) NOT NULL,
    WallCondition               NVARCHAR(50) NOT NULL,
    CeilingType                 NVARCHAR(50) NOT NULL,
    CeilingCondition            NVARCHAR(50) NOT NULL,

    -- Kitchen (Value Object)
    KitchenType                 NVARCHAR(50) NOT NULL,      -- OpenPlan, Closed, Pantry
    HasBuiltInCabinets          BIT NOT NULL DEFAULT 0,
    HasHood                     BIT NOT NULL DEFAULT 0,
    HasStove                    BIT NOT NULL DEFAULT 0,
    KitchenCondition            NVARCHAR(50) NOT NULL,

    -- Bathroom (Value Object)
    BathroomType                NVARCHAR(50) NULL,          -- Ensuite, Shared, Jacuzzi
    FixtureQuality              NVARCHAR(50) NOT NULL,      -- Basic, Standard, Premium
    HasBathtub                  BIT NOT NULL DEFAULT 0,
    HasShowerEnclosure          BIT NOT NULL DEFAULT 0,
    BathroomCondition           NVARCHAR(50) NOT NULL,

    -- Decoration (Value Object)
    IsDecorated                 BIT NOT NULL DEFAULT 0,
    DecorationLevel             NVARCHAR(50) NULL,          -- Basic, Standard, Luxury
    DecorationCondition         NVARCHAR(50) NULL,

    -- Fixtures (Value Object)
    HasAirConditioner           BIT NOT NULL DEFAULT 0,
    NumberOfACUnits             INT NULL,
    HasWaterHeater              BIT NOT NULL DEFAULT 0,
    HasBuiltInWardrobe          BIT NOT NULL DEFAULT 0,
    HasCurtains                 BIT NOT NULL DEFAULT 0,

    -- View (Value Object)
    ViewType                    NVARCHAR(50) NULL,          -- City, Garden, Pool, Mountain, River, Sea
    ViewQuality                 NVARCHAR(50) NULL,          -- Excellent, Good, Fair, Obstructed
    HasBalcony                  BIT NOT NULL DEFAULT 0,

    -- Parking (Value Object)
    NumberOfParkingSpaces       INT NOT NULL DEFAULT 0,
    ParkingType                 NVARCHAR(50) NULL,          -- Covered, Uncovered, Mechanical
    ParkingFloor                INT NULL,

    -- Common Facilities (Value Object)
    HasSwimmingPool             BIT NOT NULL DEFAULT 0,
    HasFitnessCenter            BIT NOT NULL DEFAULT 0,
    HasGarden                   BIT NOT NULL DEFAULT 0,
    HasPlayground               BIT NOT NULL DEFAULT 0,
    HasClubhouse                BIT NOT NULL DEFAULT 0,
    HasSecurity                 BIT NOT NULL DEFAULT 0,
    SecurityType                NVARCHAR(50) NULL,          -- Guard, CCTV, Both
    FacilitiesCondition         NVARCHAR(50) NULL,

    -- Juristic Information (Value Object)
    MonthlyFee                  DECIMAL(18,2) NOT NULL,     -- Baht per month
    SinkingFund                 DECIMAL(18,2) NULL,         -- Baht
    JuristicStatus              NVARCHAR(50) NOT NULL,      -- Active, Issues
    ManagementCompany           NVARCHAR(200) NULL,

    -- Assessment
    Defects                     NVARCHAR(MAX) NULL,
    RequiredRepairs             NVARCHAR(MAX) NULL,
    Remarks                     NVARCHAR(MAX) NULL,
    LastUpdatedBy               UNIQUEIDENTIFIER NOT NULL,
    LastUpdatedOn               DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### Valuation Factors

Condos have specific valuation considerations:

1. **Floor Level**: Higher floors usually command premium
2. **View**: Unobstructed views increase value
3. **Direction**: Affects sunlight and heat
4. **Parking**: Multiple spaces add value
5. **Common Facilities**: Quality and maintenance affect value
6. **Juristic Management**: Well-managed projects maintain value
7. **Monthly Fees**: Lower fees more attractive to buyers

### Usage Notes

```csharp
// High-rise condo valuation considerations
var condoDetail = new CondoAppraisalDetail
{
    Floor = 25,                    // Premium floor
    Direction = Direction.South,    // Good sunlight
    ViewType = ViewType.City,      // Premium view
    ViewQuality = ViewQuality.Excellent,
    NumberOfParkingSpaces = 2,     // Multiple spaces
    MonthlyFee = 4500,             // Reasonable fee
    FacilitiesCondition = Condition.Good  // Well-maintained
};
// These factors would justify higher price per sqm
```

---

## 4. VehicleAppraisalDetail

### Purpose
Comprehensive vehicle assessment covering mechanical condition, exterior/interior state, documentation, and market factors.

### Key Assessment Areas

1. **Identification**: Chassis number, engine number (for verification)
2. **Usage History**: Mileage, previous owners, accident history
3. **Physical Condition**: Body, paint, tires, interior
4. **Mechanical Systems**: Engine, transmission, brakes, suspension
5. **Documentation**: Service book, warranty, insurance

### Schema Highlights

```sql
-- Vehicle Information
VehicleType         NVARCHAR(50) NOT NULL,      -- Sedan, SUV, Truck, Pickup, Van, Motorcycle
Make                NVARCHAR(100) NOT NULL,
Model               NVARCHAR(100) NOT NULL,
Year                INT NOT NULL,
Mileage             DECIMAL(18,2) NOT NULL,     -- km

-- Condition Assessment
BodyCondition       NVARCHAR(50) NOT NULL,      -- Excellent, Good, Fair, Poor
PaintCondition      NVARCHAR(50) NOT NULL,      -- Original, Repainted, Faded, Scratched
TireCondition       NVARCHAR(50) NOT NULL,      -- New, Good, Fair, Worn
EngineCondition     NVARCHAR(50) NOT NULL,
TransmissionCondition NVARCHAR(50) NOT NULL,

-- Special Considerations
HasRust             BIT NOT NULL DEFAULT 0,
HasDents            BIT NOT NULL DEFAULT 0,
HasOilLeak          BIT NOT NULL DEFAULT 0,
AccidentHistory     NVARCHAR(50) NOT NULL,      -- None, Minor, Major, Unknown
```

### Valuation Impact

| Factor | Impact on Value |
|--------|----------------|
| Low Mileage | ↑ Higher value |
| Complete Service History | ↑ Buyer confidence |
| Accident History | ↓ Significant reduction |
| Original Paint | ↑ Better value retention |
| Rust/Corrosion | ↓↓ Major concern |
| Well-Maintained | ↑ Premium pricing |

---

## 5. VesselAppraisalDetail

### Purpose
Marine vessel assessment including hull condition, engine systems, navigation equipment, and safety compliance.

### Key Sections

```sql
-- Vessel Specifications
VesselType          NVARCHAR(50) NOT NULL,      -- SpeedBoat, SailingYacht, MotorYacht, FishingBoat
YearBuilt           INT NOT NULL,
Length              DECIMAL(18,2) NOT NULL,     -- meters
GrossTonnage        DECIMAL(18,2) NULL,

-- Engine & Propulsion
NumberOfEngines     INT NOT NULL,
TotalHorsePower     INT NOT NULL,
EngineHours         DECIMAL(18,2) NULL,
FuelType            NVARCHAR(50) NOT NULL,

-- Hull Condition
HullMaterial        NVARCHAR(50) NOT NULL,      -- Fiberglass, Steel, Aluminum, Wood
HullCondition       NVARCHAR(50) NOT NULL,
HasOsmosisBlisters  BIT NOT NULL DEFAULT 0,     -- Critical for fiberglass hulls
HasCorrosion        BIT NOT NULL DEFAULT 0,     -- Critical for metal hulls

-- Safety Equipment
HasLifeRaft         BIT NOT NULL DEFAULT 0,
HasEPIRB            BIT NOT NULL DEFAULT 0,     -- Emergency Position Indicating Radio Beacon
NumberOfLifeJackets INT NOT NULL,
```

### Marine-Specific Considerations

- **Engine Hours**: Like mileage for vehicles
- **Hull Survey**: Professional inspection critical
- **Safety Equipment**: Legal requirement, affects insurability
- **Documentation**: Registration, survey reports essential

---

## 6. MachineryAppraisalDetail

### Purpose
Industrial machinery and equipment assessment covering operational status, maintenance history, and marketability.

### Key Assessment Areas

```sql
-- Machinery Information
MachineryType       NVARCHAR(50) NOT NULL,      -- CNC, Press, Lathe, Generator, Forklift, Crane
Category            NVARCHAR(50) NOT NULL,      -- Production, Construction, Agricultural, Office
Capacity            NVARCHAR(100) NULL,         -- e.g., "10 tons", "500 kW"
YearOfManufacture   INT NOT NULL,
HoursUsed           DECIMAL(18,2) NULL,

-- Operational Status
CurrentStatus       NVARCHAR(50) NOT NULL,      -- Operational, Idle, UnderRepair, Decommissioned
OperatingEfficiency DECIMAL(5,2) NULL,          -- Percentage

-- Condition Assessment
OverallCondition    NVARCHAR(50) NOT NULL,
StructuralIntegrity NVARCHAR(50) NOT NULL,      -- Critical for safety
CorrosionLevel      NVARCHAR(50) NOT NULL,      -- None, Minor, Moderate, Severe

-- Maintenance
MaintenanceHistory  NVARCHAR(50) NOT NULL,      -- Excellent, Good, Partial, Poor, Unknown
LastMajorOverhaul   DATETIME2 NULL,

-- Marketability
DemandLevel         NVARCHAR(50) NOT NULL,      -- High, Medium, Low, Obsolete
ReplacementCostNew  DECIMAL(18,2) NULL,
EstimatedRemainingLife INT NULL,                -- Years
```

### Critical Factors

1. **Operational Status**: Non-operational machinery worth significantly less
2. **Maintenance Records**: Complete records command premium
3. **Technology Level**: Obsolete technology hard to sell
4. **Spare Parts**: Availability affects long-term viability
5. **Safety Compliance**: Non-compliant equipment may be worthless

---

## Implementation Guidelines

### Entity Framework Configuration

```csharp
// In LandAppraisalDetailConfiguration.cs
public class LandAppraisalDetailConfiguration : IEntityTypeConfiguration<LandAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<LandAppraisalDetail> builder)
    {
        builder.ToTable("LandAppraisalDetails", "appraisal");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Value Object: LandCharacteristics
        builder.OwnsOne(e => e.LandCharacteristics, lc =>
        {
            lc.Property(x => x.Topography).HasColumnName("Topography").HasConversion<string>();
            lc.Property(x => x.LandShape).HasColumnName("LandShape").HasConversion<string>();
            lc.Property(x => x.SoilType).HasColumnName("SoilType").HasConversion<string>();
            lc.Property(x => x.DrainageCondition).HasColumnName("DrainageCondition").HasConversion<string>();
            lc.Property(x => x.FloodRisk).HasColumnName("FloodRisk").HasConversion<string>();
        });

        // Value Object: AccessRoad
        builder.OwnsOne(e => e.AccessRoad, ar =>
        {
            ar.Property(x => x.HasAccessRoad).HasColumnName("HasAccessRoad");
            ar.Property(x => x.RoadType).HasColumnName("RoadType").HasConversion<string>();
            ar.Property(x => x.RoadWidth).HasColumnName("RoadWidth");
            ar.Property(x => x.RoadSurfaceType).HasColumnName("RoadSurfaceType").HasConversion<string>();
            ar.Property(x => x.RoadCondition).HasColumnName("RoadCondition").HasConversion<string>();
        });

        // Relationships
        builder.HasOne<Appraisal>()
            .WithOne()
            .HasForeignKey<LandAppraisalDetail>(e => e.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.AppraisalId);
    }
}
```

### Repository Pattern

```csharp
public interface IPropertyDetailRepository<T> where T : class
{
    Task<T?> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<T> CreateAsync(T detail, CancellationToken cancellationToken = default);
    Task UpdateAsync(T detail, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

// Usage
public class LandDetailRepository : IPropertyDetailRepository<LandAppraisalDetail>
{
    private readonly AppraisalDbContext _context;

    public async Task<LandAppraisalDetail?> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        return await _context.LandAppraisalDetails
            .FirstOrDefaultAsync(x => x.AppraisalId == appraisalId, cancellationToken);
    }

    // Other methods...
}
```

### CQRS Command Example

```csharp
// CreateLandDetailCommand.cs
public record CreateLandDetailCommand(
    Guid AppraisalId,
    LandCharacteristics LandCharacteristics,
    decimal? FrontageWidth,
    decimal? Depth,
    bool CornerLot,
    AccessRoad AccessRoad,
    Utilities Utilities,
    string? Zoning,
    LandUse LandUse
) : ICommand<Result<Guid>>;

// Handler
public class CreateLandDetailCommandHandler : ICommandHandler<CreateLandDetailCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateLandDetailCommand request, CancellationToken cancellationToken)
    {
        // Validate appraisal exists and is for land
        var appraisal = await _appraisalRepository.GetByIdAsync(request.AppraisalId);
        if (appraisal == null)
            return Result.Failure<Guid>("Appraisal not found");

        if (appraisal.CollateralType != CollateralType.Land)
            return Result.Failure<Guid>("Appraisal is not for land type");

        // Check if detail already exists
        var existing = await _landDetailRepository.GetByAppraisalIdAsync(request.AppraisalId);
        if (existing != null)
            return Result.Failure<Guid>("Land detail already exists for this appraisal");

        // Create detail
        var detail = new LandAppraisalDetail
        {
            AppraisalId = request.AppraisalId,
            LandCharacteristics = request.LandCharacteristics,
            FrontageWidth = request.FrontageWidth,
            Depth = request.Depth,
            CornerLot = request.CornerLot,
            AccessRoad = request.AccessRoad,
            Utilities = request.Utilities,
            Zoning = request.Zoning,
            LandUse = request.LandUse,
            LastUpdatedBy = _currentUserService.UserId,
            LastUpdatedOn = DateTime.UtcNow
        };

        await _landDetailRepository.CreateAsync(detail, cancellationToken);

        return Result.Success(detail.Id);
    }
}
```

### Validation Example

```csharp
public class CreateLandDetailValidator : AbstractValidator<CreateLandDetailCommand>
{
    public CreateLandDetailValidator()
    {
        RuleFor(x => x.AppraisalId)
            .NotEmpty();

        RuleFor(x => x.LandCharacteristics)
            .NotNull()
            .SetValidator(new LandCharacteristicsValidator());

        RuleFor(x => x.FrontageWidth)
            .GreaterThan(0).When(x => x.FrontageWidth.HasValue)
            .WithMessage("Frontage width must be positive");

        RuleFor(x => x.Depth)
            .GreaterThan(0).When(x => x.Depth.HasValue)
            .WithMessage("Depth must be positive");

        RuleFor(x => x.AccessRoad)
            .NotNull()
            .SetValidator(new AccessRoadValidator());

        RuleFor(x => x.Utilities)
            .NotNull()
            .SetValidator(new UtilitiesValidator());
    }
}
```

## Best Practices

### 1. One Detail Record Per Appraisal
```csharp
// Enforce in application logic
if (await _landDetailRepository.GetByAppraisalIdAsync(appraisalId) != null)
{
    throw new InvalidOperationException("Land detail already exists");
}
```

### 2. Validate Collateral Type Match
```csharp
// Before creating LandAppraisalDetail
if (appraisal.CollateralType != CollateralType.Land)
{
    throw new InvalidOperationException("Cannot create land detail for non-land appraisal");
}
```

### 3. Update Tracking
```csharp
detail.LastUpdatedBy = currentUserId;
detail.LastUpdatedOn = DateTime.UtcNow;
await _repository.UpdateAsync(detail);
```

### 4. Null Handling for Optional Fields
```csharp
// Use nullable types appropriately
public decimal? FrontageWidth { get; set; }  // May not be applicable for all land

// Validate related fields together
if (HasAccessRoad && RoadType == null)
{
    errors.Add("Road type is required when access road exists");
}
```

### 5. Enum for Consistency
```csharp
// Instead of free text
public enum Topography
{
    Flat,
    Sloped,
    Hilly,
    Mountainous
}

// Provides:
// - Type safety
// - Consistent values
// - Easy to query/filter
// - Multi-language support
```

---

**Next**: See [08-photo-gallery.md](08-photo-gallery.md) for linking photos to these property details.
