using Appraisal.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Handles AppraisalCreationRequestedIntegrationEvent by creating an appraisal from the request payload.
/// This event is published by the Workflow module when the table-driven condition matches.
/// </summary>
public class AppraisalCreationRequestedIntegrationEventHandler(
    ILogger<AppraisalCreationRequestedIntegrationEventHandler> logger,
    IAppraisalCreationService appraisalCreationService)
    : IConsumer<AppraisalCreationRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCreationRequestedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId} with {TitleCount} titles",
            nameof(AppraisalCreationRequestedIntegrationEvent),
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
                message.HasAppraisalBook,
                message.RequestedBy,
                message.RequestedAt,
                context.CancellationToken);

            logger.LogInformation(
                "Successfully processed AppraisalCreationRequestedIntegrationEvent. Created/Retrieved AppraisalId: {AppraisalId} for RequestId: {RequestId}",
                appraisalId,
                message.RequestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing AppraisalCreationRequestedIntegrationEvent for RequestId: {RequestId}",
                message.RequestId);

            throw;
        }
    }
}
