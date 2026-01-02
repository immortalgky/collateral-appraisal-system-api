using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Services;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Generic human task activity with full assignment strategy support.
/// Uses complex assignment logic from HumanTaskActivityBase including:
/// - Custom assignment services
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
        ILogger<HumanTaskActivity> logger)
        : base(bookmarkService, auditService, logger)
    {
    }

    public override string ActivityType => ActivityTypes.HumanTask;
    public override string Name => "Human Task";
    public override string Description => "Assigns a task to a user or role for completion using various assignment strategies";

    // Uses all the complex assignment logic from HumanTaskActivityBase
    // No overrides needed - the base class provides full functionality
}