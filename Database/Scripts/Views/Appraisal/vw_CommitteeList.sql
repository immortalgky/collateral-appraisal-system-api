CREATE
OR ALTER
VIEW appraisal.vw_CommitteeList AS
SELECT c.Id,
       c.CommitteeName,
       c.CommitteeCode,
       c.[Description],
       c.IsActive,
       c.QuorumType,
       c.QuorumValue,
       c.MajorityType,
       (SELECT COUNT(*)
        FROM appraisal.CommitteeMembers cm
        WHERE cm.CommitteeId = c.Id AND cm.IsActive = 1)                                             AS MemberCount,
       (SELECT COUNT(*) FROM appraisal.CommitteeApprovalConditions cac WHERE cac.CommitteeId = c.Id) AS ConditionCount,
       c.CreatedAt
FROM appraisal.Committees c
