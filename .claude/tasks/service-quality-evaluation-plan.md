# Service Quality Evaluation — Implementation Plan

## Todo

- [x] 1. Domain entity: `AppraisalEvaluation.cs`
- [x] 2. EF config: `AppraisalEvaluationConfiguration.cs`
- [x] 3. Add `DbSet` to `AppraisalDbContext`
- [x] 4. SQL view: `vw_AppraisalEvaluationList.sql`
- [x] 5. Query: `DetectDeliveryTimeQuery` + handler
- [x] 6. Command: `CreateEvaluationCommand` + handler
- [x] 7. Command: `UpdateEvaluationCommand` + handler
- [x] 8. Query: `GetEvaluationListQuery` + handler
- [x] 9. Query: `GetEvaluationByAppraisalQuery` + handler
- [x] 10. Carter endpoints: `EvaluationEndpoints.cs`
- [x] 11. Add global using for new namespace (if needed)
- [x] 12. EF migration: `AddAppraisalEvaluation`
- [x] 13. `dotnet build` — fix any errors

## Notes
- Entity audit: inherited from `Entity<Guid>` (CreatedAt/By/UpdatedAt/By auto-stamped by interceptor)
- No `ITransactionalCommand` needed — direct `AppraisalDbContext.SaveChangesAsync` in handlers
- ReportReceivedDate: not on Appraisal entity — will use `a.CompletedAt` as surrogate or NULL for now (field doesn't exist)
- AppraisalValue: from `appraisal.ValuationAnalyses.AppraisedValue` (LEFT JOIN)
- ExternalAppraiserName: from the most recent External assignment that isn't Cancelled/Rejected
