namespace Request.Data.Seed;

public static class InitialData
{
    public static IEnumerable<Requests.Models.Request> Requests => new List<Requests.Models.Request>
    {
        global::Request.Requests.Models.Request.Create(
            "Appraisal",
            "Normal",
            RequestStatus.Draft,
            false,
            false,
            null,
            LoanDetail.Create(
                null,
                null,
                null,
                1700000,
                500000,
                1200000
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
                "Starbuck"
            ),
            Fee.Create(
                "01",
                1000,
                null
            )
        )
    };
}