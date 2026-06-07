# Task: Appraisal Summary Condo & Machine PDF Reports

## Plan

### Files to create
1. `Modules/Reporting/Reporting/Application/Providers/AppraisalSummaryCommonLoader.cs` — shared common queries helper
2. `Modules/Reporting/Reporting/Application/Providers/AppraisalSummaryCondoDataProvider.cs` — Condo report provider
3. `Modules/Reporting/Reporting/Application/Providers/AppraisalSummaryMachineDataProvider.cs` — Machine report provider
4. `Modules/Reporting/Reporting/Templates/appraisal-summary-condo.html` — Condo HTML template
5. `Modules/Reporting/Reporting/Templates/appraisal-summary-machine.html` — Machine HTML template
6. Edit `ReportingModule.cs` — add 2 DI registrations

### Key column mapping notes
- CondoAppraisalDetails: RoomNumber, FloorNumber, NumberOfFloors, CondoName, BuiltOnTitleNumber (= TitleNumber for report),
  CondoRegistrationNumber, UsableArea, BuildingConditionType/Other, BuildingAge, OwnerName, ObligationDetails,
  Soi, Street (Road), SubDistrict/District/Province (via Address VO, columns with HasColumnName),
  LandOffice (Address VO), Latitude/Longitude (Coordinates VO), BuildingInsurancePrice, ForcedSalePrice, SellingPrice
- CondoAppraisalAreaDetails: FK column "CondoAppraisalDetailsId", AreaDescription, AreaSize
- MachineryAppraisalSummaries: no schema override (inherits appraisal schema from DbContext convention? check ToTable call)
  Wait — MachineryAppraisalSummaryConfiguration.cs has `builder.ToTable("MachineryAppraisalSummaries")` — no schema!
  Need to check the default schema. Check DbContext.
- MachineryAppraisalDetails: RegistrationNumber, Brand, Model, Series, EngineNo (EngineNo col), ChassisNo, SerialNo,
  Manufacturer, YearOfManufacture, MachineAge (decimal), ReplacementValue, ConditionValue, MachineCondition,
  OwnerName, Location, MachineName, Quantity
- MachineType for model → MachineryAppraisalSummary.InIndustrial (machine category)
- MarketDemandConditions → MachineryAppraisalSummary.MarketDemand
- Machine GPS → MachineryAppraisalSummary.Latitude/Longitude
- Machine Obligation → MachineryAppraisalSummary.Obligation

## Todo
- [x] Read all source files
- [ ] Verify MachineryAppraisalSummaries schema
- [ ] Create CommonLoader
- [ ] Create CondoDataProvider
- [ ] Create MachineDataProvider
- [ ] Create Condo template
- [ ] Create Machine template
- [ ] Update ReportingModule.cs DI
- [ ] dotnet build 0 errors
