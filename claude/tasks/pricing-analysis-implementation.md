# Pricing Analysis Data Model Implementation

## Overview
Implementing pricing analysis data model changes for the Appraisal module following DDD patterns.

## Todo List

- [x] Task 1: Modify PricingAnalysis Entity - Change AppraisalId to PropertyGroupId
- [x] Task 2: Create PricingFactorScore Entity with complete domain logic
- [x] Task 3: Update PricingCalculation Entity - Add factor scores collection and methods
- [x] Task 4: Update EF Core Configuration for all pricing entities
- [x] Task 5: Register PricingFactorScore in AppraisalDbContext
- [x] Task 6: Build solution to verify compilation
- [x] Task 7: Review and validate all changes

## Implementation Notes
- Following DDD patterns from existing codebase
- Using Entity<Guid> base class from Shared.DDD
- Private constructors for EF Core
- Static factory methods for entity creation
- Backing fields pattern for collections

## Review Section

### Implementation Summary

All tasks completed successfully. The pricing analysis data model has been refactored to support PropertyGroup-level pricing and factor-level scoring for WQS and SaleGrid methods.

### Changes Made

#### 1. PricingAnalysis Entity (`Domain/Appraisals/PricingAnalysis.cs`)
- Changed `AppraisalId` property to `PropertyGroupId`
- Updated factory method `Create(Guid propertyGroupId)` parameter name
- Updated XML documentation to reflect "per PropertyGroup" relationship
- **Impact**: PricingAnalysis now has 1:1 relationship with PropertyGroup instead of Appraisal

#### 2. PricingFactorScore Entity (NEW - `Domain/Appraisals/PricingFactorScore.cs`)
- Created new entity for factor-level scoring comparisons
- **Properties**:
  - `PricingCalculationId` and `FactorId` - Foreign keys
  - Subject/Comparable values and scores for comparison
  - Automatic calculation of `ScoreDifference` and `WeightedScore`
  - `AdjustmentPct` for final adjustments
  - `DisplaySequence` for UI ordering
- **Methods**:
  - `Create()` - Static factory method
  - `SetSubjectValues()` / `SetComparableValues()` - Update values with auto-calculation
  - `SetAdjustment()` - Set percentage adjustments
  - `UpdateWeight()` / `UpdateSequence()` - Modify display properties
  - Private calculation methods for score difference and weighted scores
- **Purpose**: Stores granular factor comparisons between subject property and market comparables

#### 3. PricingCalculation Entity (`Domain/Appraisals/PricingCalculation.cs`)
- Added private backing field `_factorScores` list
- Added public read-only property `FactorScores`
- **New Methods**:
  - `AddFactorScore()` - Adds new factor score with validation (prevents duplicates)
  - `RemoveFactorScore()` - Removes factor and resequences remaining scores
  - `RecalculateTotalFactorAdjustment()` - Sums all factor adjustment percentages into `TotalFactorDiffPct`
- **Impact**: PricingCalculation can now manage multiple factor scores per comparable

#### 4. EF Core Configuration (`Infrastructure/Configurations/PricingConfiguration.cs`)
- **PricingAnalysisConfiguration**: Changed `AppraisalId` to `PropertyGroupId` in configuration and unique index
- **PricingCalculationConfiguration**: Added HasMany relationship for `FactorScores` with backing field access mode
- **PricingFactorScoreConfiguration** (NEW):
  - Table: `PricingFactorScores`
  - Precision configurations for all decimal properties
  - String length constraints (500 chars for values/remarks)
  - Indexes on `PricingCalculationId` and composite unique index on `(PricingCalculationId, FactorId)`
  - Cascade delete behavior

#### 5. AppraisalDbContext (`Infrastructure/AppraisalDbContext.cs`)
- Added `DbSet<PricingFactorScore> PricingFactorScores` property
- Positioned correctly in the Pricing Entities section

### Build Verification
- Solution built successfully with **0 errors**
- Only warnings present (371 warnings) are pre-existing:
  - NuGet package version constraints
  - xUnit test analyzer suggestions
  - Unused field warnings in test projects
- All new code compiles correctly

### Architecture Compliance
All changes follow the established patterns in the codebase:
- ✅ DDD patterns with Entity<Guid> base class
- ✅ Private parameterless constructors for EF Core
- ✅ Static factory methods for entity creation
- ✅ Encapsulated domain logic (calculations in entity methods)
- ✅ Backing fields with read-only collections
- ✅ PropertyAccessMode.Field for EF Core relationships
- ✅ Proper validation in domain methods
- ✅ Cascade delete behaviors configured
- ✅ Appropriate indexes for query performance

### Database Schema Impact (Future Migration)
When migration is created, the following changes will occur:
1. `PricingAnalysis` table: Column rename `AppraisalId` → `PropertyGroupId`
2. New table: `PricingFactorScores` with 12 columns
3. New indexes on `PricingFactorScores` table
4. Foreign key relationship from `PricingFactorScores` to `PricingCalculations`

### Next Steps
1. Create EF Core migration: `dotnet ef migrations add AddPricingFactorScoresAndUpdateAnalysis --project Modules/Appraisal/Appraisal --startup-project Bootstrapper/Api`
2. Review migration script for correctness
3. Apply migration to database
4. Implement application layer (commands/queries) to use the new entities
5. Create API endpoints for factor score management

### Security Review
✅ No security concerns:
- No sensitive data exposed
- All properties use private setters
- Domain validation prevents invalid states
- No SQL injection risks (using EF Core)
- No authentication/authorization changes

### Files Modified
1. `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/PricingAnalysis.cs`
2. `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/PricingCalculation.cs`
3. `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Infrastructure/Configurations/PricingConfiguration.cs`
4. `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Infrastructure/AppraisalDbContext.cs`

### Files Created
1. `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/PricingFactorScore.cs`
