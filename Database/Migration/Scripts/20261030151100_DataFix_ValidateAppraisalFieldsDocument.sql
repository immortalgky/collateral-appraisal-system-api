
-- ============================================================
-- Data fix: set the ValidateAppraisalFields rules on both appraisal-execution activities
-- additionally, fix the hasCompleteValuationReport rule to require a complete valuation report (D001) to be attached.
-- ============================================================

-- Execution
UPDATE [workflow].[ActivityProcessConfigurations]
SET [ParametersJson] = N'{"rules":[    {"fieldKey":"hasNoAppraisedValue","op":"Equals","value":"false","message":"Set an appraised value before proceeding."},    {"fieldKey":"selectedPricingMethodCount","op":"GreaterThan","value":"0","message":"Select at least one pricing method before proceeding."},    {"fieldKey":"appointmentInFuture","op":"Equals","value":"false","message":"The appointment date must be in the past."},    {"fieldKey":"hasCompleteValuationReport","op":"Equals","value":"true","message":"A Complete Valuation Report (D001) must be attached."},    {"fieldKey":"propsMissingTitle","op":"Equals","value":"0","message":"Every land property needs a title number."},    {"fieldKey":"propsMissingLandOffice","op":"Equals","value":"0","message":"Every land property needs a land office."}  ],"mode":"AllMustPass"}'
WHERE [ActivityName] IN ('ext-appraisal-execution', 'int-appraisal-execution')
  AND [ProcessorName] = 'ValidateAppraisalFields';

-- Offine Key in
UPDATE [workflow].[ActivityProcessConfigurations]
SET [ParametersJson] = N'{"rules":[    {"fieldKey":"hasNoAppraisedValue","op":"Equals","value":"false","message":"Set an appraised value before proceeding."},    {"fieldKey":"selectedPricingMethodCount","op":"GreaterThan","value":"0","message":"Select at least one pricing method before proceeding."},    {"fieldKey":"appointmentInFuture","op":"Equals","value":"false","message":"The appointment date must be in the past."},    {"fieldKey":"hasCompleteValuationReport","op":"Equals","value":"true","message":"A Complete Valuation Report (D001) must be attached."},    {"fieldKey":"propsMissingTitle","op":"Equals","value":"0","message":"Every land property needs a title number."},    {"fieldKey":"propsMissingLandOffice","op":"Equals","value":"0","message":"Every land property needs a land office."},    {"fieldKey":"externalCompanyRecorded","op":"Equals","value":"true","message":"Record the external appraisal company before proceeding."},    {"fieldKey":"externalAppraisalDateRecorded","op":"Equals","value":"true","message":"Record the appraisal date from the company''s book before proceeding."}  ],"mode":"AllMustPass"}'
WHERE [ActivityName] = 'int-offline-book-keyin'
  AND [ProcessorName] = 'ValidateAppraisalFields';

-- Checker
UPDATE [workflow].[ActivityProcessConfigurations]
SET [ParametersJson] = N'{"rules":[    {"fieldKey":"comparableCount","op":"GreaterOrEqual","value":"3","message":"Fewer than 3 market comparables were captured."},    {"fieldKey":"hasCompleteValuationReport","op":"Equals","value":"true","message":"A Complete Valuation Report (D001) must be attached."},    {"fieldKey":"photoCount","op":"GreaterOrEqual","value":"2","message":"Fewer than 2 photos were attached."}  ],"mode":"AllMustPass"}'
WHERE [ActivityName] IN ('int-appraisal-check', 'ext-appraisal-check')
  AND [ProcessorName] = 'ValidateAppraisalFields';


-- Verifier: update if the row exists, otherwise insert it
DECLARE @VerificationRules NVARCHAR(MAX) = N'{"rules":[    {"fieldKey":"hasCompleteValuationReport","op":"Equals","value":"true","message":"A Complete Valuation Report (D001) must be attached."}  ],"mode":"AllMustPass"}';

UPDATE [workflow].[ActivityProcessConfigurations]
SET [ParametersJson] = @VerificationRules
WHERE [ActivityName] IN ('int-appraisal-verification', 'ext-appraisal-verification')
  AND [ProcessorName] = 'ValidateAppraisalFields';

INSERT INTO [workflow].[ActivityProcessConfigurations]
    ([Id], [ActivityName], [StepName], [ProcessorName], [Kind], [Severity], [SortOrder],
     [ParametersJson], [RunIfExpression], [IsActive], [Version],
     [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy])
SELECT NEWID(), source.[ActivityName], 'Validate Appraisal Fields', 'ValidateAppraisalFields', 0, 0, source.[SortOrder],
       @VerificationRules, N'activity.movement === ''F''', 1, 1,
       GETDATE(), 'system', GETDATE(), 'system'
FROM (VALUES ('int-appraisal-verification', 3), ('ext-appraisal-verification', 2)) AS source ([ActivityName], [SortOrder])
WHERE NOT EXISTS (
    SELECT 1 FROM [workflow].[ActivityProcessConfigurations] AS target
    WHERE target.[ActivityName] = source.[ActivityName]
      AND target.[ProcessorName] = 'ValidateAppraisalFields');
