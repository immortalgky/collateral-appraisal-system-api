using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Request.Domain.RequestTitles.TitleTypes;
using Request.Extensions;
using Request.Infrastructure;
using Request.Infrastructure.Repositories;

namespace Integration.Request.Integration.Tests;

/// <summary>
/// RequestTitles.Id is a v7 Guid, which SQL Server sorts by its random tail, so reading titles back in Id
/// order shuffles them. The stored SequenceNumber is what keeps the requester's order.
/// </summary>
[Collection("Integration")]
public class RequestTitleOrderTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task Titles_are_read_back_in_the_order_they_were_entered()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();
        var numbers = Enumerable.Range(1, 8).Select(i => $"T{i}").ToList();

        var titles = numbers.Select((n, i) =>
        {
            var dto = new RequestTitleDto
            {
                CollateralType = "01",
                TitleNumber = n,
                TitleType = "DEED",
                TitleAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
                DopaAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
                Documents = []
            };
            var title = TitleFactory.Create("01", dto.ToRequestTitleData() with { RequestId = requestId });
            title.SetSequenceNumber(i + 1);
            return title;
        }).ToList();

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RequestDbContext>();
            // Inserted in reverse, so neither insert order nor Id order can be what makes the test pass.
            db.RequestTitles.AddRange(Enumerable.Reverse(titles));
            await db.SaveChangesAsync(ct);
        }

        using var readScope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var repository = readScope.ServiceProvider.GetRequiredService<IRequestTitleRepository>();

        var withDocuments = await repository.GetByRequestIdWithDocumentsAsync(requestId, ct);
        Assert.Equal(numbers, withDocuments.Select(t => ((TitleLand)t).TitleDeedInfo!.TitleNumber));

        var plain = await repository.GetByRequestIdAsync(requestId, ct);
        Assert.Equal(numbers, plain.Select(t => ((TitleLand)t).TitleDeedInfo!.TitleNumber));
    }
}
