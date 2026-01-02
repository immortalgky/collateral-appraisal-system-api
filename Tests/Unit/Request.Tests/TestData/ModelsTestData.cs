using Request.Domain.Requests;
using Shared.Models;

namespace Request.Tests.TestData;

public static class ModelsTestData
{
    public static Domain.Requests.Request RequestGeneral()
    {
        var requestor = new UserInfo("01", "John");
        var creator = new UserInfo("01", "John");

        var loanDetail = LoanDetail.Create(new LoanDetailData(
            "RB",
            "LA-67890",
            1700000,
            500000,
            1200000,
            1200000));

        var address = Address.Create(new AddressData(
            "A1",
            "Project-1",
            "2",
            "Soi 10",
            "Main Road",
            "SubDistrict",
            "District",
            "Province",
            "12345"));

        var contact = Contact.Create(
            "John Doe",
            "0123456789",
            "DL-001");

        var appointment = Appointment.Create(
            null,
            null);

        var fee = Fee.Create(
            "01",
            "No additional fees",
            null);

        var detail = RequestDetail.Create(new RequestDetailData(
            false,
            loanDetail,
            null,
            address,
            contact,
            appointment,
            fee));

        var request = Domain.Requests.Request.Create(new RequestData(
            "NCL",
            "LOS",
            requestor,
            creator,
            DateTime.UtcNow,
            "High",
            false));

        request.SetDetail(detail);
        return request;
    }

    public static Domain.Requests.Request RequestDraft()
    {
        var requestor = new UserInfo("01", "John");
        var creator = new UserInfo("01", "John");

        var loanDetail = LoanDetail.Create(new LoanDetailData(
            null,
            null,
            null,
            null,
            null,
            null));

        var address = Address.Create(new AddressData(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null));

        var contact = Contact.Create(
            null,
            null,
            null);

        var appointment = Appointment.Create(
            null,
            null);

        var fee = Fee.Create(
            null,
            null,
            null);

        var detail = RequestDetail.Create(new RequestDetailData(
            false,
            loanDetail,
            null,
            address,
            contact,
            appointment,
            fee));

        var request = Domain.Requests.Request.Create(new RequestData(
            null,
            null,
            requestor,
            creator,
            DateTime.UtcNow,
            "Medium",
            false));

        request.SetDetail(detail);
        return request;
    }
}