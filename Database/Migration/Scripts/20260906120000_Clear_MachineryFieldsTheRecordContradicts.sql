/*
  20260906120000_Clear_MachineryFieldsTheRecordContradicts.sql

  Purpose : Empty the two machinery columns a record can contradict —
              RegistrationNumber on a machine that is not registered, and
              InvoiceNumber on a machine that is not under procurement (MachineStatus '2').

  Why     : The form disables both boxes in exactly those states, so the value is invisible to the
            appraiser while still sitting in the database, reaching the summary report and the
            outbound appraisal result. From this release the domain enforces the rule on every
            write (MachineryAppraisalDetail.ClearInapplicableFields), which covers new and edited
            rows; this script is what reaches the rows nobody opens again.

  Rule    : Only those two columns, only in those two states. IsPriceCertified is NOT touched —
            certifying a price is the appraiser's judgement, and the invariant that used to infer
            it from these same fields was removed on purpose
            (see 20260906090000_Restore_MachineryPriceCertification.sql).

  Safety  : Idempotent — after the first run nothing matches. Nothing else stores these numbers,
            so this is not recoverable: it is the deletion the rule asks for, not a copy of it.
*/

SET NOCOUNT ON;

UPDATE appraisal.MachineryAppraisalDetails
SET RegistrationNumber = NULL
WHERE RegistrationStatus = 0
  AND RegistrationNumber IS NOT NULL;

PRINT CONCAT('Cleared RegistrationNumber on unregistered machines: ', @@ROWCOUNT, ' row(s).');

UPDATE appraisal.MachineryAppraisalDetails
SET InvoiceNumber = NULL
WHERE ISNULL(InstallationStatus, N'') <> N'2'
  AND InvoiceNumber IS NOT NULL;

PRINT CONCAT('Cleared InvoiceNumber on machines that are not under procurement: ', @@ROWCOUNT, ' row(s).');
