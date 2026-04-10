# AppraisalFee Domain Entity Refactoring

## Objective
Refactor AppraisalFee, AppraisalFeeItem, and AppraisalFeePaymentHistory domain entities and EF configurations to match the document specification. The new design introduces a fee summary model (AppraisalFee) that aggregates line items (AppraisalFeeItem) with payment tracking at the fee level (AppraisalFeePaymentHistory).

## Context
The current implementation has:
- AppraisalFee linked to AppraisalId with detailed tax and billing fields
- AppraisalFeeItem with quantity/unit pricing and payment history per item
- AppraisalFeePaymentHistory linked to AppraisalFeeItem with refund support

The new design simplifies to:
- AppraisalFee linked to AssignmentId (one fee per assignment) with aggregated totals
- AppraisalFeeItem with fee codes and approval workflow
- AppraisalFeePaymentHistory linked to AppraisalFee (not item) without refunds

## Changes Required

### 1. AppraisalFee Entity
**File:** `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalFee.cs`

**Remove:**
- AppraisalId
- FeeType, FeeCategory, Description
- Amount, Currency
- VATRate (nullable), VATAmount (nullable)
- WithholdingTaxRate, WithholdingTaxAmount, NetAmount
- IsBillableToCustomer, InvoiceNumber, InvoiceDate, PaymentDate, CostCenter

**Add:**
- AssignmentId (Guid, required) - one fee per assignment
- TotalFeeBeforeVAT (decimal, default 0)
- VATRate (decimal, default 7.00m)
- VATAmount (decimal, default 0)
- TotalFeeAfterVAT (decimal, default 0)
- BankAbsorbAmount (decimal, default 0)
- CustomerPayableAmount (decimal, default 0)
- TotalPaidAmount (decimal, default 0)
- OutstandingAmount (decimal, default 0)
- InspectionFeeAmount (decimal?, nullable)
- PaymentHistory collection (moved from FeeItem)

**Keep:**
- PaymentStatus (but values: Pending, PartialPaid, FullyPaid)
- Items collection

**Methods:**
- Factory: `Create(Guid assignmentId)` - initialize with defaults
- `RecalculateFromItems()` - sum FeeAmount from items, calculate VAT, totals
- `SetBankAbsorb(decimal amount)` - set bank absorb and calculate customer payable
- `RecordPayment(decimal amount)` - update paid amounts and status

### 2. AppraisalFeeItem Entity
**File:** `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalFeeItem.cs`

**Remove:**
- ItemType, Description
- Quantity, UnitPrice, Amount
- VATRate, VATAmount, NetAmount
- PaymentStatus
- PaymentHistory collection (moved to AppraisalFee)

**Add:**
- FeeCode (string, e.g., "01", "02", "03")
- FeeDescription (string)
- FeeAmount (decimal)
- RequiresApproval (bool, default false)
- ApprovalStatus (string?, nullable) - Pending, Approved, Rejected
- ApprovedBy (Guid?, nullable)
- ApprovedAt (DateTime?, nullable)
- RejectionReason (string?, nullable)

**Methods:**
- Factory: `Create(Guid appraisalFeeId, string feeCode, string feeDescription, decimal feeAmount)`
- `Approve(Guid approvedBy)` - set approval status and timestamp
- `Reject(Guid rejectedBy, string reason)` - set rejection status and reason

### 3. AppraisalFeePaymentHistory Entity
**File:** `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Domain/Appraisals/AppraisalFeePaymentHistory.cs`

**Remove:**
- AppraisalFeeItemId (replace with AppraisalFeeId)
- Status
- RefundAmount, RefundDate, RefundReason
- Refund(), Cancel() methods

**Change:**
- FK: AppraisalFeeItemId → AppraisalFeeId
- PaidAmount → PaymentAmount
- PaymentMethod: now nullable

**Add:**
- Remarks (string?, nullable)

**Methods:**
- Factory: `Create(Guid appraisalFeeId, decimal paymentAmount, DateTime paymentDate, string? paymentMethod = null, string? paymentReference = null, string? remarks = null)`

### 4. EF Core Configurations
**File:** `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Infrastructure/Configurations/AppraisalFeeConfiguration.cs`

**AppraisalFeeConfiguration:**
- AssignmentId: IsRequired(), HasIndex with IsUnique
- TotalFeeBeforeVAT: HasPrecision(18,2), HasDefaultValue(0)
- VATRate: HasPrecision(5,2), HasDefaultValue(7.00m)
- VATAmount: HasPrecision(18,2), HasDefaultValue(0)
- TotalFeeAfterVAT: HasPrecision(18,2), HasDefaultValue(0)
- BankAbsorbAmount: HasPrecision(18,2), HasDefaultValue(0)
- CustomerPayableAmount: HasPrecision(18,2), HasDefaultValue(0)
- TotalPaidAmount: HasPrecision(18,2), HasDefaultValue(0)
- OutstandingAmount: HasPrecision(18,2), HasDefaultValue(0)
- PaymentStatus: IsRequired(), HasMaxLength(50), HasDefaultValue("Pending")
- InspectionFeeAmount: HasPrecision(18,2)
- HasMany Items with FK AppraisalFeeId, cascade delete
- HasMany PaymentHistory with FK AppraisalFeeId, cascade delete
- Remove: old AppraisalId index and old field configs

**AppraisalFeeItemConfiguration:**
- AppraisalFeeId: IsRequired()
- FeeCode: IsRequired(), HasMaxLength(20)
- FeeDescription: IsRequired(), HasMaxLength(200)
- FeeAmount: HasPrecision(18,2)
- RequiresApproval: HasDefaultValue(false)
- ApprovalStatus: HasMaxLength(50)
- ApprovedAt: optional
- RejectionReason: HasMaxLength(4000)
- Index on AppraisalFeeId
- Remove: PaymentHistory navigation and old field configs

**AppraisalFeePaymentHistoryConfiguration:**
- AppraisalFeeId: IsRequired() (was AppraisalFeeItemId)
- PaymentAmount: HasPrecision(18,2)
- PaymentDate: IsRequired()
- PaymentMethod: HasMaxLength(50) (nullable)
- PaymentReference: HasMaxLength(100)
- Remarks: HasMaxLength(4000)
- Index on AppraisalFeeId
- Remove: Status, RefundAmount configs

### 5. Update AppraisalCreationService
**File:** `/Users/gky/Developer/collateral-appraisal-system-api/Modules/Appraisal/Appraisal/Application/Services/AppraisalCreationService.cs`

**Change:**
- Line 134-145: Update `AppraisalFee.Create()` to use new factory signature
- Remove old parameters: feeType, feeCategory, description, amount
- Use: `AppraisalFee.Create(assignmentId)` (requires assignmentId)
- NOTE: Since assignment creation is not in scope, we'll need to handle this appropriately

## Task List

- [ ] 1. Rewrite AppraisalFee.cs entity
  - [ ] Remove old properties
  - [ ] Add new properties with proper defaults
  - [ ] Update factory method
  - [ ] Add RecalculateFromItems() method
  - [ ] Add SetBankAbsorb() method
  - [ ] Add RecordPayment() method
  - [ ] Move PaymentHistory collection from Items to Fee

- [ ] 2. Rewrite AppraisalFeeItem.cs entity
  - [ ] Remove old properties
  - [ ] Add new properties (FeeCode, FeeDescription, etc.)
  - [ ] Update factory method
  - [ ] Add Approve() method
  - [ ] Add Reject() method
  - [ ] Remove PaymentHistory collection

- [ ] 3. Rewrite AppraisalFeePaymentHistory.cs entity
  - [ ] Change FK: AppraisalFeeItemId → AppraisalFeeId
  - [ ] Rename: PaidAmount → PaymentAmount
  - [ ] Add Remarks property
  - [ ] Update factory method
  - [ ] Remove Refund() and Cancel() methods

- [ ] 4. Update AppraisalFeeConfiguration.cs
  - [ ] Update AppraisalFeeConfiguration for new schema
  - [ ] Update AppraisalFeeItemConfiguration for new schema
  - [ ] Update AppraisalFeePaymentHistoryConfiguration for new schema

- [ ] 5. Update AppraisalCreationService.cs
  - [ ] Update AppraisalFee.Create() call to match new signature
  - [ ] Handle assignmentId requirement (may need temporary solution)

- [ ] 6. Verify and test
  - [ ] Build solution to check compilation
  - [ ] Review all changes for consistency
  - [ ] Note: Database migration will be needed separately

## Breaking Changes
- AppraisalFee.Create() signature changed
- AppraisalFeeItem.Create() signature changed
- AppraisalFeePaymentHistory.Create() signature changed
- PaymentHistory moved from AppraisalFeeItem to AppraisalFee
- FK change: AppraisalFeePaymentHistory.AppraisalFeeItemId → AppraisalFeeId

## Notes
- All entities inherit from `Entity<Guid>` (from Shared.DDD namespace)
- Use `Guid.CreateVersion7()` for Id generation
- Keep private parameterless constructor for EF Core
- Implicit usings in project
- Database migration will be required after these changes
- AppraisalCreationService may need temporary solution for assignmentId since assignment creation is not in current scope

## Review
(To be filled after implementation)
