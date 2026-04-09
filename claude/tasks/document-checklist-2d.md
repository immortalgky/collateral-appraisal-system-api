# 2D Document Checklist (PropertyType + Purpose) - Implementation

## Tasks
- [x] Step 1: Rename CollateralTypeCode → PropertyTypeCode + Add PurposeCode in Domain Entity
- [x] Step 2: Update EF Configuration (rename column, add PurposeCode, update indexes)
- [x] Step 3: Update Repository Interface & Implementation (rename + add purpose-aware methods)
- [x] Step 4: Update GetDocumentChecklist (consumer endpoint) - 4-tier fallback logic
- [x] Step 5: Update GetDocumentRequirements (admin endpoint) - add purpose filter
- [x] Step 6: Update Create Command (rename + add PurposeCode)
- [x] Step 7: Update Seed Data (rename params)
- [x] Step 8: Add EF Migration (manually corrected rename direction)
- [x] Step 9: Build verification — 0 errors

## Review

### Summary
Added a second dimension (`PurposeCode`) to the DocumentRequirement feature, enabling 2D configuration of document checklists per (PropertyType + Purpose) combination. Renamed `CollateralTypeCode` → `PropertyTypeCode` everywhere (C#, DB column, API params).

### 4-Tier Architecture
1. **Universal** (PropertyType=NULL, Purpose=NULL): Always required (e.g., APP_FORM, ID_COPY)
2. **Purpose-only** (PropertyType=NULL, Purpose=NOT NULL): Required for a purpose regardless of property type
3. **PropertyType-only** (PropertyType=NOT NULL, Purpose=NULL): Base requirement for a property type
4. **Fully specific** (PropertyType=NOT NULL, Purpose=NOT NULL): Specific to a (PropertyType, Purpose) combination

### Checklist Assembly Logic
- **ApplicationDocuments**: Union of Tier 1 + Tier 2, dedup by DocumentTypeId (Tier 2 overrides Tier 1)
- **PropertyTypeGroups**: Union of Tier 3 + Tier 4 per property type, dedup by DocumentTypeId (Tier 4 overrides Tier 3)

### Files Modified (14)

| File | Change |
|------|--------|
| `Domain/DocumentRequirements/DocumentRequirement.cs` | Renamed `CollateralTypeCode` → `PropertyTypeCode`, added `PurposeCode`, 4 factory methods (1 per tier) |
| `Domain/DocumentRequirements/IDocumentRequirementRepository.cs` | Renamed methods, added `GetUniversalRequirementsAsync`, `GetPurposeOnlyRequirementsAsync`, updated `RequirementExistsAsync` to 3-param |
| `Infrastructure/Configurations/DocumentRequirementConfiguration.cs` | Renamed column, added `PurposeCode`, updated unique index to 3-column composite |
| `Infrastructure/Repositories/DocumentRequirementRepository.cs` | Implemented new repo methods, `GetRequirementsByPropertyTypesAsync` now accepts optional purposeCode |
| `Infrastructure/Seed/DocumentRequirementDataSeed.cs` | Renamed `collateralType` → `propertyTypeCode`, `CreateForCollateral` → `CreateForPropertyType` |
| `GetDocumentChecklist/GetDocumentChecklistQuery.cs` | `CollateralTypeCodes` → `PropertyTypeCodes`, added `PurposeCode` |
| `GetDocumentChecklist/GetDocumentChecklistQueryHandler.cs` | 4-tier logic with dedup, renamed dictionaries |
| `GetDocumentChecklist/GetDocumentChecklistResult.cs` | `CollateralDocumentGroupDto` → `PropertyTypeDocumentGroupDto` |
| `GetDocumentChecklist/GetDocumentChecklistResponse.cs` | Updated to use `PropertyTypeDocumentGroupDto` |
| `GetDocumentChecklist/GetDocumentChecklistEndpoint.cs` | `collateralTypeCodes` → `propertyTypeCodes`, added `purposeCode` param |
| `GetDocumentRequirements/GetDocumentRequirementsQuery.cs` | `CollateralTypeCode` → `PropertyTypeCode`, added `PurposeCode` |
| `GetDocumentRequirements/GetDocumentRequirementsQueryHandler.cs` | Renamed, added `PurposeCode` to DTO mapping, purpose filter |
| `GetDocumentRequirements/GetDocumentRequirementsResult.cs` | Renamed fields, added `PurposeCode` |
| `GetDocumentRequirements/GetDocumentRequirementsEndpoint.cs` | `collateralTypeCode` → `propertyTypeCode`, added `purposeCode` param |
| `CreateDocumentRequirement/CreateDocumentRequirementCommand.cs` | `CollateralTypeCode` → `PropertyTypeCode`, added `PurposeCode` |
| `CreateDocumentRequirement/CreateDocumentRequirementRequest.cs` | Same rename + add |
| `CreateDocumentRequirement/CreateDocumentRequirementCommandHandler.cs` | Pattern-match switch for 4 factory methods, updated duplicate check |
| `CreateDocumentRequirement/CreateDocumentRequirementEndpoint.cs` | Updated param names |

### Files Created (1)

| File | Purpose |
|------|---------|
| `Migrations/20260301032342_RenameCollateralToPropertyTypeAndAddPurposeCode.cs` | Renames column + adds PurposeCode + updates indexes |

### Migration Note
EF Core auto-generated the migration with the wrong rename direction (CollateralTypeCode → PurposeCode instead of → PropertyTypeCode). This was manually corrected so existing data (L, B, LB, etc.) maps to PropertyTypeCode correctly.

### Security Review
- No secrets or sensitive data exposed
- No SQL injection risk — all queries use EF Core LINQ (parameterized)
- Input normalization (`ToUpperInvariant()`) applied to user-provided codes
- `ArgumentException.ThrowIfNullOrWhiteSpace()` validates factory method inputs
- Unique constraint prevents duplicate (DocumentTypeId, PropertyTypeCode, PurposeCode) combinations at DB level

### API Breaking Changes
- `collateralTypeCodes` → `propertyTypeCodes` (GET /document-checklist)
- `collateralTypeCode` → `propertyTypeCode` (GET /document-requirements)
- `CollateralTypeCode` → `PropertyTypeCode` (POST /document-requirements request body)
- New optional `purposeCode` parameter on checklist and requirements endpoints
- Response DTOs: `CollateralTypeCode`/`CollateralTypeName` → `PropertyTypeCode`/`PropertyTypeName`
- Response type: `CollateralDocumentGroupDto` → `PropertyTypeDocumentGroupDto`
