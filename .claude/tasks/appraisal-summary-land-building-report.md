# Appraisal Summary Land Building Report (FSD §2.1.3.1)

## Goal
Add a read-only PDF report `appraisal-summary-land-building` to the existing Reporting module.

## Files to create/edit
- [x] `Modules/Reporting/Reporting/Application/Models/AppraisalSummaryModel.cs` — model POCO
- [x] `Modules/Reporting/Reporting/Application/Providers/AppraisalSummaryLandBuildingDataProvider.cs` — Dapper provider
- [x] `Modules/Reporting/Reporting/Templates/appraisal-summary-land-building.html` — main template
- [x] `Modules/Reporting/Reporting/Templates/partials/approver-block.html` — shared partial
- [x] `Modules/Reporting/Reporting/ReportingModule.cs` — add DI line

## Column confirmations (from configs/views)
- Appraisals: `AppraisalNumber`, `RequestId`, `FacilityLimit`, `AppraisalType`, `Purpose` ✓
- vw_AppraisalList: `CustomerName` (OUTER APPLY top 1 from RequestCustomers), `AppointmentDateTime` (OUTER APPLY latest apt) ✓
- Appointments: `AppointmentDateTime`, FK via `AssignmentId → AppraisalAssignments.AppraisalId` ✓
- LandAppraisalDetails: `OwnerName`, `UrbanPlanningType`, `HasObligation`, `Village`, `Soi`, `Street` (road), `SubDistrict`, `District`, `Province`, `LandOffice` (address value object), `Latitude`/`Longitude` (coords value object) ✓
- LandTitles: `AreaRai`, `AreaNgan`, `AreaSquareWa`, `GovernmentPrice`, `GovernmentPricePerSqWa` ✓
- AppraisalAssignments: `AssignmentType` (string code), `AssigneeCompanyId`, `InternalAppraiserId` ✓
- auth.AspNetUsers: `UserName` (bank code), `FirstName`, `LastName`, `Position` ✓
- auth.Companies: `Id`, `Name` ✓
- ValuationAnalyses: `AppraisalId`, `MarketValue`, `AppraisedValue`, `ForcedSaleValue`, `InsuranceValue` ✓
- GroupValuations: `ValuationAnalysisId`, `PropertyGroupId`, `AppraisedValue`, `ForcedSaleValue`, `ValuePerUnit`, `UnitType` ✓
- PricingFinalValues: `FinalValue`, `FinalValueRounded`, `FinalValueAdjusted`, `AppraisalPrice`, `BuildingCost`, `LandValue`, `LandArea` ✓
- PricingAnalysisMethods: `MethodType` (string) ✓
- PropertyGroups: `AppraisalId`, `GroupNumber`, `GroupName` (all via OwnsMany inline config) ✓
- AppraisalReviews: `AppraisalId`, `CommitteeId`, `MeetingId` ✓
- CommitteeVotes: `ReviewId`, `CommitteeMemberId`, `MemberName`, `MemberRole`, `Vote`, `Comments` ✓
- AppraisalDecisions: `AppraisalId`, `CommitteeOpinion`, `Condition`, `Remark` ✓
- workflow.Meetings: `Id`, `MeetingNo`, `StartAt` ✓
- RequestCustomers: join multiple for customer name; `Name` column ✓

## Deferred fields (no verified source)
- AoName (AO name/dept): no column in explored schema — deferred
- GPS coordinates: exist on LandAppraisalDetails as Latitude/Longitude value object columns — INCLUDED
- IncludeAreaDetail flag: PricingFinalValues has `IncludeLandArea` — check entity domain first; if not confirmed, defer

## Build check
- [x] `dotnet build Modules/Reporting/Reporting/Reporting.csproj` — 0 errors, 7 pre-existing warnings (all in Auth/Shared, not Reporting)

## Review

### Files created
- `Modules/Reporting/Reporting/Application/Models/AppraisalSummaryModel.cs`
- `Modules/Reporting/Reporting/Application/Providers/AppraisalSummaryLandBuildingDataProvider.cs`
- `Modules/Reporting/Reporting/Templates/appraisal-summary-land-building.html`
- `Modules/Reporting/Reporting/Templates/partials/approver-block.html`

### Files modified
- `Modules/Reporting/Reporting/ReportingModule.cs` — added 1 DI line

### Deferred fields
- AoName: no AO column in explored schema
- AppraisalCheckerName/Position: no checker role column on assignments
- AppraisalVerifyName/Position: no verify role column on assignments

### Column confirmations (trickiest)
- LandAppraisalDetails.Street = road column (confirmed in config builder.Property(e => e.Street))
- LandAppraisalDetails address VO flattens to SubDistrict/District/Province/LandOffice (HasColumnName)
- PricingAnalysisMethods.MethodType is a string ("WQS","SaleGrid","DirectComparison","BuildingCost","Income","Leasehold","ProfitRent","Hypothesis")
- CommitteeVotes.MemberRole is the position column (confirmed in CommitteeVoteConfiguration)
- ValuationAnalyses uses schema default = appraisal (no explicit schema on ToTable)
- Meetings is workflow.Meetings (confirmed in MeetingConfiguration — no schema prefix, uses workflow DbContext default)
- PropertyGroups columns (AppraisalId, GroupNumber, GroupName) via OwnsMany inline config in AppraisalAggregateConfiguration

