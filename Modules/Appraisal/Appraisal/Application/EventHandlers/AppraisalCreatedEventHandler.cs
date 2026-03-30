using Dapper;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class AppraisalCreatedEventHandler(
    ILogger<AppraisalCreatedEventHandler> logger,
    IIntegrationEventOutbox outbox,
    ISqlConnectionFactory sqlConnectionFactory) : INotificationHandler<AppraisalCreatedEvent>
{
    public async Task Handle(AppraisalCreatedEvent notification, CancellationToken cancellationToken)
    {
        var appraisal = notification.Appraisal;

        logger.LogInformation("Domain Event handled: {DomainEvent} for AppraisalId: {AppraisalId}",
            nameof(AppraisalCreatedEvent), appraisal.Id);

        var requestData = await GetRequestDataAsync(appraisal.RequestId);

        outbox.Publish(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            RequestId = appraisal.RequestId,
            AppraisalNumber = appraisal.AppraisalNumber,
            AppraisalType = appraisal.AppraisalType,
            CreatedBy = notification.RequestedBy ?? appraisal.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            IsPma = requestData?.IsPma ?? false,
            FacilityLimit = requestData?.FacilityLimit,
            Priority = appraisal.Priority,
            HasAppraisalBook = requestData?.HasAppraisalBook ?? false
        }, correlationId: appraisal.Id.ToString());

        logger.LogInformation(
            "Published AppraisalCreatedIntegrationEvent for AppraisalId: {AppraisalId}, RequestId: {RequestId}",
            appraisal.Id, appraisal.RequestId);
    }

    private async Task<RequestRoutingData?> GetRequestDataAsync(Guid requestId)
    {
        using var connection = sqlConnectionFactory.CreateNewConnection();
        const string sql = """
            SELECT r.IsPma, rd.HasAppraisalBook, rd.FacilityLimit
            FROM request.Requests r
            LEFT JOIN request.RequestDetails rd ON rd.RequestId = r.Id
            WHERE r.Id = @RequestId
            """;
        return await connection.QueryFirstOrDefaultAsync<RequestRoutingData>(sql, new { RequestId = requestId });
    }

    private record RequestRoutingData(bool IsPma, bool HasAppraisalBook, decimal? FacilityLimit);
}
