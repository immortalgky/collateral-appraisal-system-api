using Appraisal.Application.Features.Appraisals.AddAppraisalDocument;
using Appraisal.Application.Features.Appraisals.RemoveAppraisalDocument;
using Dapper;
using Document.Contracts;
using Hangfire;
using Reporting.Contracts;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Services;

/// <summary>
/// Hangfire job: renders the post-approval Appraisal Summary, persists it as a Document, links it to
/// the appraisal as a VAL_REPORT checklist entry, and publishes
/// <see cref="AppraisalResultReadyIntegrationEvent"/> — which is what releases the outbound
/// APPRAISAL_COMPLETED webhook to LOS.
///
/// The summary is only meaningful once the committee has decided, and at that point the UI is
/// read-only, so nobody can attach it by hand. This job closes that gap.
///
/// Contract with the rest of the pipeline: <b>exactly one</b> AppraisalResultReadyIntegrationEvent is
/// published per run, whatever happens. Skipping it on failure would leave LOS never being told the
/// case finished.
///
/// Retries are handled in-method rather than by Hangfire ([AutomaticRetry(Attempts = 0)]) so there is a
/// single, predictable exit: a Hangfire-level retry would re-publish the event and fire the webhook again.
/// </summary>
[AutomaticRetry(Attempts = 0)]
public class AppraisalSummaryAutoAttachJob(
    ISqlConnectionFactory connectionFactory,
    IReportPdfGenerator reportPdfGenerator,
    IDocumentCreator documentCreator,
    ISender sender,
    IIntegrationEventOutbox outbox,
    IOutboxScope outboxScope,
    AppraisalDbContext dbContext,
    ILogger<AppraisalSummaryAutoAttachJob> logger)
{
    /// <summary>The composite report key; the provider picks the per-property child forms itself.</summary>
    private const string ReportKey = "appraisal-summary";

    /// <summary>D042 = Construction Progress Inspection Summary, D043 = Property Valuation Summary.</summary>
    private const string ProgressiveDocumentTypeCode = "D042";

    private const string StandardDocumentTypeCode = "D043";

    private const string SystemUserCode = "SYSTEM";

    private const int MaxRenderAttempts = 2;

    private static readonly TimeSpan RenderRetryDelay = TimeSpan.FromSeconds(15);

    private const int MaxPublishAttempts = 3;

    private static readonly TimeSpan PublishRetryDelay = TimeSpan.FromSeconds(3);

    /// <param name="force">
    /// true = always re-render, even if a post-completion summary is already attached. Used by the admin
    /// regenerate endpoint; the automatic path passes false so a second run skips the render. It still
    /// publishes the result-ready event — re-running means LOS is told to collect again, which is the point
    /// of the admin path and harmless on a replay.
    /// </param>
    public async Task RunAsync(
        Guid appraisalId,
        Guid requestId,
        DateTime completedAt,
        bool force,
        CancellationToken ct = default)
    {
        // The argument is ApplicationNow at the time AppraisalCompletedEventHandler ran, not the committee
        // decision time — Appraisals.CompletedAt is. Resolved from the row below and used for every publish
        // so the webhook's occurredAt does not depend on whether the run came from the event or the admin
        // endpoint. Falls back to the argument only when the row cannot be read.
        var approvedAt = completedAt;

        // Marks the early-return paths, where publishing IS the whole work. If that throws there is nothing
        // left to fall back on, so the catch rethrows instead of retrying on a context that just failed —
        // a Failed job in the Hangfire dashboard is the honest signal. The success path leaves this false on
        // purpose: its event is staged in the outbox and rolled back with the command, so the catch still
        // has a real failure to report.
        var terminalPublishAttempted = false;

        // Recorded only so a failure after the upload can name the file it stranded. Nothing branches on it.
        Guid? uploadedDocumentId = null;

        try
        {
            var header = await LoadHeaderAsync(appraisalId, ct);
            if (header is null)
            {
                // Missing or soft-deleted between completion and this job running. Nothing to generate and
                // nothing to wait for — but the webhook still has to go out, otherwise the case stalls.
                logger.LogError(
                    "[SUMMARY-AUTO] Appraisal {AppraisalId} not found or deleted — skipping generation",
                    appraisalId);
                terminalPublishAttempted = true;
                await PublishResultReadyAsync(
                    appraisalId, requestId, approvedAt, documentReady: false,
                    failureReason: "Appraisal not found.", ct);
                return;
            }

            // Classified by the same function the renderer uses, so the label cannot drift from the form that
            // actually gets rendered. Only a construction body is a construction-progress inspection (D042);
            // Block — which wins over Progressive — and Standard both file as a valuation summary (D043).
            var bodyType = AppraisalBodyTypeClassifier.Classify(header.ProjectExists, header.AppraisalType);
            var documentTypeCode = bodyType == AppraisalBodyType.Construction
                ? ProgressiveDocumentTypeCode
                : StandardDocumentTypeCode;

            approvedAt = header.CompletedAt ?? completedAt;

            if (!force && await HasPostCompletionSummaryAsync(appraisalId, documentTypeCode, approvedAt, ct))
            {
                logger.LogInformation(
                    "[SUMMARY-AUTO] Appraisal {AppraisalId} already has a post-completion {DocumentTypeCode} document — skipping render",
                    appraisalId, documentTypeCode);
                terminalPublishAttempted = true;
                await PublishResultReadyAsync(
                    appraisalId, requestId, approvedAt, documentReady: true, failureReason: null, ct);
                return;
            }

            // Checked up front because CreateFromBytesAsync commits the Document row and writes the PDF in
            // its own transaction, before AddAppraisalDocumentCommand validates the type code. Discovering
            // a missing D042/D043 there would leave an unreferenced multi-MB file on disk with nothing to
            // clean it up — and every regenerate attempt would add another. Also saves a wasted render.
            await EnsureDocumentTypeUsableAsync(documentTypeCode, ct);

            ReportFile reportFile;
            try
            {
                reportFile = await RenderAsync(appraisalId, ct);
            }
            catch (NoApplicableReportException)
            {
                // Nothing matched any summary form — an appraisal whose properties are only VEH/VES, or one
                // with no properties at all. There is no document to produce, so the result package is as
                // complete as it will ever be: report it ready rather than flagging a failure nobody can fix
                // (regenerate would resolve to the same empty form list). Warning, not Error, because it is
                // not broken — but it should be visible if the business ever expects a form for these.
                logger.LogWarning(
                    "[SUMMARY-AUTO] No summary form applies to AppraisalId={AppraisalId} "
                    + "(no land/building, condo or machine property, and not a block or construction appraisal) "
                    + "— releasing the webhook with no summary attached",
                    appraisalId);

                // DocumentReady answers "is the summary in the package", not "are we done trying" — and it
                // is not, so it is false. Reporting true here would also silence the Integration-side warning
                // (AppraisalCompletedWebhookConsumer only warns when it is false), leaving a package with no
                // valuation summary reaching LOS with no trace outside this module. The reason says plainly
                // that regenerating will not change the outcome.
                terminalPublishAttempted = true;
                await PublishResultReadyAsync(
                    appraisalId, requestId, approvedAt, documentReady: false,
                    failureReason: "No summary form applies to this appraisal; regenerating cannot change it.",
                    ct);
                return;
            }

            var fileName = BuildFileName(header.AppraisalNumber);

            var documentId = await documentCreator.CreateFromBytesAsync(
                reportFile.Bytes,
                fileName,
                reportFile.ContentType,
                documentType: documentTypeCode,
                // The Document module's own loose label, not the authoritative category — that lives in
                // parameter.DocumentTypes, where D042/D043 are VAL_REPORT, and it is what GetAppraisalResult
                // filters on. Nothing reads Documents.DocumentCategory for these rows, so this mirrors what
                // the checklist UI stamps on every attachment it makes (ValuationDocumentChecklist.tsx) and
                // keeps generated rows uniform with manual ones. The audit columns do differ: a Hangfire job
                // has no HttpContext, so AuditableEntityInterceptor writes CreatedBy = "anonymous" and SYSTEM
                // only reaches UploadedByName.
                documentCategory: "VAL_DOC",
                uploadedBy: SystemUserCode,
                uploadedByName: SystemUserCode,
                ct);

            uploadedDocumentId = documentId;

            // Published BEFORE the command on purpose: IIntegrationEventOutbox stages the message in the
            // request-scoped OutboxScope, and DispatchDomainEventInterceptor drains it into whichever
            // SaveChanges comes next. AddAppraisalDocumentCommand is an ITransactionalCommand, so the
            // AppraisalDocuments row and this event commit together — there is no window where the document
            // is attached but the webhook never fires, or vice versa. Same shape as ReportGenerationJob.
            outbox.Publish(
                new AppraisalResultReadyIntegrationEvent
                {
                    AppraisalId = appraisalId,
                    RequestId = requestId,
                    CompletedAt = approvedAt,
                    DocumentReady = true
                },
                correlationId: appraisalId.ToString());

            await sender.Send(
                new AddAppraisalDocumentCommand(
                    appraisalId,
                    documentTypeCode,
                    documentId,
                    fileName,
                    reportFile.ContentType,
                    reportFile.Bytes.LongLength,
                    Notes: null,
                    SortOrder: null,
                    UploadedByName: SystemUserCode),
                ct);

            logger.LogInformation(
                "[SUMMARY-AUTO] Attached {DocumentTypeCode} ({Bytes} bytes) to AppraisalId={AppraisalId} as DocumentId={DocumentId}",
                documentTypeCode, reportFile.Bytes.Length, appraisalId, documentId);

            // Runs after the attach committed — the webhook is already released, so a failure here cannot
            // stall the case, it only leaves the stale row behind (today's behaviour).
            await SupersedeOtherSummaryCodeAsync(appraisalId, documentTypeCode, approvedAt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown, not a generation failure. Rethrow so Hangfire re-queues the job instead of
            // burning the one attempt and firing the webhook with a "not ready" flag it would never revise.
            logger.LogWarning(
                "[SUMMARY-AUTO] Cancelled mid-run for AppraisalId={AppraisalId} (host shutting down) — requeueing",
                appraisalId);

            // The requeued run renders and uploads again, so anything already uploaded here is abandoned.
            // This exit needs the same trail as the failure path below, or a shutdown mid-upload leaves a
            // file nothing points at and nothing recorded.
            LogPossibleOrphan(uploadedDocumentId, appraisalId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[SUMMARY-AUTO] Summary generation failed permanently for AppraisalId={AppraisalId} — " +
                "releasing the APPRAISAL_COMPLETED webhook anyway so the case does not stall. " +
                "Recover with POST /appraisals/{AppraisalId}/documents/regenerate-summary",
                appraisalId, appraisalId);

            LogPossibleOrphan(uploadedDocumentId, appraisalId);

            // No attempt is made to detect whether an ambiguous commit (client timeout on a transaction that
            // actually landed) already emitted the success event, so a duplicate is possible here. That is
            // deliberate and cheap: DocumentReady never reaches LOS — AppraisalCompletedWebhookConsumer sends
            // { appraisalNumber } either way and only varies its own log line — so a second event reads as
            // "come and collect again", exactly what the admin regenerate path sends on purpose. Detecting it
            // was tried and withdrawn: probing for the document row cannot tell this run's write from one
            // that was already there on the force path, and it shadowed the rethrow below.
            if (terminalPublishAttempted)
            {
                // Publishing WAS the work on that path, and it just failed on this DbContext; retrying it
                // here would almost certainly fail the same way. Rethrow so the job shows as Failed in the
                // Hangfire dashboard instead of reporting success with nothing done.
                throw;
            }

            // Deliberately not rethrown: the webhook has been released, so the run is finished. Letting it
            // bubble would only mark the Hangfire job Failed after the side effect already happened.
            try
            {
                // CancellationToken.None on purpose: if ct is what tripped us (an HTTP timeout inside the
                // render surfaces as a cancellation without ct itself being cancelled, and a genuine host
                // shutdown is already rethrown above), passing it here would cancel the very SaveChanges
                // that releases the webhook and leave the case stalled.
                await PublishResultReadyAsync(
                    appraisalId, requestId, approvedAt, documentReady: false,
                    failureReason: Truncate(ex.Message, 2000), CancellationToken.None);
            }
            catch (Exception publishEx)
            {
                // Now the case really can stall — surface it loudly. Rethrow so Hangfire records a failure
                // and ops can replay from the dashboard.
                logger.LogError(publishEx,
                    "[SUMMARY-AUTO] Failed to publish AppraisalResultReadyIntegrationEvent for AppraisalId={AppraisalId}; " +
                    "the APPRAISAL_COMPLETED webhook will NOT fire until this is replayed",
                    appraisalId);
                throw;
            }
        }
    }

    /// <summary>
    /// Renders the composite summary PDF, retrying transient failures (Puppeteer/browser-pool hiccups).
    /// A <see cref="NotFoundException"/> — including <see cref="NoApplicableReportException"/> — means the
    /// outcome is fixed for this appraisal, so it fails through immediately instead of retrying.
    /// </summary>
    private async Task<ReportFile> RenderAsync(Guid appraisalId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await reportPdfGenerator.GenerateAsync(ReportKey, appraisalId.ToString(), ct);
            }
            catch (NotFoundException)
            {
                // Covers NoApplicableReportException too (it derives from NotFoundException): both mean the
                // outcome cannot change between attempts, so retrying only wastes a browser slot. The caller
                // tells the two apart.
                throw;
            }
            catch (Exception ex) when (attempt < MaxRenderAttempts)
            {
                logger.LogWarning(ex,
                    "[SUMMARY-AUTO] Render attempt {Attempt}/{MaxAttempts} failed for AppraisalId={AppraisalId}, retrying in {Delay}s",
                    attempt, MaxRenderAttempts, appraisalId, RenderRetryDelay.TotalSeconds);
                await Task.Delay(RenderRetryDelay, ct);
            }
        }
    }

    private async Task PublishResultReadyAsync(
        Guid appraisalId,
        Guid requestId,
        DateTime completedAt,
        bool documentReady,
        string? failureReason,
        CancellationToken ct)
    {
        // Two ways a success event can still be in flight here, and each needs a different reset:
        //   - thrown BEFORE the command's SaveChanges — it is staged in the scoped OutboxScope.
        //   - thrown DURING it (deadlock, timeout) — DispatchDomainEventInterceptor had already drained the
        //     scope into the DbContext, and TransactionalBehavior rolls back without detaching anything, so
        //     the AppraisalDocument row and both events sit in the change tracker as Added.
        // The job's AppraisalDbContext is the same scoped instance the unit of work used, so without both
        // resets the SaveChanges below would re-commit the rolled-back document plus a DocumentReady=true
        // event alongside the DocumentReady=false one — two contradicting webhooks and a row that the log
        // just said was never written.
        StageResultReady(appraisalId, requestId, completedAt, documentReady, failureReason);

        // Retried, unlike everything else in this job. The write is a single outbox row with no external
        // side effect, so a second attempt cannot duplicate work — and with [AutomaticRetry(Attempts = 0)]
        // this is the last thing standing between a transient deadlock and LOS never being told the
        // appraisal finished. Worst case a first attempt committed before timing out and LOS gets the same
        // "come and collect" twice, which it already tolerates.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await SaveResultReadyAsync(appraisalId, ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxPublishAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "[SUMMARY-AUTO] Publish attempt {Attempt}/{MaxAttempts} failed for AppraisalId={AppraisalId}, retrying in {Delay}s",
                    attempt, MaxPublishAttempts, appraisalId, PublishRetryDelay.TotalSeconds);
                await Task.Delay(PublishRetryDelay, ct);

                // The failed SaveChanges left the outbox message staged in the DbContext; drop it so the
                // retry below stages a fresh one instead of committing two.
                dbContext.ChangeTracker.Clear();
                outboxScope.Clear();
                StageResultReady(appraisalId, requestId, completedAt, documentReady, failureReason);
            }
        }
    }

    /// <summary>
    /// Resets any half-applied state and queues the event into the scoped outbox. Split out so the retry
    /// loop can stage a fresh message after a failed SaveChanges instead of committing two.
    ///
    /// Both resets are needed, for different reasons: a success event thrown BEFORE the command's
    /// SaveChanges is still sitting in the OutboxScope, while one thrown DURING it was already drained into
    /// the DbContext by DispatchDomainEventInterceptor and left there as Added when TransactionalBehavior
    /// rolled back without detaching. This job's AppraisalDbContext is the same scoped instance the unit of
    /// work used, so skipping either reset would re-commit a rolled-back document row and a
    /// DocumentReady=true event alongside the DocumentReady=false one.
    /// </summary>
    private void StageResultReady(
        Guid appraisalId, Guid requestId, DateTime completedAt, bool documentReady, string? failureReason)
    {
        dbContext.ChangeTracker.Clear();
        outboxScope.Clear();

        outbox.Publish(
            new AppraisalResultReadyIntegrationEvent
            {
                AppraisalId = appraisalId,
                RequestId = requestId,
                CompletedAt = completedAt,
                DocumentReady = documentReady,
                FailureReason = failureReason
            },
            correlationId: appraisalId.ToString());
    }

    private async Task SaveResultReadyAsync(Guid appraisalId, CancellationToken ct)
    {
        // No command follows on these paths, so drain the outbox scope with an explicit SaveChanges.
        // DispatchDomainEventInterceptor adds the staged message inside SavingChangesAsync, so a
        // zero-row result means it never landed and the APPRAISAL_COMPLETED webhook will never fire —
        // the exact stall this job is built to avoid. Fail loudly rather than returning quietly.
        var written = await dbContext.SaveChangesAsync(ct);
        if (written == 0)
        {
            throw new InvalidOperationException(
                $"AppraisalResultReadyIntegrationEvent for appraisal {appraisalId} was not persisted to the outbox.");
        }
    }

    /// <summary>
    /// "Already generated" means a document of the exact type this job would produce, attached at or
    /// after the committee decision — anything older is the stale pre-approval copy this job exists to
    /// replace. AppraisalDocuments carries no version column; GetAppraisalResult picks the newest row
    /// per DocumentTypeCode by CreatedAt, which is the same rule applied here.
    ///
    /// Matching the whole VAL_REPORT category would be wrong: it also covers D001 (Complete Valuation
    /// Report), so attaching the appraisal book after closing would make this job skip the summary and
    /// still report the result package ready.
    /// </summary>
    private async Task<bool> HasPostCompletionSummaryAsync(
        Guid appraisalId, string documentTypeCode, DateTime completedAt, CancellationToken ct)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [appraisal].[AppraisalDocuments] ad
                WHERE ad.[AppraisalId] = @AppraisalId
                  AND ad.[DocumentTypeCode] = @DocumentTypeCode
                  AND ad.[CreatedAt] >= @CompletedAt
            ) THEN 1 ELSE 0 END
            """;

        using var connection = connectionFactory.CreateNewConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { AppraisalId = appraisalId, DocumentTypeCode = documentTypeCode, CompletedAt = completedAt },
                cancellationToken: ct));
    }

    /// <summary>
    /// Asserts VAL_REPORT specifically, not the VAL_DOC/VAL_REPORT pair AddAppraisalDocumentCommandHandler
    /// accepts. D042/D043 are seeded as VAL_DOC by 20260621090000_SeedDocumentTypesAndPatchCodes.sql and only
    /// become VAL_REPORT in 20260719130000_PatchDocumentTypeD042D043ToValReport.sql, and GetAppraisalResult
    /// serves the LOS package from `dt.Category = 'VAL_REPORT'` alone. On a database missing that second
    /// script the looser check would pass, the summary would attach, DocumentReady=true would go out — and
    /// LOS would collect a package with no documents in it.
    /// </summary>
    private async Task EnsureDocumentTypeUsableAsync(string documentTypeCode, CancellationToken ct)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [parameter].[DocumentTypes]
                WHERE [Code] = @Code AND [Category] = 'VAL_REPORT' AND [IsActive] = 1
            ) THEN 1 ELSE 0 END
            """;

        using var connection = connectionFactory.CreateNewConnection();
        var usable = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Code = documentTypeCode }, cancellationToken: ct));

        if (!usable)
        {
            throw new InvalidOperationException(
                $"Document type '{documentTypeCode}' is not an active VAL_REPORT row in parameter.DocumentTypes; "
                + "the appraisal summary would attach but never reach the LOS result package. "
                + "Check that 20260719130000_PatchDocumentTypeD042D043ToValReport.sql has run.");
        }
    }

    /// <summary>
    /// Deliberately stricter than the report itself: AppraisalSummaryDataProvider's dispatch query filters on
    /// Id only, so a human can still render a soft-deleted appraisal's summary on the sync endpoint. This
    /// path does not just render — it attaches a document and pushes a notification to the bank's LOS, and
    /// doing that for a record the system considers deleted is a different thing entirely. A soft-deleted
    /// appraisal falls into the "not found" branch instead.
    /// </summary>
    private async Task<AppraisalHeaderRow?> LoadHeaderAsync(Guid appraisalId, CancellationToken ct)
    {
        const string sql = """
            SELECT a.[AppraisalNumber],
                   a.[AppraisalType],
                   a.[CompletedAt],
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM [appraisal].[Projects] pr
                                          WHERE pr.[AppraisalId] = a.[Id])
                             THEN 1 ELSE 0 END AS bit) AS ProjectExists
            FROM [appraisal].[Appraisals] a
            WHERE a.[Id] = @AppraisalId
              AND a.[IsDeleted] = 0
            """;

        using var connection = connectionFactory.CreateNewConnection();
        return await connection.QuerySingleOrDefaultAsync<AppraisalHeaderRow>(
            new CommandDefinition(sql, new { AppraisalId = appraisalId }, cancellationToken: ct));
    }

    /// <summary>
    /// e.g. "appraisal-summary-AP-2569-00042.pdf"; falls back to the bare key when the number is missing.
    /// Path separators are stripped because the value reaches the filesystem via DocumentService.
    /// </summary>
    internal static string BuildFileName(string? appraisalNumber)
    {
        if (string.IsNullOrWhiteSpace(appraisalNumber))
            return $"{ReportKey}.pdf";

        var safe = appraisalNumber.Replace('/', '-').Replace('\\', '-').Trim();
        return $"{ReportKey}-{safe}.pdf";
    }

    /// <summary>
    /// Removes a previously auto-generated summary filed under the OTHER code.
    ///
    /// The document type is re-derived on every run, so a post-close data correction that flips the body
    /// type — dropping Progressive, or adding an appraisal.Projects row — makes a regenerate file the new
    /// render under D043 while the old D042 stays. GetAppraisalResult returns the newest row PER
    /// DocumentTypeCode, not the newest summary overall, so LOS would then collect both: a construction
    /// summary and a valuation summary for the same appraisal, disagreeing with each other.
    ///
    /// Scoped deliberately narrowly — only rows this pipeline created (UploadedByName = SYSTEM) at or after
    /// the committee decision. A summary a person attached by hand, or anything predating approval, is left
    /// alone; deciding those is not this job's call. Goes through RemoveAppraisalDocumentCommand rather than
    /// deleting directly so DocumentUnlinkedIntegrationEvent still fires and the Document module's
    /// ReferenceCount stays correct.
    /// </summary>
    private async Task SupersedeOtherSummaryCodeAsync(
        Guid appraisalId, string attachedTypeCode, DateTime approvedAt, CancellationToken ct)
    {
        var otherCode = attachedTypeCode == ProgressiveDocumentTypeCode
            ? StandardDocumentTypeCode
            : ProgressiveDocumentTypeCode;

        try
        {
            const string sql = """
                SELECT ad.[Id]
                FROM [appraisal].[AppraisalDocuments] ad
                WHERE ad.[AppraisalId] = @AppraisalId
                  AND ad.[DocumentTypeCode] = @OtherCode
                  AND ad.[UploadedByName] = @SystemUser
                  AND ad.[CreatedAt] >= @ApprovedAt
                """;

            using var connection = connectionFactory.CreateNewConnection();
            var staleIds = (await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AppraisalId = appraisalId,
                        OtherCode = otherCode,
                        SystemUser = SystemUserCode,
                        ApprovedAt = approvedAt
                    },
                    cancellationToken: ct))).ToList();

            foreach (var staleId in staleIds)
            {
                await sender.Send(new RemoveAppraisalDocumentCommand(appraisalId, staleId), ct);

                logger.LogInformation(
                    "[SUMMARY-AUTO] Superseded stale {OtherCode} summary {AppraisalDocumentId} on AppraisalId={AppraisalId} "
                    + "after the body type changed to {AttachedTypeCode}",
                    otherCode, staleId, appraisalId, attachedTypeCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[SUMMARY-AUTO] Could not supersede stale {OtherCode} summaries on AppraisalId={AppraisalId}; "
                + "the LOS result package may now carry two conflicting summaries",
                otherCode, appraisalId);
        }
    }

    /// <summary>
    /// The upload commits on the Document module's own unit of work, so a failure afterwards usually leaves
    /// the file and its Documents row behind with nothing referencing them, and no job in the system sweeps
    /// unlinked documents — a log line is the only trail ops has.
    ///
    /// Deliberately hedged rather than asserting an orphan: on an ambiguous commit the link DID land, and
    /// that row is the one GetAppraisalResult serves to LOS, so deleting it on the strength of this message
    /// would turn a harmless duplicate webhook into a broken result package.
    /// </summary>
    private void LogPossibleOrphan(Guid? uploadedDocumentId, Guid appraisalId)
    {
        if (uploadedDocumentId is null) return;

        logger.LogWarning(
            "[SUMMARY-AUTO] Document {DocumentId} for AppraisalId={AppraisalId} may be orphaned: the file and "
            + "its Documents row were committed, but the link may not have been. Before deleting, check that "
            + "no appraisal.AppraisalDocuments row references it.",
            uploadedDocumentId.Value, appraisalId);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    // Dapper binds positionally — keep this in the SELECT's column order.
    private sealed record AppraisalHeaderRow(
        string? AppraisalNumber, string? AppraisalType, DateTime? CompletedAt, bool ProjectExists);
}
