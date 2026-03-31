using Appraisal.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Handles RequestSubmittedIntegrationEvent by creating an appraisal from the submitted request.
/// </summary>
public class RequestSubmittedIntegrationEventHandler(
    ILogger<RequestSubmittedIntegrationEventHandler> logger,
    IAppraisalCreationService appraisalCreationService)
    : IConsumer<RequestSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId} with {TitleCount} titles",
            nameof(RequestSubmittedIntegrationEvent),
            message.RequestId,
            message.RequestTitles.Count);

        try
        {
            var appraisalId = await appraisalCreationService.CreateAppraisalFromRequest(
                message.RequestId,
                message.RequestTitles,
                message.Appointment,
                message.Fee,
                message.Contact,
                message.CreatedBy,
                message.Priority,
                message.IsPma,
                message.Purpose,
                message.Channel,
                message.BankingSegment,
                message.FacilityLimit,
                context.CancellationToken);

            logger.LogInformation(
                "Successfully processed RequestSubmittedIntegrationEvent. Created/Retrieved AppraisalId: {AppraisalId} for RequestId: {RequestId}",
                appraisalId,
                message.RequestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing RequestSubmittedIntegrationEvent for RequestId: {RequestId}",
                message.RequestId);

            // Let exception propagate for MassTransit retry/error handling
            throw;
        }
    }
}
