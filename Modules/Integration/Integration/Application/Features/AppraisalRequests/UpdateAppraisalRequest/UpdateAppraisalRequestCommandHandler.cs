using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.CQRS;

namespace Integration.Application.Features.AppraisalRequests.UpdateAppraisalRequest;

public class UpdateAppraisalRequestCommandHandler(
    IRequestRepository requestRepository
) : ICommandHandler<UpdateAppraisalRequestCommand, UpdateAppraisalRequestResult>
{
    public async Task<UpdateAppraisalRequestResult> Handle(
        UpdateAppraisalRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);

        if (request is null)
        {
            throw new KeyNotFoundException($"Request {command.RequestId} not found");
        }

        // Update customers if provided
        if (command.Customers is not null)
        {
            var customers = command.Customers
                .Select(c => RequestCustomer.Create(c.Name, c.ContactNumber))
                .ToList();
            request.SetCustomers(customers);
        }

        // Update properties if provided
        if (command.Properties is not null)
        {
            var properties = command.Properties
                .Select(p => RequestProperty.Create(p.PropertyType, p.BuildingType, p.SellingPrice))
                .ToList();
            request.SetProperties(properties);
        }

        await requestRepository.SaveChangesAsync(cancellationToken);

        return new UpdateAppraisalRequestResult(true);
    }
}
