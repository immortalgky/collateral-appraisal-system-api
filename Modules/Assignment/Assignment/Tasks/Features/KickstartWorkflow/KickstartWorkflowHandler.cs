using Assignment.Services;

namespace Assignment.Tasks.Features.KickstartWorkflow;

public record KickstartWorkflowCommand(long RequestId) : ICommand<KickstartWorkflowResult>;

public record KickstartWorkflowResult(Guid CorrelationId);

public class KickstartWorkflowHandler(IAssignmentService assignmentService)
    : ICommandHandler<KickstartWorkflowCommand, KickstartWorkflowResult>
{
    public async Task<KickstartWorkflowResult> Handle(KickstartWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        var correlationId = await assignmentService.StartWorkflowAsync(command.RequestId, cancellationToken);

        return new KickstartWorkflowResult(correlationId);
    }
}