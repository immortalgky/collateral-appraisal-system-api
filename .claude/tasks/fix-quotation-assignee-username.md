# Fix Quotation Assignee Username

## Goal
Replace Guid strings in quotation workflow `AssignedTo` with usernames.

## Root Cause
1. `ResolveRmUserIdAsync` queries `SELECT Requestor` (= username string), then tries `Guid.TryParse()` — always fails → `RmUserId` always null on aggregate.
2. `SendQuotationCommandHandler` sends `StartedByUserId = currentUser.UserId?.ToString()` (Guid string).
3. Consumer forwards both into workflow variables → `AssignedTo` gets Guid strings.

## Todo

- [x] Read all relevant files (done above)
- [x] 1. Add `RmUsername string?` property to `QuotationRequest` aggregate + `CreateFromTask` factory
- [x] 2. (Not needed as separate step — set in factory and CreateFromTask call)
- [x] 3. Rename `QuotationStartedIntegrationEvent.StartedByUserId` → `StartedByUsername`; add `RmUsername string?`
- [x] 4. Fix `SendQuotationCommandHandler`: use `currentUser.Username` + read `RmUsername` from aggregate
- [x] 5. Fix `StartQuotationFromTaskCommandHandler.ResolveRmUserIdAsync` → `ResolveRmAsync` returning `(Guid?, string?)`
- [x] 6. Fix `QuotationStartedIntegrationEventConsumer`: use `StartedByUsername`, set `rmUserId` variable to `RmUsername`
- [x] 7. Add EF config for `RmUsername` in `QuotationRequestConfiguration`
- [x] 8. Add EF migration `AddRmUsernameToQuotationRequests`
- [x] 9. `dotnet build` — 0 errors

## Review

All 7 call sites corrected. Migration `20260422162626_AddRmUsernameToQuotationRequests` adds nullable `nvarchar(50)` column.

Key discovery: `ResolveRmUserIdAsync` was calling `Guid.TryParse()` on the `Requestor` column value, which holds an employee ID string (e.g. "EMP001"), not a Guid. This always returned null. Fixed by removing the Guid parse and returning the string directly as `RmUsername`.
