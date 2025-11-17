using Request.Requests.ValueObjects;

namespace Request.Tests.TestData;

public static class ModelsTestData
{
    public static Requests.Models.Request RequestGeneral() => Requests.Models.Request.Create(
        RequestDetail.Create
        (
            false,
            null,
            LoanDetail.Create("LA-67890",
                "RB",
                1700000,
                500000,
                1200000,
                1200000),
            Address.Create("123",
                "A1",
                "2",
                "A",
                "Location 1",
                "5",
                "Soi 10",
                "Main Road",
                "100101",
                "1001",
                "10",
                "12345"),
            Contact.Create(
                "John Doe",
                "0123456789",
                "Project-1"),
            Appointment.Create(
                null,
                null),
            Fee.Create(
                "01",
                "No additional fees",
                null)
        ),
        false,
        "NCL",
        "High",
        SourceSystem.Create(
            "LOS",
            null,
            "01",
            "John",
            null,
            "01",
            "John"
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
        //     )
    );
}