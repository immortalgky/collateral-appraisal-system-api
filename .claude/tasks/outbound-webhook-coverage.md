# Outbound Webhook Coverage — Task List

Plan: `/Users/gky/.claude/plans/analyze-the-workflow-module-atomic-blum.md`

## Tasks

- [ ] Task 1: Extend `IWebhookService` signature with `eventType`, `externalCaseKey`, `occurredAt`, `data`
- [ ] Task 2: Implement spec envelope in `WebhookService`
- [ ] Task 3: Add `AppraisalId` to `AppraisalCompletedIntegrationEvent`
- [ ] Task 4: Populate `AppraisalId` in `AppraisalCompletedEventHandler`
- [ ] Task 5: Add `AppraisalIds` to `ShortlistSentToRmIntegrationEvent`
- [ ] Task 6: Add `AppraisalId`, `ActivityCode`, `Movement` to `TaskAssignedIntegrationEvent`
- [ ] Task 7: Add `AppraisalId`, `ActivityCode` to `TransitionCompletedIntegrationEvent`
- [ ] Task 8: Add new `DocumentFollowupRequiredIntegrationEvent`
- [ ] Task 9: Publish `DocumentFollowupRequiredIntegrationEvent` from `DocumentFollowupNotificationHandlers`
- [ ] Task 10: Create `IntegrationStatusMap`
- [ ] Task 11: Create `AppraisalCreatedWebhookConsumer`
- [ ] Task 12: Create `QuotationReadyWebhookConsumer`
- [x] Task 13: Fix `TaskActivity` route-back reason propagation (singleton canonical vars + read fix in `PublishTaskAssignedEventAsync`) — **completed**
- [ ] Task 14: Create `AppraisalStatusChangedWebhookConsumer`
- [ ] Task 15: Create `AppraisalCompletedWebhookConsumer`
- [ ] Task 16: Create `DocumentFollowupRequiredWebhookConsumer`
- [ ] Task 17: Register all new consumers in `IntegrationModule`
