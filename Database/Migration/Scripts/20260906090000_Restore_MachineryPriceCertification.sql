/*
  20260906090000_Restore_MachineryPriceCertification.sql

  Purpose : Undo the forced IsPriceCertified = 0 that 20260901090000_Backfill_MachineryRegistration
            FromOtherBlob.sql applied on its first run.

  Why     : That pass mirrored a domain invariant called NormalizePriceCertification(), which this
            release DELETES — do not go looking for it in the code. It held that a price could only
            be certified for a machine that was registered and not still being procured.
            The invariant is gone: certifying a price is the appraiser's decision alone. The
            forcing pass has been deleted from the backfill script, but DbUp journals one-time
            scripts by file name with no checksum, so that edit is a no-op on every database that
            already ran it. Those rows still read 0, which the machinery report prints as
            "(ไม่ประเมินมูลค่า)" and which drags down the derived ประเมินมูลค่า count.

  Rule    : Only rows the removed pass could have touched — unregistered, or under procurement
            (MachineStatus code '2'). Nothing else is considered. A 0 on any other row was set by
            a person and stays.

  Safety  : Re-running is harmless; the second run matches nothing because the column is already 1.
            The window in which an appraiser could deliberately set 0 on one of these machines
            opens with the same release this script ships in, so there is no human decision here
            to overwrite.

  Gap     : The predicate reads TODAY's registration and installation status, while the pass it
            undoes read the status as it was when the backfill ran. A machine that was zeroed then
            and has been registered since no longer matches, so it keeps IsPriceCertified = 0 —
            and nothing else will ever set it back, because the old invariant only ever forced 0.
            Widening the predicate is not safe either: after this release a 0 on such a row is a
            decision an appraiser is entitled to make. The count of rows left behind is printed
            below so it can be checked against the target database rather than assumed away; on a
            database where the backfill has not run yet (production at the time of writing) it is
            whatever a person chose, and nothing needs doing.
*/

SET NOCOUNT ON;

UPDATE appraisal.MachineryAppraisalDetails
SET IsPriceCertified = 1
WHERE IsPriceCertified = 0
  AND (RegistrationStatus = 0 OR InstallationStatus = N'2');

PRINT CONCAT('Restored IsPriceCertified on machines the removed invariant had zeroed: ', @@ROWCOUNT, ' row(s).');

-- Left behind on purpose: either an appraiser's own decision, or a machine the backfill zeroed
-- that has been registered since. Only a person can tell those apart, so report and stop.
DECLARE @Remaining int = (
    SELECT COUNT(*)
    FROM appraisal.MachineryAppraisalDetails
    WHERE IsPriceCertified = 0
);

PRINT CONCAT('Machines still not price-certified (review if unexpected): ', @Remaining, ' row(s).');
