CREATE
OR ALTER
VIEW workflow.vw_UserTaskSummary
AS
-- Active tasks: one row per pending task, bucketed by TaskStatus.
-- Dated by AssignedAt so period filters group by when the task was assigned.
SELECT pt.AssignedTo AS Username,
       pt.Id         AS TaskId,
       pt.AssignedAt AS EventAt,
       CASE pt.TaskStatus
           WHEN 'Assigned'   THEN 'NotStarted'
           WHEN 'InProgress' THEN 'InProgress'
           WHEN 'Completing' THEN 'InProgress'
       END           AS Bucket
FROM workflow.PendingTasks pt
WHERE pt.TaskStatus IN ('Assigned', 'InProgress', 'Completing')

UNION ALL

-- Overdue is an overlapping flag on active tasks, not a status.
-- Emitted as an extra row so it can be counted independently of the status bucket.
SELECT pt.AssignedTo,
       pt.Id,
       pt.AssignedAt,
       'Overdue'
FROM workflow.PendingTasks pt
WHERE pt.SlaStatus = 'Breached'

UNION ALL

-- Completed tasks: separate table, dated by CompletedAt.
SELECT ct.AssignedTo,
       ct.Id,
       ct.CompletedAt,
       'Completed'
FROM workflow.CompletedTasks ct;
