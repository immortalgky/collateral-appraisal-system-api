# Auto-Enqueue AppraisalAcknowledgementQueueItem via Integration Event

## Goal
Replace the manual `POST /meetings/acknowledgement-queue` endpoint with an integration-event-driven flow.
The existing `AppraisalApprovedByCommitteeIntegrationEvent` is extended and consumed by a new MassTransit consumer on the Workflow side.

## Steps

- [x] 1. **Extend integration event** — add `AppraisalNo` + `CommitteeId` to `AppraisalApprovedByCommitteeIntegrationEvent`
- [x] 2. **Update publisher** — populate the two new fields in `ApprovalActivity.ResumeActivityAsync`
- [x] 3. **Make `AppraisalDecisionId` nullable** on `AppraisalAcknowledgementQueueItem` + `Create(...)` signature
- [x] 4. **Update EF configuration** — remove `.IsRequired()` from `AppraisalDecisionId`
- [x] 5. **Drop unique index on AppraisalDecisionId** — unique partial index becomes invalid when the column is nullable (NULL values are not equal in SQL Server partial index). Replace with a unique index on `(AppraisalId, CommitteeId)` active-only rows.
- [x] 6. **Create migration** `MakeAcknowledgementDecisionIdNullable`
- [x] 7. **Rename + implement consumer** — `AppraisalApprovedBySubCommitteeEventHandler.cs` → `AppraisalApprovedByCommitteeAckIntegrationEventConsumer.cs`
- [x] 8. **Delete manual endpoint** — remove `CreateAcknowledgementQueueItem/` folder
- [x] 9. **Write unit tests** — 4 consumer scenarios + 1 ApprovalActivity publish test

## Review
All steps implemented. See code changes for details.
