using System.Globalization;
using Request.Data.Repository;

namespace Request.Requests.Services;

public class AppraisalNumberGenerator(IRequestRepository requestRepository) : IAppraisalNumberGenerator
{
    public async Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var yearTh = DateTime.Now.ToString("yy", new CultureInfo("th-TH"));

        var nextNumber = await requestRepository.GetNextAppraisalNumber(cancellationToken);

        return AppraisalNumber.Create($"{yearTh}A{nextNumber:D6}");
    }
}