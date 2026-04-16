namespace Workflow.Meetings.EventHandlers;

// TODO (Option B): An "AppraisalApprovedBySubCommittee" upstream event does not yet exist in the
// Appraisal module. The Appraisal domain currently has no domain event for committee approval
// (only AppraisalStatusChangedEvent, AppraisalCompletedEvent, AppraisalCreatedEvent, AppraisalAssignedEvent).
//
// Until the Appraisal module raises a suitable event (e.g. AppraisalApprovedBySubCommitteeIntegrationEvent
// via MassTransit), use the manual-insert endpoint in Phase 5:
//
//   POST /meetings/acknowledgement-queue
//   Body: { appraisalId, appraisalNo, appraisalDecisionId, committeeId, committeeCode }
//
// The endpoint will look up the committee code → AcknowledgementGroup via AcknowledgementGroupSettings
// and insert a new AppraisalAcknowledgementQueueItem with Status=PendingAcknowledgement.
//
// When the upstream event is available:
// 1. Create a MassTransit consumer here implementing IConsumer<AppraisalApprovedBySubCommitteeIntegrationEvent>.
// 2. Inject IOptions<AcknowledgementGroupSettings> to resolve committee code → group.
// 3. Insert AppraisalAcknowledgementQueueItem.Create(...) and save via IWorkflowUnitOfWork.
// 4. Remove the manual-insert endpoint or keep it as a fallback for edge cases.
