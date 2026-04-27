# Quotation v2 Enhancement — Track A

## Domain
- [x] 1. QuotationRequestAppraisal join entity
- [x] 2. Modify QuotationRequest (drop AppraisalId scalar, add _appraisals, AddAppraisal, RemoveAppraisal, MarkAllResponsesReceived, FinalizedAckedAt)
- [x] 3. Modify CompanyQuotation (Declined status + Decline method + ResponseReceivedDomainEvent)

## EF + Migration
- [x] 4. QuotationRequestAppraisalConfiguration
- [x] 5. Update QuotationRequestConfiguration (drop AppraisalId, map collection, add FinalizedAckedAt)
- [x] 6. Update CompanyQuotationConfiguration (Declined in CK)
- [x] 7. EF migration AddQuotationMultiAppraisalAndDecline

## Integration Events
- [x] 8. QuotationAllResponsesReceivedIntegrationEvent
- [x] 9. AppraisalAddedToQuotationIntegrationEvent
- [x] 10. AppraisalRemovedFromQuotationIntegrationEvent
- [x] 11. QuotationInvitationDeclinedIntegrationEvent

## Application — modify existing
- [x] 12. StartQuotationFromTaskHandler — add ExistingQuotationRequestId support
- [x] 13. SubmitQuotationHandler — call MarkAllResponsesReceived after save
- [x] 14. QuotationFinalizedIntegrationEventHandler — publish one CompanyAssignedIE per appraisal
- [x] 15. FinalizeQuotationCommandHandler — publish per-appraisal events

## Application — new endpoints
- [x] 16. AddAppraisalToDraft (POST /quotations/{id}/appraisals)
- [x] 17. RemoveAppraisalFromDraft (DELETE /quotations/{id}/appraisals/{appraisalId})
- [x] 18. DeclineInvitation (POST /quotations/{id}/companies/{companyId}/decline)
- [x] 19. GetMyDraftsForAssembly (GET /quotations/drafts)
- [x] 20. AckFinalizedQuotation (POST /quotations/{id}/ack-finalized)

## Authorization
- [x] 21. Extend QuotationAccessPolicy — ExtCompany can decline
- [x] 22. DocumentAccessPolicy — ExtCompany read-only access to appraisal docs via invitation

## Read-side tweaks
- [x] 23. Update GetQuotationById result — appraisals array
- [x] 24. Update GetQuotations — filter by appraisalId works via join table; update vw_QuotationList
- [x] 25. Update CompanyQuotationDto/Result — Declined status visible
- [x] 26. Update repositories — GetFinalizedByAppraisalIdAsync works via join table

## Infrastructure
- [x] 27. Add QuotationRequestAppraisal to DbContext
- [x] 28. Update QuotationRepository — new method GetByIdWithAppraisalsAsync + update GetFinalizedByAppraisalId

## Review

### What was built

**Domain changes**
- `QuotationRequestAppraisal` join entity (composite PK). `QuotationRequest` drops the scalar `AppraisalId` property; now carries `_appraisals` collection + `AddAppraisal`/`RemoveAppraisal`/`MarkAllResponsesReceived`/`AckFinalized`/`FinalizedAckedAt`.
- `CompanyQuotation` gains `Declined` status, `Decline(reason, by)` method, `CreateDeclined(...)` static factory, and `DeclineReason`/`DeclinedAt`/`DeclinedBy` fields.

**EF + migration** `AddQuotationMultiAppraisalAndDecline`
- Drops `AppraisalId` column on `QuotationRequests`
- Adds `FinalizedAckedAt` to `QuotationRequests`
- Creates `QuotationRequestAppraisals` table with composite PK + cascade FK
- Adds `DeclineReason`/`DeclinedAt`/`DeclinedBy` to `CompanyQuotations`
- Updates `CK_CompanyQuotations_Status` to include `Declined`

**Integration events (4 new)**
- `QuotationAllResponsesReceivedIntegrationEvent`
- `AppraisalAddedToQuotationIntegrationEvent`
- `AppraisalRemovedFromQuotationIntegrationEvent`
- `QuotationInvitationDeclinedIntegrationEvent`
- `QuotationFinalizedIntegrationEvent` updated: `AppraisalId` scalar → `AppraisalIds[]` array (backward-compat `AppraisalId` property returns first element)

**Modified handlers**
- `StartQuotationFromTaskHandler`: new `ExistingQuotationRequestId?` path that AddAppraisal to an existing Draft; active-quotation uniqueness check on both paths; emits `AppraisalAddedToQuotationIntegrationEvent` on add-to-existing path.
- `SubmitQuotationCommandHandler`: now calls `MarkAllResponsesReceived()` and emits `QuotationAllResponsesReceivedIntegrationEvent` on early close.
- `FinalizeQuotationCommandHandler`: publishes `QuotationFinalizedIntegrationEvent` with `AppraisalIds[]`.
- `QuotationFinalizedIntegrationEventHandler` (new consumer): iterates appraisals, publishes one `CompanyAssignedIntegrationEvent` per appraisal.
- `GetQuotationByIdQueryHandler` / Result / Response: `Appraisals[]` + `FirstAppraisalId` + `FinalizedAckedAt`.
- `GetQuotationsQueryHandler`: `AppraisalId` filter uses EXISTS on join table.

**New endpoints**
- `POST /quotations/{id}/appraisals` (Admin) — `AddAppraisalToDraft`
- `DELETE /quotations/{id}/appraisals/{appraisalId}` (Admin) — `RemoveAppraisalFromDraft`
- `POST /quotations/{id}/companies/{companyId}/decline` (ExtAdmin) — `DeclineInvitation`
- `GET /quotations/drafts?bankingSegment=` (Admin) — `GetMyDraftsForAssembly`
- `POST /quotations/{id}/ack-finalized` (RM/Admin) — `AckFinalizedQuotation`

**Authorization**
- `QuotationAccessPolicy.EnsureExtCompanyUser()` — new helper for Decline/Submit paths
- `DocumentAccessPolicy` (new static class) — ExtCompany read-only access via active invitation check

**Repository**
- `QuotationRepository.HasActiveQuotationForAppraisalAsync()` — active-quotation uniqueness check
- `GetFinalizedByAppraisalIdAsync()` — updated to search via join table
- All `GetBy*` methods now include `.Include(q => q.Appraisals)`

### Follow-ups required
1. **Redeploy `vw_QuotationList.sql`** — the `AppraisalId` column was removed; FE queries that filter by `AppraisalId` via the view now use the EXISTS sub-query in the handler, but the view itself must be redeployed on the database server.
2. `CompanyAssignedIntegrationEventHandler.HandleQuotationAssignmentAsync()` calls `GetFinalizedByAppraisalIdAsync()` — this now searches via the join table; verify it resolves correctly in a multi-appraisal quotation scenario (one event per appraisal, each finds the same QuotationRequest — idempotency guard prevents duplicate fees).
3. `GetMyDraftsForAssembly` query is against `QuotationRequestItems` for preview — ensure `QuotationRequestItems` rows are always added alongside `QuotationRequestAppraisals` rows (done in both `StartQuotationFromTask` and `AddAppraisalToDraft`).
4. Track B (`GET /quotations/my-tasks`) is a separate handoff.
