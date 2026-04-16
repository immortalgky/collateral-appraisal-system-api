namespace Workflow.Workflow.Services;

public interface IWorkflowSignalDispatcher
{
    Task DispatchAsync(
        string signalName,
        string correlationValue,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default);
}
