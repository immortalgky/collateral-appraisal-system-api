# Pricing Analysis - Comparable Linking & Calculation Management

## Overview
Implement 6 features for managing comparable links and calculations in the Pricing Analysis module.

## TODO List

### Comparable Linking Features (3)
- [x] 1. LinkComparable - POST /pricing-analysis/{id}/methods/{methodId}/comparables
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Request
  - [x] Response
  - [x] Result

- [x] 2. UnlinkComparable - DELETE /pricing-analysis/{id}/methods/{methodId}/comparables/{linkId}
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Result

- [x] 3. UpdateComparableLink - PUT /pricing-analysis/{id}/comparable-links/{linkId}
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Request
  - [x] Response
  - [x] Result

### Calculation Management Features (3)
- [x] 4. AddCalculation - POST /pricing-analysis/{id}/methods/{methodId}/calculations
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Request
  - [x] Response
  - [x] Result

- [x] 5. UpdateCalculation - PUT /pricing-analysis/{id}/calculations/{calcId}
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Request
  - [x] Response
  - [x] Result

- [x] 6. DeleteCalculation - DELETE /pricing-analysis/{id}/calculations/{calcId}
  - [x] Command
  - [x] CommandHandler
  - [x] Endpoint
  - [x] Result

### Testing & Verification
- [x] Build solution
- [x] Verify all endpoints compile
- [x] Check domain method usage is correct

## Technical Notes
- Follow existing patterns from MarketComparableTemplates features
- Use IPricingAnalysisRepository for data access
- All commands implement ICommand and ITransactionalCommand<IAppraisalUnitOfWork>
- Tag all endpoints with "PricingAnalysis"
- Use Mapster for Result -> Response mapping
- Domain entities handle business logic (PricingAnalysisMethod, PricingComparableLink, PricingCalculation)

## Domain Methods Available
- PricingAnalysisMethod.LinkComparable(marketComparableId, displaySequence, weight?)
- PricingAnalysisMethod.AddCalculation(marketComparableId)
- PricingComparableLink.SetWeight(weight)
- PricingComparableLink.SetDisplaySequence(sequence)
- PricingCalculation.SetOfferingPrice(...)
- PricingCalculation.SetSellingPrice(...)
- PricingCalculation.SetTimeAdjustment(...)
- PricingCalculation.SetLandAdjustment(...)
- PricingCalculation.SetBuildingAdjustment(...)
- PricingCalculation.SetFactorAdjustment(...)
- PricingCalculation.SetResult(...)

## Review Section

### Implementation Summary
Successfully implemented all 6 features for Pricing Analysis - Comparable Linking & Calculation Management.

**Total Files Created: 34**
- LinkComparable: 6 files
- UnlinkComparable: 4 files
- UpdateComparableLink: 6 files
- AddCalculation: 6 files
- UpdateCalculation: 6 files
- DeleteCalculation: 4 files

### Features Implemented

#### 1. LinkComparable
**Endpoint**: POST /pricing-analysis/{id}/methods/{methodId}/comparables
- Links a market comparable to a pricing method
- Creates both PricingComparableLink and PricingCalculation in one operation
- Returns both linkId and calculationId
- Validates method exists before linking

#### 2. UnlinkComparable
**Endpoint**: DELETE /pricing-analysis/{id}/methods/{methodId}/comparables/{linkId}
- Removes comparable link from pricing method
- Also removes associated calculation (cascade delete)
- Uses reflection to access private collections (temporary solution until domain method added)
- Returns 204 NoContent on success

#### 3. UpdateComparableLink
**Endpoint**: PUT /pricing-analysis/{id}/comparable-links/{linkId}
- Updates weight and/or display sequence of a link
- Uses domain methods: SetWeight() and SetDisplaySequence()
- Both parameters optional - updates only what's provided
- Returns updated link data

#### 4. AddCalculation
**Endpoint**: POST /pricing-analysis/{id}/methods/{methodId}/calculations
- Creates standalone calculation for a market comparable
- Uses domain method: PricingAnalysisMethod.AddCalculation()
- Returns calculationId for further updates

#### 5. UpdateCalculation
**Endpoint**: PUT /pricing-analysis/{id}/calculations/{calcId}
- Updates all calculation fields:
  - Offering/Selling Price (price, unit, adjustments)
  - Time Adjustment (year, month, percentages)
  - Land Adjustment (area deficient, price, value)
  - Building Adjustment (usable area deficient, price, value)
  - Factor Adjustment (percentages, amounts)
  - Results (total adjusted value, weight)
- Uses domain methods: SetOfferingPrice(), SetSellingPrice(), SetTimeAdjustment(), etc.
- All fields optional - updates only provided values
- Returns calculationId

#### 6. DeleteCalculation
**Endpoint**: DELETE /pricing-analysis/{id}/calculations/{calcId}
- Removes calculation from pricing method
- Uses reflection to access private collection (temporary solution)
- Returns 204 NoContent on success

### Technical Approach

**CQRS Pattern**
- Commands implement ICommand<TResult> and ITransactionalCommand<IAppraisalUnitOfWork>
- CommandHandlers retrieve aggregates via IPricingAnalysisRepository
- All updates wrapped in transactions

**Domain-Driven Design**
- Business logic encapsulated in domain entities
- Used domain methods where available (LinkComparable, AddCalculation, SetWeight, etc.)
- Reflection used temporarily for delete operations (UnlinkComparable, DeleteCalculation)

**API Design**
- RESTful endpoints with proper HTTP verbs
- Route parameters for IDs (pricingAnalysisId, methodId, linkId, calculationId)
- Request bodies for POST/PUT operations
- 201 Created for POST operations with location header
- 200 OK for PUT operations
- 204 NoContent for DELETE operations
- All endpoints tagged with "PricingAnalysis"

**Data Flow**
1. Endpoint receives request
2. Maps request to command
3. CommandHandler loads aggregate via repository
4. Domain methods enforce business rules
5. Repository persists changes
6. Result mapped to response via Mapster

### Security Considerations
- No sensitive data exposed in responses
- All operations require valid IDs (GUIDs)
- Domain validation prevents invalid states:
  - Weight must be 0-100
  - OfferingPrice required before SetSellingPrice
  - Duplicate comparables prevented in LinkComparable
- Transaction boundaries ensure data consistency

### Code Quality
- Build succeeded with 0 errors
- All warnings are pre-existing in Shared project
- Consistent naming conventions followed
- Proper null handling throughout
- Clear exception messages for not found scenarios

### Potential Improvements (Future)
1. Add domain methods for delete operations to avoid reflection
2. Add validation attributes to Request DTOs
3. Add integration tests for all 6 endpoints
4. Add authorization policies for endpoints
5. Consider adding bulk operations (LinkMultipleComparables)
6. Add audit logging for calculation changes
7. Consider adding calculation versioning/history

### Files Location
All files created in:
`/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/`

Directory structure:
```
PricingAnalysis/
├── LinkComparable/
│   ├── LinkComparableCommand.cs
│   ├── LinkComparableCommandHandler.cs
│   ├── LinkComparableEndpoint.cs
│   ├── LinkComparableRequest.cs
│   ├── LinkComparableResponse.cs
│   └── LinkComparableResult.cs
├── UnlinkComparable/
│   ├── UnlinkComparableCommand.cs
│   ├── UnlinkComparableCommandHandler.cs
│   ├── UnlinkComparableEndpoint.cs
│   └── UnlinkComparableResult.cs
├── UpdateComparableLink/
│   ├── UpdateComparableLinkCommand.cs
│   ├── UpdateComparableLinkCommandHandler.cs
│   ├── UpdateComparableLinkEndpoint.cs
│   ├── UpdateComparableLinkRequest.cs
│   ├── UpdateComparableLinkResponse.cs
│   └── UpdateComparableLinkResult.cs
├── AddCalculation/
│   ├── AddCalculationCommand.cs
│   ├── AddCalculationCommandHandler.cs
│   ├── AddCalculationEndpoint.cs
│   ├── AddCalculationRequest.cs
│   ├── AddCalculationResponse.cs
│   └── AddCalculationResult.cs
├── UpdateCalculation/
│   ├── UpdateCalculationCommand.cs
│   ├── UpdateCalculationCommandHandler.cs
│   ├── UpdateCalculationEndpoint.cs
│   ├── UpdateCalculationRequest.cs
│   ├── UpdateCalculationResponse.cs
│   └── UpdateCalculationResult.cs
└── DeleteCalculation/
    ├── DeleteCalculationCommand.cs
    ├── DeleteCalculationCommandHandler.cs
    ├── DeleteCalculationEndpoint.cs
    └── DeleteCalculationResult.cs
```
