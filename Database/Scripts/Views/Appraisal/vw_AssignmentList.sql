CREATE
OR ALTER
VIEW appraisal.vw_AssignmentList AS
SELECT a.Id,
       a.AppraisalId,
       a.AssignmentType,
       a.AssignmentStatus,
       a.AssigneeUserId,
       a.AssigneeCompanyId,
       a.ExternalAppraiserName,
       a.AssignmentMethod,
       a.ReassignmentNumber,
       a.ProgressPercent,
       a.AssignedAt,
       a.AssignedBy,
       a.StartedAt,
       a.CompletedAt,
       a.RejectionReason,
       a.CancellationReason,
       a.CreatedAt
FROM appraisal.AppraisalAssignments a
WHERE a.AssignmentStatus != 'Rejected' AND a.AssignmentStatus != 'Cancelled'
