namespace Request.Data.Seed;

public static class InitialData
{
    public static IEnumerable<Requests.Models.Request> Requests => new List<Requests.Models.Request>
    {
        Request.Requests.Models.Request.Create(
            "Appraisal",
            true,
            "High",
            "Online",
            null,
            Reference.Create(
                "PA-12345",
                1000000,
                DateTime.Now.AddMonths(-6)
            ),
            LoanDetail.Create(
                "LA-67890",
                500000,
                1200000
            ),
            Address.Create(
                "123",
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
                "12345"
            ),
            Contact.Create(
                "John Doe",
                "0123456789",
                "Project-1"
            ),
            Fee.Create(
                "01",
                "No additional fees"
            ),
            Requestor.Create(
                "EMP-001",
                "Jane Smith",
                "",
                "0987654321",
                "AO-001",
                "01",
                "01",
                "01",
                "01",
                "01"
            )
        )
    };
}