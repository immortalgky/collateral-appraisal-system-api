using Request.Domain.Requests;
using Request.Infrastructure.Repositories;
using Shared.CQRS;
using Shared.Models;
using Shared.Time;

namespace Integration.Application.Features.AppraisalRequests.CreateAppraisalRequest;

public class CreateAppraisalRequestCommandHandler(
    IRequestRepository requestRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateAppraisalRequestCommand, CreateAppraisalRequestResult>
{
    public async Task<CreateAppraisalRequestResult> Handle(
        CreateAppraisalRequestCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now;

        // Create request aggregate
        var request = Request.Domain.Requests.Request.Create(new RequestData(
            command.Purpose,
            command.Channel,
            new UserInfo("CLS", "CLS System"),
            new UserInfo("CLS", "CLS System"),
            now,
            command.Priority,
            false
        ));

        // Set external reference
        request.SetExternalReference(command.ExternalCaseKey, "CLS");

        // Set loan detail if provided
        if (command.LoanDetail is not null)
        {
            var detail = RequestDetail.Create(new RequestDetailData(
                false,
                LoanDetail.Create(new LoanDetailData(
                    command.LoanDetail.LoanApplicationNumber,
                    command.LoanDetail.BankingSegment,
                    command.LoanDetail.FacilityLimit,
                    null,
                    null,
                    null
                )),
                null,
                null,
                command.Contact is not null
                    ? Contact.Create(
                        command.Contact.ContactPersonName,
                        command.Contact.ContactPersonPhone,
                        null
                    )
                    : null,
                null,
                null
            ));
            request.SetDetail(detail);
        }

        // Add customers
        if (command.Customers is { Count: > 0 })
        {
            var customers = command.Customers
                .Select(c => RequestCustomer.Create(c.Name, c.ContactNumber))
                .ToList();
            request.SetCustomers(customers);
        }

        // Add properties
        if (command.Properties is { Count: > 0 })
        {
            var properties = command.Properties
                .Select(p => RequestProperty.Create(p.PropertyType, p.BuildingType, p.SellingPrice))
                .ToList();
            request.SetProperties(properties);
        }

        // Add documents
        if (command.DocumentIds is { Count: > 0 })
        {
            foreach (var docId in command.DocumentIds)
            {
                request.AddDocument(new RequestDocumentData(
                    DocumentId: docId,
                    DocumentType: "UPLOADED",
                    FileName: null,
                    Prefix: null,
                    Set: null,
                    Notes: null,
                    FilePath: null,
                    Source: "CLS",
                    IsRequired: false,
                    UploadedBy: "CLS",
                    UploadedByName: "CLS System",
                    UploadedAt: now
                ));
            }
        }

        request.UpdateStatus(RequestStatus.New);

        await requestRepository.AddAsync(request, cancellationToken);
        await requestRepository.SaveChangesAsync(cancellationToken);

        return new CreateAppraisalRequestResult(
            request.Id,
            request.RequestNumber?.Value
        );
    }
}
