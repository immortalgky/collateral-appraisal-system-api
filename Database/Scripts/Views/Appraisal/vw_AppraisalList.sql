CREATE
OR ALTER
VIEW appraisal.vw_AppraisalList AS
SELECT a.Id,
       a.AppraisalNumber,
       a.RequestId,
       a.Status,
       a.AppraisalType,
       a.Priority,
       a.SLADays,
       a.SLADueDate,
       a.SLAStatus,
       a.CreatedAt,
       (SELECT COUNT(*) FROM appraisal.AppraisalProperties ap WHERE ap.AppraisalId = a.Id) AS PropertyCount,
       la.AssigneeUserId,
       la.AssignmentStatus,
       la.AssignedAt                                                                       AS AssignedDate,
       ld.Province
FROM appraisal.Appraisals a
         LEFT JOIN (SELECT aa.AppraisalId,
                           aa.AssigneeUserId,
                           aa.AssignmentStatus,
                           aa.AssignedAt,
                           ROW_NUMBER() OVER (PARTITION BY aa.AppraisalId ORDER BY aa.AssignedAt DESC) AS rn
                    FROM appraisal.AppraisalAssignments aa) la ON la.AppraisalId = a.Id AND la.rn = 1
         LEFT JOIN (SELECT ap2.AppraisalId,
                           lad.Province,
                           ROW_NUMBER() OVER (PARTITION BY ap2.AppraisalId ORDER BY ap2.SequenceNumber) AS rn
                    FROM appraisal.AppraisalProperties ap2
                             JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap2.Id
                    WHERE lad.Province IS NOT NULL) ld ON ld.AppraisalId = a.Id AND ld.rn = 1
