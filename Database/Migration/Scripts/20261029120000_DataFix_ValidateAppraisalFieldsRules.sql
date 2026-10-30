-- ============================================================
-- Data fix: set the ValidateAppraisalFields rules on both appraisal-execution activities
-- additionally, fix the appointmentInFuture rule to check for false (past) instead of true (future)
-- ============================================================

UPDATE [workflow].[ActivityProcessConfigurations]
SET [ParametersJson] = N'{"rules":[    {"fieldKey":"hasNoAppraisedValue","op":"Equals","value":"false","message":"Set an appraised value before proceeding."},    {"fieldKey":"selectedPricingMethodCount","op":"GreaterThan","value":"0","message":"Select at least one pricing method before proceeding."},    {"fieldKey":"appointmentInFuture","op":"Equals","value":"false","message":"The appointment date must be in the past."},    {"fieldKey":"propsMissingTitle","op":"Equals","value":"0","message":"Every land property needs a title number."},    {"fieldKey":"propsMissingLandOffice","op":"Equals","value":"0","message":"Every land property needs a land office."}  ],"mode":"AllMustPass"}' 
WHERE [ActivityName] IN ('ext-appraisal-execution', 'int-appraisal-execution')
  AND [ProcessorName] = 'ValidateAppraisalFields';
