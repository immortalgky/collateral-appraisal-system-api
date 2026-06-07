# Phase D — Block Reappraisal: Seed Project + Excel Re-match

## Todo

- [x] Read codebase (AppraisalCreationService, ProjectUnit, Project, UploadProjectUnits handler, endpoints, sold-unit readers)
- [x] Deliverable 1: Seed prior project units into new project in AppraisalCreationService
- [x] Deliverable 2: Add MarkSoldByReappraisal() to ProjectUnit
- [x] Deliverable 3: Extract parser to ProjectUnitExcelParser static class; update UploadProjectUnitsCommandHandler
- [x] Deliverable 4: New UploadBlockReappraisalUnits feature folder (Command, Result, Handler, Endpoint)
- [x] Build and verify 0 errors (30 projects, 0 errors)

## Plan

### 1. AppraisalCreationService — Seed prior units on block reappraisal
- Location: lines 212-223 (the block branch inside the big try)
- After `unitOfWork.SaveChangesAsync` for the empty project:
  - If `prevAppraisalId.HasValue`, call `projectRepository.GetWithFullGraphAsync(prevAppraisalId.Value, ct)`
  - If null → log warning, skip clone
  - If found → build `List<ProjectUnit>` from `priorProject.Units.Where(u => !u.IsSold)`, recreate via CreateCondo/CreateLandAndBuilding
  - `project.ImportUnits("Seeded from prior appraisal", documentId: null, units)`
  - `await unitOfWork.SaveChangesAsync(ct)`
- AppraisalCreationService already has `AppraisalDbContext dbContext` but NOT `IProjectRepository`
  - Need to inject `IProjectRepository projectRepository`

### 2. ProjectUnit.MarkSoldByReappraisal()
- Public void method; bypasses Cash/Loan invariant of SetSaleInfo
- Sets IsSold=true, PurchaseBy=null, LoanBankName=null

### 3. Parser extraction
- New static class `ProjectUnitExcelParser` in `UploadProjectUnits` folder (or a `Shared` subfolder)
- Move `ParseCondoExcel`, `ParseLandAndBuildingExcel`, `NormalizeHeader`, `BuildHeaderMap`, `Resolve` to it
- `UploadProjectUnitsCommandHandler` calls `ProjectUnitExcelParser.ParseCondoExcel(...)`

### 4. UploadBlockReappraisalUnits feature
- Command: same shape as UploadProjectUnitsCommand
- Result: `(int MatchedUnsold, int AutoSold, int Added)`
- Handler logic:
  - Load project via GetWithFullGraphAsync
  - Parse incoming Excel via ProjectUnitExcelParser
  - Build key for each existing unit + each incoming row (Condo: CondoRegNumber ?? (TowerName+RoomNumber); LB: PlotNumber ?? HouseNumber)
  - Existing unit key in incoming → ensure IsSold=false via SetSaleInfo(false,null,null)  
  - Existing unit key NOT in incoming → MarkSoldByReappraisal()
  - Incoming row with no existing match → v1: count and log; no Add (Project.ImportUnits is a batch operation, no single-unit add on aggregate)
  - Save; return counts
- Endpoint: POST /appraisals/{appraisalId:guid}/project/units/reappraisal-upload (multipart .xlsx, 5 MB max)

## Sold-unit PurchaseBy null safety check
- `BlockUnitMaintenanceUnitDto.PurchaseBy` → `string?` — safe (nullable)
- `GetBlockUnitMaintenanceUnitsQueryHandler` — reads `pu.PurchaseBy` and passes to DTO; value object enum `.ToString()` — need to check if EF returns null or a string
- `AppraisalForCollateralResult.ProjectUnitForCollateral` — does NOT include PurchaseBy — safe
- `UpdateProjectUnitSaleInfoCommandValidator` — only validates on the REQUEST path; does not read existing PurchaseBy — safe
- Conclusion: all consumers tolerate null PurchaseBy since field is string? or not read at all for sold units

## Notes
- No `Project.AddUnit` single-unit method exists. v1: Added count is counted but NOT persisted.
- SetSaleInfo(false, null, null) can flip a sold unit back to unsold — this is the correct path for "unit reappears in Excel after being auto-sold"
