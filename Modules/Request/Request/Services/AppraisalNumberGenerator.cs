using System.Data;
using System.Globalization;
using Dapper;

namespace Request.Services;

public class AppraisalNumberGenerator(ISqlConnectionFactory sqlConnectionFactory) : IAppraisalNumberGenerator
{
    public async Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var yearTh = DateTime.Now.ToString("yy", new CultureInfo("th-TH"));
        var nextNumber = await GetNextAppraisalNumberAsync(cancellationToken);
        var appraisalNumber = AppraisalNumber.Create($"{yearTh}A{nextNumber:D6}");

        return appraisalNumber;
    }
    
    private async Task<int> GetNextAppraisalNumberAsync(CancellationToken cancellationToken)
    {
        var connection =  sqlConnectionFactory.GetOpenConnection();

        return await connection.QuerySingleAsync<int>(
            "request.sp_GetNextAppraisalNumber",
            commandType: CommandType.StoredProcedure
        );
    }
}