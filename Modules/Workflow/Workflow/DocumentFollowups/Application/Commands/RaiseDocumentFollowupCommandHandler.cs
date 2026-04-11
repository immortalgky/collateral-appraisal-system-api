using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.Infrastructure;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

public class RaiseDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowDefinitionRepository definitionRepository,
    IWorkflowInstanceRepository instanceRepository,
    IWorkflowService workflowService,
    ICurrentUserService currentUser,
    ILogger<RaiseDocumentFollowupCommandHandler> logger
) : ICommandHandler<RaiseDocumentFollowupCommand, RaiseDocumentFollowupResult>
{
    public async Task<RaiseDocumentFollowupResult> Handle(
        RaiseDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        if (command.LineItems is null || command.LineItems.Count == 0)
            throw new ArgumentException("At least one line item is required");

        // W6: reject ambiguous auto-fulfill targets. If two line items share a document type,
        // the upload-matching logic (FulfillFirstMatchingByType) would arbitrarily resolve one.
        var duplicateType = command.LineItems
            .GroupBy(li => li.DocumentType, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateType is not null)
            throw new ArgumentException(
                $"Duplicate document type '{duplicateType.Key}' in line items. " +
                "Auto-fulfill cannot disambiguate two line items of the same type.");

        var raisingUserId = currentUser.UserId?.ToString() ?? currentUser.Username
            ?? throw new InvalidOperationException("User not authenticated");

        // 1. Load raising (parent) workflow instance — must exist + be running
        var parentInstance = await instanceRepository.GetByIdAsync(command.RaisingWorkflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Raising workflow instance {command.RaisingWorkflowInstanceId} not found");

        // 2. Resolve the pending task to capture activityId + correlationId (= requestId)
        var pendingTask = await dbContext.PendingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == command.RaisingPendingTaskId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Raising pending task {command.RaisingPendingTaskId} not found");

        // Reject duplicate open followups for the same raising task. The authoritative guard
        // is the filtered unique index UX_DocumentFollowups_RaisingPendingTaskId_Open; this
        // check is a friendlier error for the common non-racey case.
        var existingOpen = await dbContext.DocumentFollowups
            .AnyAsync(f => f.RaisingPendingTaskId == command.RaisingPendingTaskId
                           && f.Status == DocumentFollowupStatus.Open, cancellationToken);
        if (existingOpen)
            throw new InvalidOperationException(
                "An open document followup already exists for this task. Resolve or cancel it first.");

        var requestId = pendingTask.CorrelationId;
        var appraisalId = ReadGuidFromVariables(parentInstance.Variables, "appraisalId")
            ?? throw new InvalidOperationException(
                $"Cannot raise document followup for workflow {command.RaisingWorkflowInstanceId}: " +
                "appraisalId is not set in workflow variables. Document followups require an existing appraisal.");

        // 3. Resolve followup workflow definition up front so we fail fast before any writes
        var followupDefinition = await definitionRepository.GetLatestVersion(
            DocumentFollowupWorkflowDefinitionSeeder.WorkflowName, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Workflow definition '{DocumentFollowupWorkflowDefinitionSeeder.WorkflowName}' not found. " +
                "Did the seeder run?");

        // 4. Create the aggregate
        var followup = DocumentFollowup.Raise(
            appraisalId: appraisalId,
            requestId: requestId,
            raisingWorkflowInstanceId: command.RaisingWorkflowInstanceId,
            raisingPendingTaskId: command.RaisingPendingTaskId,
            raisingActivityId: pendingTask.ActivityId,
            raisingUserId: raisingUserId,
            lineItems: command.LineItems.Select(li => (li.DocumentType, li.Notes)));

        // W2: wrap the two writes in a transaction so a failure in StartWorkflowAsync does
        // not leave an orphaned Open followup without a followup workflow instance.
        // In-memory provider does not support transactions — fall back to a plain save.
        var useTransaction = dbContext.Database.IsRelational();
        var transaction = useTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            dbContext.DocumentFollowups.Add(followup);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // W7: concurrent raise won the unique-index race.
                throw new InvalidOperationException(
                    "An open document followup already exists for this task. Resolve or cancel it first.", ex);
            }

            // 5. Spawn the followup workflow instance.
            // StartedBy = parent's StartedBy → StartedByAssigneeSelector resolves to request maker.
            var initialVariables = new Dictionary<string, object>
            {
                ["documentFollowupId"] = followup.Id,
                ["parentAppraisalId"] = appraisalId,
                ["raisingWorkflowInstanceId"] = command.RaisingWorkflowInstanceId,
                ["raisingPendingTaskId"] = command.RaisingPendingTaskId,
                ["raisingActivityId"] = pendingTask.ActivityId,
                ["raisingUserId"] = raisingUserId
            };

            var followupInstance = await workflowService.StartWorkflowAsync(
                workflowDefinitionId: followupDefinition.Id,
                instanceName: $"DocFollowup-{followup.Id}",
                startedBy: parentInstance.StartedBy,
                initialVariables: initialVariables,
                correlationId: followup.Id.ToString(),
                cancellationToken: cancellationToken);

            followup.AttachFollowupWorkflowInstance(followupInstance.Id);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Raised document followup {FollowupId} for raising task {TaskId}, followup workflow instance {InstanceId}",
                followup.Id, command.RaisingPendingTaskId, followupInstance.Id);

            return new RaiseDocumentFollowupResult(followup.Id, followupInstance.Id);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Reads a Guid from a workflow variable bag, tolerating the three shapes the value can
    /// take depending on how the instance was loaded: native Guid, string, or JsonElement
    /// (post-EF JSON hydration). Returns null if the variable is missing or unparseable.
    /// </summary>
    private static Guid? ReadGuidFromVariables(Dictionary<string, object> variables, string key)
    {
        if (!variables.TryGetValue(key, out var raw) || raw is null)
            return null;

        switch (raw)
        {
            case Guid g:
                return g;
            case string s when Guid.TryParse(s, out var parsedStr):
                return parsedStr;
            case JsonElement je when je.ValueKind == JsonValueKind.String &&
                                     Guid.TryParse(je.GetString(), out var parsedJson):
                return parsedJson;
            default:
                return Guid.TryParse(raw.ToString(), out var fallback) ? fallback : null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // SQL Server error numbers for unique constraint / unique index violations.
        var inner = ex.InnerException?.Message ?? ex.Message;
        return inner.Contains("UX_DocumentFollowups_RaisingPendingTaskId_Open", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("Violation of UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
