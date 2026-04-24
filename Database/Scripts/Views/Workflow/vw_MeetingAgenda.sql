CREATE
OR ALTER
VIEW workflow.vw_MeetingAgenda
AS
SELECT m.Id    AS MeetingId,
       m.MeetingNo,
       m.Title AS MeetingTitle,
       CASE
           WHEN m.Status = 'InvitationSent'
               AND m.StartAt IS NOT NULL
               AND m.StartAt <= GETDATE()
               THEN 'InProgress'
           ELSE m.Status
           END AS MeetingStatus,
       m.StartAt,
       m.EndAt,
       mi.Id   AS MeetingItemId,
       mi.AppraisalId,
       mi.AppraisalNo,
       mi.AppraisalType,
       mi.FacilityLimit,
       mi.Kind,
       mi.AcknowledgementGroup,
       mi.ItemDecision,
       mi.DecisionAt,
       mi.DecisionBy,
       mi.DecisionReason,
       mi.AddedAt,
       -- ApplicantName is not directly stored on appraisal.Appraisals.
       -- Join to request.RequestCustomers via Appraisals.RequestId to get the customer name.
       rc.Name AS ApplicantName,
       -- GroupKey for UI grouping: Decision items group by AppraisalType; Acknowledgement items use a fixed bucket prefix.
       CASE
           WHEN mi.Kind = 'Decision' THEN mi.AppraisalType
           WHEN mi.Kind = 'Acknowledgement' THEN 'Ack_' + mi.AcknowledgementGroup
           END AS GroupKey
FROM workflow.Meetings m
         INNER JOIN workflow.MeetingItems mi
                    ON mi.MeetingId = m.Id
         LEFT JOIN appraisal.Appraisals a
                   ON a.Id = mi.AppraisalId
         LEFT JOIN (SELECT rc2.RequestId,
                           rc2.Name,
                           ROW_NUMBER() OVER (PARTITION BY rc2.RequestId ORDER BY rc2.Id) AS rn
                    FROM request.RequestCustomers rc2) rc ON rc.RequestId = a.RequestId AND rc.rn = 1
