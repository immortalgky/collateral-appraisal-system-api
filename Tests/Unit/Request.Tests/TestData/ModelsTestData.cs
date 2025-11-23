using Request.Requests.ValueObjects;

namespace Request.Tests.TestData;

public static class ModelsTestData
{
    public static Requests.Models.Request RequestGeneral() => Requests.Models.Request.Create(
        "Appraisal",
        "Normal",
        RequestStatus.Draft,
        false,
        false,
        null,
        // Reference.Create(
        //     "PA-12345",
        //     1000000,
        //     DateTime.Now.AddMonths(-6)
        // ),
        LoanDetail.Create(
            "LA-67890",
            null,
            1700000,
            500000,
            1200000,
            1800000
        ),
        Address.Create(
            "123",
            "A1",
            "2",
            "Ideo",
            "5",
            "Soi 10",
            "Main Road",
            "100101",
            "1001",
            "10",
            "12345"
        ),
        Contact.Create(
            "John Doe",
            "0123456789",
            "Project-1"
        ),
        Appointment.Create(
            DateTime.Now,
            null
        ),
        Fee.Create(
            "01",
            1000,
            "No additional fees"
        )
        // Requestor.Create(
        //     "EMP-001",
        //     "Jane Smith",
        //     "",
        //     "0987654321",
        //     "AO-001",
        //     "01",
        //     "01",
        //     "01",
        //     "01",
        //     "01"
        // )
    );
    
}