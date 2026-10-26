using Integration.Collateral.Integration.Tests;
using Integration.Fixtures;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Request.Application.EventHandlers.Reappraisal;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Infrastructure;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Integration.Request.Integration.Tests;

/// <summary>
/// The call site that copies AS400's collateral id and Auto Update flag from the Initiate event onto the
/// request. Both are <c>string?</c> and adjacent, so swapping them compiles; only a test sees it.
/// </summary>
[Collection("Integration")]
public class ReappraisalInitiatedHandlerTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task TheRequestCarriesTheEventsCollateralIdAndAutoUpdateFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var book = $"8{Random.Shared.Next(1_000_000, 9_999_999)}";

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var handler = new ReappraisalInitiatedIntegrationEventHandler(
                sp.GetRequiredService<RequestDbContext>(),
                sp.GetRequiredService<ICreateRequestService>(),
                sp.GetRequiredService<global::Request.IRequestUnitOfWork>(),
                sp.GetRequiredService<ISqlConnectionFactory>(),
                sp.GetRequiredService<ISender>(),
                sp.GetRequiredService<InboxGuard<RequestDbContext>>(),
                NullLogger<ReappraisalInitiatedIntegrationEventHandler>.Instance);

            var message = new ReappraisalInitiatedIntegrationEvent
            {
                GroupNumber = $"G-{Guid.NewGuid():N}"[..20],
                Source = "Candidate",
                SurveyNumber = book,
                CollateralId = "60088001",
                AutoUpdate = "Y",
                Requestor = new UserInfoDto("U1", "u1"),
                Creator = new UserInfoDto("U1", "u1"),
            };

            await handler.Consume(FakeConsumeContext.Build(message, NewId.NextGuid(), ct));
        }

        using var verify = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var request = await verify.ServiceProvider.GetRequiredService<RequestDbContext>()
            .Requests.AsNoTracking().SingleAsync(r => r.ReappraisalBookNumber == book, ct);

        Assert.Equal("60088001", request.ReappraisalCollateralId);
        Assert.Equal("Y", request.ReappraisalAutoUpdate);
    }

    // A legacy book (no CAS appraisal behind it) that is not a 99A number: Initiate sends its number, the consumer
    // records it with SetLegacyPriorBook, and the submit guard accepts it because it is the draft's own book.
    [Fact]
    public async Task ANonNinetyNineABook_IsRecordedAsTheLegacyPriorAndPassesTheSubmitGuard()
    {
        var ct = TestContext.Current.CancellationToken;
        var book = $"8{Random.Shared.Next(1_000_000, 9_999_999)}";

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var handler = new ReappraisalInitiatedIntegrationEventHandler(
                sp.GetRequiredService<RequestDbContext>(),
                sp.GetRequiredService<ICreateRequestService>(),
                sp.GetRequiredService<global::Request.IRequestUnitOfWork>(),
                sp.GetRequiredService<ISqlConnectionFactory>(),
                sp.GetRequiredService<ISender>(),
                sp.GetRequiredService<InboxGuard<RequestDbContext>>(),
                NullLogger<ReappraisalInitiatedIntegrationEventHandler>.Instance);

            await handler.Consume(FakeConsumeContext.Build(new ReappraisalInitiatedIntegrationEvent
            {
                GroupNumber = $"G-{Guid.NewGuid():N}"[..20],
                Source = "Candidate",
                SurveyNumber = book,
                CollateralId = "60088002",
                PrevAppraisalNumber = book,
                PrevAppraisalValue = 1_500_000m,
                Requestor = new UserInfoDto("U1", "u1"),
                Creator = new UserInfoDto("U1", "u1"),
            }, NewId.NextGuid(), ct));
        }

        using var verify = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var request = await verify.ServiceProvider.GetRequiredService<RequestDbContext>()
            .Requests.AsNoTracking().SingleAsync(r => r.ReappraisalBookNumber == book, ct);

        Assert.Null(request.Detail!.PrevAppraisalId);
        Assert.Equal(book, request.Detail.PrevAppraisalNumber);
        Assert.Equal(1_500_000m, request.Detail.PrevAppraisalValue);

        // The resubmit path runs the same submit guard: the number alone passes for purpose 03 because it is the
        // draft's own book (it would be a 400 for any other free-text number).
        var user = new UserInfoDto("U1", "u1");
        var data = new ResubmitRequestData(
            request.Id, "03", "SIBS", user, user, "Normal", false,
            new RequestDetailDto(false, null, null, null, null, null, null, book, 1_500_000m, null),
            [], [], null, null, null);
        var updated = await verify.ServiceProvider.GetRequiredService<IUpdateRequestService>()
            .ResubmitRequestAsync(data, ct);

        Assert.Equal(book, updated.Detail!.PrevAppraisalNumber);
    }
}
