CREATE OR ALTER VIEW workflow.vw_PendingAcknowledgement
AS
SELECT
    qi.Id,
    qi.AppraisalId,
    qi.AppraisalNo,
    qi.AppraisalDecisionId,
    qi.CommitteeId,
    qi.CommitteeCode,
    qi.AcknowledgementGroup,
    qi.Status,
    qi.MeetingId,
    qi.EnqueuedAt,
    -- Appraisal identifying columns (for Cut-Off preview UI)
    a.AppraisalType,
    a.FacilityLimit,
    a.RequestId
FROM workflow.AppraisalAcknowledgementQueueItems qi
         LEFT JOIN appraisal.Appraisals a
                   ON a.Id = qi.AppraisalId
WHERE qi.Status = 'PendingAcknowledgement'
