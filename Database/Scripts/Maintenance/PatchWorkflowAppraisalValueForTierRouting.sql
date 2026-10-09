-- =============================================================================
-- PatchWorkflowAppraisalValueForTierRouting.sql
--
-- PURPOSE
--   Write the appraisal's authoritative appraised value into the workflow
--   instance variable 'appraisalValue', so committee-tier routing sees it.
--
-- WHY THIS EXISTS
--   Two routing decisions read ONE workflow variable, 'appraisalValue':
--     1. activity 'approval-tier-switch' (SwitchActivity)
--          case '<= 30000000' -> pending-approval  (no meeting)
--          case '>  30000000' -> pending-meeting
--     2. activity 'pending-approval' (ApprovalActivity), memberSource of type
--        'threshold' -> ApprovalMemberResolver.ResolveFromThreshold:
--          <= 9999999.99  SUB_COMMITTEE
--          <= 30000000    COMMITTEE
--          else           COMMITTEE_WITH_MEETING
--
--   That variable is pushed ONLY by AppraisalValueChangedIntegrationEventConsumer,
--   fed by AppraisalValuationSummaryService.RecomputeAsync, and only when the
--   total actually changes. A block/project appraisal whose unit prices were
--   imported with PatchProjectUnitPricesFromApprovedList.sql never runs that C#
--   path, so the variable is never written at all.
--
--   SwitchActivity.EvaluateCaseCondition deliberately coerces a missing value to
--   0m ("fail safe to the lowest tier"). For a money threshold that is a silent
--   DOWNGRADE: 0 matches '<= 30000000' (no meeting) AND 0 <= 9999999.99
--   (SUB_COMMITTEE). A multi-billion project therefore routes to sub-committee
--   with no meeting, and nothing in the logs says anything is wrong.
--
-- WHAT IT WRITES / DOES NOT WRITE
--   Writes : workflow.WorkflowInstances.Variables   ($.appraisalValue only)
--            workflow.MeetingQueueItems.AppraisalValue  (live rows only)
--   Reads  : appraisal.Appraisals, appraisal.ValuationAnalyses,
--            appraisal.Projects / ProjectModels / ProjectUnits / ProjectUnitPrices,
--            appraisal.PricingAnalysis, appraisal.PropertyGroups,
--            workflow.WorkflowDefinitionVersions, workflow.WorkflowActivityExecutions
--   NEVER writes ProjectUnitPrices, ProjectUnits, PricingAnalysis or
--   ValuationAnalyses - hand-patched approved-list prices are untouched.
--
-- OVERRIDE
--   @OverrideAppraisalValue forces a specific routing value when the real
--   appraised value is still 0 (typically a block project whose approved unit
--   prices have not been keyed yet) but the appraisal is known to need a
--   committee meeting.
--   ValuationAnalyses is never written, so the appraisal's own value, its
--   reports, the AS400 feeds and the collateral master all keep saying 0.
--   The stand-in figure IS visible in two places, both fed by the workflow
--   variable: the meeting queue / meeting screens (MeetingActivity copies it
--   onto MeetingQueueItem.AppraisalValue) and the approval-tier decision itself.
--   So the committee will convene on an appraisal whose own pages read 0.
--   The next RecomputeAsync overwrites the variable with the real value. Every
--   report line and the final message say loudly when an override was used.
--
-- IDEMPOTENT: re-running writes the same value. @Apply = 0 touches nothing, and
--   its report predicts exactly what @Apply = 1 would store.
--
-- LIMITS
--   * This is a snapshot, exactly like the event it stands in for. If the
--     appraised value changes again before the instance reaches
--     approval-tier-switch, re-run this script.
--   * Version-pinned. WorkflowEngine loads the schema by
--     WorkflowInstances.WorkflowDefinitionVersionId, not by the definition's
--     latest version. Workflow versions 1-3 route on 'facilityLimit'; version 4
--     (published 2026-07-23) switched both decisions to 'appraisalValue'. This
--     script refuses a v1-v3 instance rather than writing a variable that
--     nothing on that version reads.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- == Inputs ===================================================================
DECLARE @AppraisalNumber nvarchar(50) = N'';   -- <<< fill in before running
DECLARE @Apply           bit          = 0;     -- 0 = report only, 1 = write

-- ESCAPE HATCH. Leave NULL for normal use: the value comes from
-- appraisal.ValuationAnalyses.AppraisedValue, the source of truth.
--
-- Set a number ONLY to force a tier when the real appraised value cannot drive
-- it - typically a block project whose approved unit prices have not been keyed
-- yet, so AppraisedValue is still 0 but the appraisal is known to need a
-- committee meeting. The number written here is a STAND-IN, not a measurement:
--   * it is NOT written back to ValuationAnalyses, so the appraisal's own pages,
--     its reports, the AS400 feeds and the collateral master keep reading 0;
--   * it IS visible on the meeting queue and meeting screens, which take their
--     figure from this same workflow variable - so the committee sees the
--     stand-in while the appraisal itself still says 0;
--   * it is FRAGILE. The next RecomputeAsync (any pricing save, property
--     delete, or the Unit Price tab's Calculate button) publishes the real
--     value and overwrites it. If that happens before the instance reaches
--     approval-tier-switch, the override is gone and the tier drops back.
-- Re-run this script after any such save, and prefer keying the real prices.
DECLARE @OverrideAppraisalValue decimal(18,2) = NULL;

-- Committee bands. Source: appraisal-workflow.json, activity 'pending-approval',
-- properties.memberSource.thresholds. Deliberately NOT read from
-- workflow.CommitteeThresholds: that table has no runtime reader (see
-- Database/Migration/Scripts/20260730130000_UpdateSeed_CommitteeThresholdBands.sql)
-- and its numbers only coincidentally match - workflow version 1 disagrees.
DECLARE @SubMax        decimal(18,2) = 9999999.99;
DECLARE @CommMax       decimal(18,2) = 30000000.00;
-- Meeting cutoff. Source: activity 'approval-tier-switch', properties.cases.
DECLARE @MeetingCutoff decimal(18,2) = 30000000.00;

IF @AppraisalNumber IS NULL OR LTRIM(RTRIM(@AppraisalNumber)) = N''
    THROW 50001, 'Set @AppraisalNumber at the top of this script before running it.', 1;

PRINT '=============================================================';
PRINT CONCAT('Target appraisal : ', @AppraisalNumber);
PRINT CONCAT('Mode             : ',
             CASE WHEN @Apply = 1 THEN 'APPLY (writes)' ELSE 'REPORT ONLY (no writes)' END);
PRINT '=============================================================';

-- == 1. Resolve the appraisal =================================================
DECLARE @AppraisalId   uniqueidentifier,
        @RequestId     uniqueidentifier,
        @FacilityLimit decimal(18,2),
        @AppraisalType nvarchar(50),
        @ApprStatus    nvarchar(50);

SELECT @AppraisalId   = a.Id,
       @RequestId     = a.RequestId,
       @FacilityLimit = a.FacilityLimit,
       @AppraisalType = a.AppraisalType,
       @ApprStatus    = a.Status
FROM appraisal.Appraisals a
WHERE a.AppraisalNumber = @AppraisalNumber
  AND a.IsDeleted = 0;

IF @AppraisalId IS NULL
BEGIN
    PRINT 'ABORT: no non-deleted appraisal with that number.';
    RETURN;
END

PRINT CONCAT('AppraisalId      : ', CONVERT(varchar(36), @AppraisalId));
PRINT CONCAT('RequestId        : ', CONVERT(varchar(36), @RequestId));
PRINT CONCAT('Type / Status    : ', @AppraisalType, ' / ', @ApprStatus);
PRINT CONCAT('FacilityLimit    : ', CONVERT(varchar(40), @FacilityLimit));

-- == 2. Resolve the workflow instance ==========================================
-- CorrelationId is the REQUEST id (not the appraisal id), stored as a LOWERCASE
-- string. CONVERT(varchar(36), <uniqueidentifier>) yields UPPERCASE, so the
-- LOWER() below is load-bearing: without it this matches nothing, silently.
DECLARE @InstanceId    uniqueidentifier,
        @InstanceCount int,
        @WfStatus      nvarchar(50),
        @CurrentAct    nvarchar(200),
        @VersionId     uniqueidentifier,
        @VersionNo     int;

DECLARE @Correlation varchar(36) = LOWER(CONVERT(varchar(36), @RequestId));

SELECT @InstanceCount = COUNT(*)
FROM workflow.WorkflowInstances wi
WHERE wi.CorrelationId = @Correlation;

IF @InstanceCount = 0
BEGIN
    PRINT 'ABORT: no workflow instance for this request.';
    RETURN;
END

IF @InstanceCount > 1
BEGIN
    PRINT CONCAT('ABORT: ', @InstanceCount,
                 ' workflow instances share this CorrelationId. Resolve by hand.');
    SELECT wi.Id, wi.Status, wi.CurrentActivityId, wi.StartedOn
    FROM workflow.WorkflowInstances wi
    WHERE wi.CorrelationId = @Correlation;
    RETURN;
END

SELECT @InstanceId = wi.Id,
       @WfStatus   = wi.Status,
       @CurrentAct = wi.CurrentActivityId,
       @VersionId  = wi.WorkflowDefinitionVersionId
FROM workflow.WorkflowInstances wi
WHERE wi.CorrelationId = @Correlation;

SELECT @VersionNo = v.Version
FROM workflow.WorkflowDefinitionVersions v
WHERE v.Id = @VersionId;

PRINT CONCAT('WorkflowInstance : ', CONVERT(varchar(36), @InstanceId));
PRINT CONCAT('Instance status  : ', @WfStatus, '  @ ', ISNULL(@CurrentAct, '(none)'));
PRINT CONCAT('Pinned version   : ', ISNULL(CONVERT(varchar(10), @VersionNo), '(unknown)'));

-- == 3. Which variable does THIS instance's pinned version route on? ===========
DECLARE @Schema     nvarchar(max),
        @SwitchExpr nvarchar(200),
        @ThreshExpr nvarchar(200);

SELECT @Schema = v.JsonSchema
FROM workflow.WorkflowDefinitionVersions v
WHERE v.Id = @VersionId;

IF @Schema IS NULL
BEGIN
    PRINT 'ABORT: the pinned workflow definition version has no JsonSchema.';
    RETURN;
END

-- Read the expressions by JSON path rather than by string search: the stored
-- definition is minified and HTML-escapes '<', '>' and apostrophes, so a LIKE
-- on '<= 30000000' would not match what is actually in the column.
SELECT TOP 1 @SwitchExpr = JSON_VALUE(a.value, '$.properties.expression')
FROM OPENJSON(@Schema, '$.activities') a
WHERE JSON_VALUE(a.value, '$.id') = 'approval-tier-switch';

SELECT TOP 1 @ThreshExpr = JSON_VALUE(a.value, '$.properties.memberSource.valueExpression')
FROM OPENJSON(@Schema, '$.activities') a
WHERE JSON_VALUE(a.value, '$.id') = 'pending-approval';

PRINT CONCAT('Switch expression: ', ISNULL(@SwitchExpr, '(not found)'));
PRINT CONCAT('Threshold expr   : ', ISNULL(@ThreshExpr, '(not found)'));

IF @SwitchExpr IS NULL OR @ThreshExpr IS NULL
BEGIN
    PRINT 'ABORT: could not read the routing expressions from the pinned version.';
    RETURN;
END

IF @SwitchExpr <> @ThreshExpr
BEGIN
    PRINT 'ABORT: the meeting switch and the committee threshold read DIFFERENT';
    PRINT '       variables on this version. Patching one would leave the two';
    PRINT '       decisions disagreeing. Inspect the definition first.';
    RETURN;
END

IF @SwitchExpr <> N'appraisalValue'
BEGIN
    PRINT '-------------------------------------------------------------';
    PRINT CONCAT('ABORT: this instance routes on ''', @SwitchExpr, ''', not on appraised value.');
    PRINT CONCAT('       FacilityLimit = ', CONVERT(varchar(40), @FacilityLimit),
                 ' is what decides its committee tier.');
    PRINT '       Writing appraisalValue would change nothing.';
    PRINT '       To route this appraisal on appraised value, migrate the instance';
    PRINT '       to workflow version 4 or later first (MigrateInstancesEndpoint),';
    PRINT '       then re-run this script. That is a business decision - ask first.';
    PRINT '-------------------------------------------------------------';
    RETURN;
END

-- == 4. The value to write ====================================================
-- Normal path: read the source of truth. Never hardcode a figure here.
-- Override path: use @OverrideAppraisalValue verbatim - see the input comment.
DECLARE @NewValue        decimal(18,2),
        @StoredValuation decimal(18,2),
        @ValueSource     nvarchar(40),
        @IsOverride      bit = CASE WHEN @OverrideAppraisalValue IS NULL THEN 0 ELSE 1 END;

SELECT @StoredValuation = va.AppraisedValue
FROM appraisal.ValuationAnalyses va
WHERE va.AppraisalId = @AppraisalId;

IF @IsOverride = 1
BEGIN
    IF @OverrideAppraisalValue <= 0
    BEGIN
        PRINT 'ABORT: @OverrideAppraisalValue must be greater than 0.';
        RETURN;
    END

    SET @NewValue    = @OverrideAppraisalValue;
    SET @ValueSource = N'OVERRIDE (manual)';

    PRINT '';
    PRINT '*************************************************************';
    PRINT '*  OVERRIDE MODE - the value below is NOT the appraised value';
    PRINT CONCAT('*  Override value    : ', CONVERT(varchar(40), @OverrideAppraisalValue));
    PRINT CONCAT('*  ValuationAnalyses : ',
                 ISNULL(CONVERT(varchar(40), @StoredValuation), '(no row)'));
    PRINT '*  It is NOT written back to ValuationAnalyses: the appraisal,';
    PRINT '*  its reports and the AS400 feeds keep reading the value above.';
    PRINT '*  It IS shown on the meeting queue and meeting screens, which';
    PRINT '*  read this same workflow variable - the committee will convene';
    PRINT '*  on an appraisal whose own pages still say 0.';
    PRINT '*  The next RecomputeAsync (any pricing save) overwrites it.';
    PRINT '*  Re-run this script if that happens before the switch runs.';
    PRINT '*************************************************************';
    PRINT '';
END
ELSE
BEGIN
    IF @StoredValuation IS NULL
    BEGIN
        PRINT 'ABORT: no appraisal.ValuationAnalyses row - there is no appraised value yet.';
        PRINT '       To force a tier anyway, set @OverrideAppraisalValue at the top.';
        RETURN;
    END

    IF @StoredValuation <= 0
    BEGIN
        PRINT CONCAT('ABORT: AppraisedValue is ', CONVERT(varchar(40), @StoredValuation),
                     '. Refusing to write a non-positive routing value.');
        PRINT '       To force a tier anyway, set @OverrideAppraisalValue at the top.';
        RETURN;
    END

    SET @NewValue    = @StoredValuation;
    SET @ValueSource = N'appraisal.ValuationAnalyses';
END

-- == 5. Cross-check the stored total against a freshly computed rollup =========
-- Mirrors AppraisalValuationSummaryService.RecomputeAsync. Block is detected the
-- same way the service detects it: the presence of an appraisal.Projects row.
-- In override mode this is informational only - the whole point of an override
-- is that the routing value is deliberately not the computed one.
DECLARE @IsBlock      bit,
        @Recomputed   decimal(18,2),
        @UnitsTotal   int = 0,
        @UnitsNoModel int = 0,
        @UnitsSold    int = 0;

SET @IsBlock = CASE WHEN EXISTS (SELECT 1 FROM appraisal.Projects p
                                 WHERE p.AppraisalId = @AppraisalId)
                    THEN 1 ELSE 0 END;

IF @IsBlock = 1
BEGIN
    -- Unsold-unit rollup. The INNER JOIN on ProjectModels is intentional: it is
    -- what the application does, so this reproduces the app rather than
    -- correcting it. A unit with a NULL ProjectModelId contributes nothing
    -- however correct its price row is - counted separately below so it shows.
    SELECT @Recomputed = SUM(ISNULL(pup.TotalAppraisalValueRounded, 0))
    FROM appraisal.ProjectUnits pu
    JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = pu.Id
    JOIN appraisal.ProjectModels     pm  ON pm.Id = pu.ProjectModelId
    JOIN appraisal.Projects          p   ON p.Id = pm.ProjectId
    WHERE p.AppraisalId = @AppraisalId
      AND pu.IsSold = 0;

    SELECT @UnitsTotal   = COUNT(*),
           @UnitsNoModel = SUM(CASE WHEN pu.ProjectModelId IS NULL THEN 1 ELSE 0 END),
           @UnitsSold    = SUM(CASE WHEN pu.IsSold = 1 THEN 1 ELSE 0 END)
    FROM appraisal.ProjectUnits pu
    JOIN appraisal.Projects p ON p.Id = pu.ProjectId
    WHERE p.AppraisalId = @AppraisalId;

    PRINT CONCAT('Block project    : yes  (units ', @UnitsTotal,
                 ', no ProjectModelId ', @UnitsNoModel,
                 ', sold ', @UnitsSold, ')');
    IF @UnitsNoModel > 0
        PRINT '  ^ units without a ProjectModelId are EXCLUDED from the rollup by the app.';
END
ELSE
BEGIN
    -- PropertyGroup path: sum of the groups' final appraised values.
    -- SubjectType is an int enum (PricingAnalysisSubjectType.PropertyGroup = 0).
    SELECT @Recomputed = SUM(ISNULL(pa.FinalAppraisedValue, 0))
    FROM appraisal.PricingAnalysis pa
    JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
    WHERE pg.AppraisalId = @AppraisalId
      AND pa.SubjectType = 0;

    PRINT 'Block project    : no  (PropertyGroup pricing)';
END

SET @Recomputed = ISNULL(@Recomputed, 0);

PRINT CONCAT('ValuationAnalyses: ',
             ISNULL(CONVERT(varchar(40), @StoredValuation), '(no row)'));
PRINT CONCAT('Recomputed total : ', CONVERT(varchar(40), @Recomputed));
PRINT CONCAT('Value to write   : ', CONVERT(varchar(40), @NewValue),
             '   [', @ValueSource, ']');

IF @IsOverride = 0 AND @Recomputed <> @NewValue
BEGIN
    PRINT '-------------------------------------------------------------';
    PRINT CONCAT('ABORT: ValuationAnalyses is out of step with the pricing rows by ',
                 CONVERT(varchar(40), @NewValue - @Recomputed), '.');
    PRINT '       Patching the workflow with a stale total would route on the';
    PRINT '       wrong figure. Refresh the summary first, then re-run.';
    PRINT '-------------------------------------------------------------';
    RETURN;
END

-- == 6. Is the routing decision live right now? ================================
IF @WfStatus IN (N'Completed', N'Cancelled', N'Failed')
BEGIN
    PRINT CONCAT('ABORT: workflow instance is ', @WfStatus, '. Nothing left to route.');
    RETURN;
END

-- Sitting ON the decision - or past it, awaiting a meeting or committee votes -
-- means the tier and the committee roster are already resolved and snapshotted
-- on the activity, so writing the variable now re-routes nothing.
IF @CurrentAct IN (N'approval-tier-switch', N'pending-meeting', N'pending-approval')
BEGIN
    PRINT '-------------------------------------------------------------';
    PRINT CONCAT('ABORT: the instance is sitting at ', @CurrentAct, '.');
    PRINT '       The tier and the committee roster were resolved when the switch';
    PRINT '       ran and are snapshotted on the activity, so writing the variable';
    PRINT '       now re-routes nothing.';
    PRINT '       Either use the recall path (transition approval-recall-to-meeting),';
    PRINT '       or route the task back so the switch runs again, then re-run this.';
    PRINT '-------------------------------------------------------------';
    SELECT e.ActivityId, e.Status, e.StartedOn, e.CompletedOn,
           SwitchValue = JSON_VALUE(e.OutputData, '$.expressionResult'),
           MatchedCase = JSON_VALUE(e.OutputData, '$.case'),
           Committee   = JSON_VALUE(e.OutputData, '$.pending_approval_committeeCode')
    FROM workflow.WorkflowActivityExecutions e
    WHERE e.WorkflowInstanceId = @InstanceId
      AND e.ActivityId IN (N'approval-tier-switch', N'pending-meeting', N'pending-approval')
    ORDER BY e.StartedOn;
    RETURN;
END

-- An earlier pass may already have routed this appraisal at the wrong tier and
-- then been routed back. That is recoverable, not a blocker: the instance is
-- upstream again and will re-enter approval-tier-switch, which reads whatever
-- this script writes. Show what happened rather than hiding it.
IF EXISTS (SELECT 1
           FROM workflow.WorkflowActivityExecutions e
           WHERE e.WorkflowInstanceId = @InstanceId
             AND e.ActivityId = N'approval-tier-switch')
BEGIN
    PRINT '';
    PRINT 'WARNING: this instance has passed approval-tier-switch before (below).';
    PRINT '         It is upstream again now, so the switch WILL run once more and';
    PRINT '         will read the patched value.';
    SELECT e.ActivityId, e.Status, e.StartedOn, e.CompletedOn,
           SwitchValue = JSON_VALUE(e.OutputData, '$.expressionResult'),
           MatchedCase = JSON_VALUE(e.OutputData, '$.case'),
           Committee   = JSON_VALUE(e.OutputData, '$.pending_approval_committeeCode')
    FROM workflow.WorkflowActivityExecutions e
    WHERE e.WorkflowInstanceId = @InstanceId
      AND e.ActivityId IN (N'approval-tier-switch', N'pending-meeting', N'pending-approval')
    ORDER BY e.StartedOn;
END

-- == 7. Report: what is stored now vs. what this would store ===================
DECLARE @StoredRaw nvarchar(100) = JSON_VALUE(
            (SELECT wi.Variables FROM workflow.WorkflowInstances wi WHERE wi.Id = @InstanceId),
            '$.appraisalValue');
DECLARE @StoredVal decimal(18,2) = TRY_CONVERT(decimal(18,2), @StoredRaw);
-- Absent and 0 are different to the engine but route identically, so the report
-- shows the raw JSON reading (NULL = key absent) alongside the effective value.
DECLARE @EffectiveNow decimal(18,2) = ISNULL(@StoredVal, 0);

SELECT
    AppraisalNumber      = @AppraisalNumber,
    PinnedVersion        = @VersionNo,
    RoutingExpression    = @SwitchExpr,
    FacilityLimit        = @FacilityLimit,
    ValueSource          = @ValueSource,          -- ValuationAnalyses or OVERRIDE
    AppraisedValue       = @StoredValuation,      -- what the appraisal actually says
    StoredInWorkflow     = @StoredRaw,            -- NULL = key absent
    NewAppraisalValue    = @NewValue,
    CurrentRouting       = CASE WHEN @EffectiveNow > @MeetingCutoff
                                THEN N'via meeting' ELSE N'direct to committee' END,
    NewRouting           = CASE WHEN @NewValue > @MeetingCutoff
                                THEN N'via meeting' ELSE N'direct to committee' END,
    CurrentCommittee     = CASE WHEN @EffectiveNow <= @SubMax  THEN N'SUB_COMMITTEE'
                                WHEN @EffectiveNow <= @CommMax THEN N'COMMITTEE'
                                ELSE N'COMMITTEE_WITH_MEETING' END,
    NewCommittee         = CASE WHEN @NewValue <= @SubMax  THEN N'SUB_COMMITTEE'
                                WHEN @NewValue <= @CommMax THEN N'COMMITTEE'
                                ELSE N'COMMITTEE_WITH_MEETING' END,
    WillChangeRouting    = CASE WHEN (CASE WHEN @EffectiveNow > @MeetingCutoff THEN 1 ELSE 0 END)
                                   = (CASE WHEN @NewValue     > @MeetingCutoff THEN 1 ELSE 0 END)
                                THEN N'no' ELSE N'YES' END,
    AlreadyCorrect       = CASE WHEN @StoredVal = @NewValue THEN N'yes' ELSE N'no' END;

-- Live meeting-queue rows whose displayed value this would also refresh.
SELECT q.Id, q.AppraisalNo, q.Status,
       CurrentQueueValue = q.AppraisalValue,
       NewQueueValue     = @NewValue
FROM workflow.MeetingQueueItems q
WHERE q.AppraisalId = @AppraisalId
  AND q.Status <> N'Released';

IF @StoredVal = @NewValue
    PRINT 'Note: the workflow variable already holds this exact value.';

IF @Apply = 0
BEGIN
    PRINT '';
    PRINT 'REPORT ONLY - nothing was written. Set @Apply = 1 to apply.';
    RETURN;
END

-- == 8. Apply ==================================================================
DECLARE @RowsInstance int, @RowsQueue int;

BEGIN TRANSACTION;

    -- JSON_MODIFY with a decimal-typed value emits a BARE JSON NUMBER
    -- ("appraisalValue":4803838000.00), matching exactly what
    -- AppraisalValueChangedIntegrationEventConsumer writes.
    -- Do NOT cast to float  -> "4.803838000000000e+009"
    -- Do NOT pass a string  -> "4803838000.00" (quoted), which the
    -- SwitchActivity numeric comparison will not match.
    UPDATE workflow.WorkflowInstances
    SET Variables = JSON_MODIFY(Variables, '$.appraisalValue', @NewValue)
    WHERE Id = @InstanceId;

    SET @RowsInstance = @@ROWCOUNT;

    -- Mirrors the consumer's second effect: keep any live queue row's displayed
    -- value current. Released rows are historical and are left alone.
    UPDATE workflow.MeetingQueueItems
    SET AppraisalValue = @NewValue
    WHERE AppraisalId = @AppraisalId
      AND Status <> N'Released'
      AND (AppraisalValue IS NULL OR AppraisalValue <> @NewValue);

    SET @RowsQueue = @@ROWCOUNT;

COMMIT TRANSACTION;

PRINT CONCAT('Updated WorkflowInstances : ', @RowsInstance, ' row(s)');
PRINT CONCAT('Updated MeetingQueueItems : ', @RowsQueue, ' row(s)');

-- == 9. Verify what actually landed ============================================
SELECT
    InstanceId  = wi.Id,
    StoredNow   = JSON_VALUE(wi.Variables, '$.appraisalValue'),
    RawFragment = SUBSTRING(wi.Variables,
                            CHARINDEX('"appraisalValue"', wi.Variables), 45)
FROM workflow.WorkflowInstances wi
WHERE wi.Id = @InstanceId;

PRINT '';
PRINT 'Check RawFragment reads   "appraisalValue":<number>   with NO quotes';
PRINT 'around the number. A quoted value will not compare numerically in the';
PRINT 'approval-tier switch.';

IF @IsOverride = 1
BEGIN
    PRINT '';
    PRINT '*************************************************************';
    PRINT '*  An OVERRIDE value was written. appraisal.ValuationAnalyses';
    PRINT CONCAT('*  still says ',
                 ISNULL(CONVERT(varchar(40), @StoredValuation), '(no row)'),
                 ' and was NOT modified.');
    PRINT '*  The meeting queue and meeting screens will show the override;';
    PRINT '*  the appraisal pages, reports and AS400 feeds will not.';
    PRINT '*  The next pricing save republishes the real value over this';
    PRINT '*  one. Drive the appraisal to approval-tier-switch before any';
    PRINT '*  further pricing edit, or re-run this script afterwards.';
    PRINT '*************************************************************';
END
