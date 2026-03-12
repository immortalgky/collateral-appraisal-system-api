namespace Request.Application.EventHandlers.Request;

public class AppraisalCompletedIntegrationEventHandler(
    ILogger<AppraisalCompletedIntegrationEventHandler> logger,
    IRequestRepository requestRepository,
    RequestDbContext dbContext) : IConsumer<AppraisalCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId}",
            nameof(AppraisalCompletedIntegrationEvent), message.RequestId);

        var request = await requestRepository.GetByIdAsync(message.RequestId, context.CancellationToken)
            ?? throw new NotFoundException("Request", message.RequestId);

        request.Complete(message.CompletedAt);

        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Request {RequestId} status updated to Completed",
            message.RequestId);
    }
}
