using NSubstitute;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Request.Domain.RequestTitles;
using Request.Domain.RequestTitles.TitleTypes;
using Request.Infrastructure.Repositories;
using Request.Extensions;
using Shared.Time;
using MediatR;
using Auth.Contracts.Users;

namespace Request.Tests.Request.RequestTitles;

/// <summary>
/// RequestTitles.Id is a v7 Guid and SQL Server sorts those by their random tail, so the list order the
/// requester entered has to be stored: every write path stamps the 1-based position of the incoming list.
/// </summary>
public class TitleSequenceNumberTests
{
    private static RequestTitleDto Dto(string titleNumber, Guid? id = null) => new()
    {
        Id = id,
        CollateralType = "01",
        TitleNumber = titleNumber,
        TitleType = "DEED",
        TitleAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
        DopaAddress = new AddressDto(null, null, null, null, null, null, null, null, null),
        Documents = []
    };

    private static string NumberOf(RequestTitle t) => ((TitleLand)t).TitleDeedInfo!.TitleNumber!;

    [Fact]
    public async Task Create_stamps_the_position_of_each_title_in_the_incoming_list()
    {
        var titleRepository = Substitute.For<IRequestTitleRepository>();
        var service = new CreateRequestService(
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IRequestRepository>(),
            titleRepository,
            Substitute.For<IRequestCommentRepository>(),
            Substitute.For<IRequestUnitOfWork>(),
            Substitute.For<ISender>(),
            Substitute.For<IUserLookupService>());

        var (_, titles) = await service.CreateRequestAsync(
            new CreateRequestData(
                "New", "UI", new UserInfoDto("U1", "creator"), "Normal", false,
                null, null, null, [Dto("A"), Dto("B"), Dto("C")], null, null,
                Requestor: new UserInfoDto("U1", "requestor")),
            TestContext.Current.CancellationToken);

        Assert.Equal(["A", "B", "C"], titles.Select(NumberOf));
        Assert.Equal([1, 2, 3], titles.Select(t => t.SequenceNumber));
    }

    [Fact]
    public async Task Sync_follows_the_incoming_order_for_updated_and_created_rows()
    {
        var requestId = Guid.NewGuid();
        var existing = new[] { "A", "B", "C" }
            .Select(n => TitleFactory.Create("01", Dto(n).ToRequestTitleData() with { RequestId = requestId }))
            .ToList();

        var titleRepository = Substitute.For<IRequestTitleRepository>();
        titleRepository.GetByRequestIdWithDocumentsAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(existing);
        var service = new RequestSyncService(titleRepository);

        // C first, then a new row, then A, B — the new row sits in the middle of the existing ones.
        var result = await service.SyncTitlesAsync(requestId, [
            Dto("C", existing[2].Id),
            Dto("N"),
            Dto("A", existing[0].Id),
            Dto("B", existing[1].Id),
        ], TestContext.Current.CancellationToken);

        var byNumber = result.ToDictionary(NumberOf, t => t.SequenceNumber);
        Assert.Equal(1, byNumber["C"]);
        Assert.Equal(2, byNumber["N"]);
        Assert.Equal(3, byNumber["A"]);
        Assert.Equal(4, byNumber["B"]);
    }
}
