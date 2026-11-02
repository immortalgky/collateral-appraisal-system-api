-- Committee approval: exclude Conflicted members (spec api#626).
--
-- A committee member who completed one of these activities of an appraisal gets no approval task for it:
--   int-appraisal-execution       presenter (ผู้นำเสนอ)
--   appraisal-book-verification   presenter, book verification
--   int-offline-book-keyin        presenter, offline external book (skips appraisal-book-verification)
--   int-appraisal-check           section manager (ผู้จัดการส่วน)
--   int-appraisal-verification    assistant director (ผู้ช่วยผู้อำนวยการ)
-- ApprovalActivity reads the list from excludeAssigneesFrom on pending-approval, which admins edit on the
-- Task Assignment Config page. This script ships the initial value.
--
-- Idempotent, and never overwrites an admin's value:
--   1. an existing pending-approval row gets the list only where ExcludeAssigneesFrom is NULL ("inherit");
--      an empty list ("[]") is an admin switching the rule off, and is left alone;
--   2. an active wildcard row (no workflow definition, no banking segment) is inserted only if no active one
--      exists. Only active rows are read, so a leftover inactive row must not stop the rule; it is left as it is.
-- Approval never runs the assignment strategies, so the strategy columns are empty and change nothing.

DECLARE @ExcludeAssigneesFrom nvarchar(max) =
    N'["int-appraisal-execution","appraisal-book-verification","int-offline-book-keyin","int-appraisal-check","int-appraisal-verification"]';

UPDATE [workflow].[TaskAssignmentConfigurations]
SET [ExcludeAssigneesFrom] = @ExcludeAssigneesFrom,
    [UpdatedAt] = GETDATE(),
    [UpdatedBy] = N'system'
WHERE [ActivityId] = N'pending-approval'
  AND [ExcludeAssigneesFrom] IS NULL;

IF NOT EXISTS (SELECT 1
               FROM [workflow].[TaskAssignmentConfigurations]
               WHERE [ActivityId] = N'pending-approval'
                 AND [WorkflowDefinitionId] IS NULL
                 AND [BankingSegment] IS NULL
                 AND [IsActive] = 1)
BEGIN
    INSERT INTO [workflow].[TaskAssignmentConfigurations]
        ([Id], [ActivityId], [WorkflowDefinitionId], [PrimaryStrategies], [RouteBackStrategies],
         [AdminPoolId], [EscalateToAdminPool], [SpecificAssignee], [AssigneeGroup], [BankingSegment],
         [TeamConstrained], [ExcludeAssigneesFrom], [AdditionalConfiguration], [IsActive],
         [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
    VALUES
        (NEWID(), N'pending-approval', NULL, N'[]', N'[]',
         NULL, 0, NULL, NULL, NULL,
         NULL, @ExcludeAssigneesFrom, NULL, 1,
         GETDATE(), GETDATE(), N'system', N'system');
END
