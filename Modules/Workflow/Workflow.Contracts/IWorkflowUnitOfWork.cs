namespace Workflow;

/// <summary>
/// Unit-of-work marker for the Workflow module. Defined in Workflow.Contracts so that
/// cross-module commands (e.g. FulfillDocumentFollowupCommand) can reference it without
/// creating a circular project dependency.
/// </summary>
public interface IWorkflowUnitOfWork : IUnitOfWork;
