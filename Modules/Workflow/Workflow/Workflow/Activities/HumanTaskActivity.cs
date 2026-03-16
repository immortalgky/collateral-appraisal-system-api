using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Generic human task activity with full assignment strategy support.
/// Uses complex assignment logic from HumanTaskActivityBase including:
/// - Assignment pipeline with team filtering and exclusion rules
/// - Runtime overrides
/// - Previous owner detection
/// - Cascading assignment strategies
/// - External configuration
/// </summary>
public class HumanTaskActivity : HumanTaskActivityBase
{
    public HumanTaskActivity(
        IWorkflowBookmarkService bookmarkService,
        IWorkflowAuditService auditService,
        IAssignmentPipeline assignmentPipeline,
        IPublisher publisher,
        ILogger<HumanTaskActivity> logger)
        : base(bookmarkService, auditService, assignmentPipeline, publisher, logger)
    {
    }

    public override string ActivityType => ActivityTypes.HumanTask;
    public override string Name => "Human Task";

    public override string Description =>
        "Assigns a task to a user or role for completion using various assignment strategies";

    // Uses all the complex assignment logic from HumanTaskActivityBase
    // No overrides needed - the base class provides full functionality
}