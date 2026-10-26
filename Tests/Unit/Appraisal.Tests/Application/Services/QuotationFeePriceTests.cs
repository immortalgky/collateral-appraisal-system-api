using Appraisal.Application.Services;
using Appraisal.Domain.Quotations;

namespace Appraisal.Tests.Application.Services;

public class QuotationFeePriceTests
{
    [Fact]
    public void Resolve_breaks_an_item_number_tie_in_sql_server_guid_order_not_dotnet_order()
    {
        // .NET orders by the first field (a < b), SQL Server by the last six bytes first (b < a):
        // the SQL paths (fee preview, winner check, priced list) bill b, so Resolve must too.
        var a = new Guid("00000001-0000-0000-0000-000000000002");
        var b = new Guid("00000002-0000-0000-0000-000000000001");
        Assert.True(a.CompareTo(b) < 0);

        var appraisalId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyId, "QT-001", DateTime.Now);
        winner.AddItem(Guid.NewGuid(), appraisalId, 1, 500m, 3).Id = a;
        winner.AddItem(Guid.NewGuid(), appraisalId, 1, 700m, 3).Id = b;
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);

        var result = QuotationFeePrice.Resolve(rfq, appraisalId, companyId);

        Assert.Equal(700m, result.Amount);
    }
}
