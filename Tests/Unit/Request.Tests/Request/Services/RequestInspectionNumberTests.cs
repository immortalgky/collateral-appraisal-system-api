using Appraisal.Contracts.Appraisals;
using MediatR;
using NSubstitute;
using Request.Application.Services;
using Request.Domain.Requests;
using Shared.Models;

namespace Request.Tests.Request.Services;

/// <summary>
/// The request page asks the Appraisal module for the inspection round only for a submitted 06 / 11 request.
/// </summary>
public class RequestInspectionNumberTests
{
    private static Domain.Requests.Request NewRequest(string purpose, bool submitted)
    {
        var request = Domain.Requests.Request.Create(new RequestData(
            purpose, "LOS", new UserInfo("u", "U"), new UserInfo("u", "U"), DateTime.Now, "Normal", false));
        if (submitted)
            request.Submit(new DateTime(2026, 10, 1, 9, 0, 0));
        return request;
    }

    private static ISender SenderReturning(int? number)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetInspectionNumberByRequestQuery>(), Arg.Any<CancellationToken>()).Returns(number);
        return sender;
    }

    [Theory]
    [InlineData("06")]
    [InlineData("11")]
    public async Task SubmittedInspection_ReturnsTheAppraisalsRound(string purpose)
    {
        var request = NewRequest(purpose, submitted: true);
        var sender = SenderReturning(3);

        var result = await RequestInspectionNumber.ResolveAsync(request, sender, CancellationToken.None);

        Assert.Equal(3, result);
        await sender.Received(1).Send(
            Arg.Is<GetInspectionNumberByRequestQuery>(q => q.RequestId == request.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmittedInspection_NoAppraisalYet_IsNull()
    {
        var result = await RequestInspectionNumber.ResolveAsync(
            NewRequest("06", submitted: true), SenderReturning(null), CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("06", false)] // draft: no appraisal can exist
    [InlineData("02", true)]  // not a construction inspection
    [InlineData("07", true)]
    public async Task NotAnInspectionOrNotSubmitted_SkipsTheCrossModuleCall(string purpose, bool submitted)
    {
        var sender = SenderReturning(9);

        var result = await RequestInspectionNumber.ResolveAsync(
            NewRequest(purpose, submitted), sender, CancellationToken.None);

        Assert.Null(result);
        await sender.DidNotReceiveWithAnyArgs().Send(default(GetInspectionNumberByRequestQuery)!, default);
    }
}
