using Appraisal.Contracts.Appraisals;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Request.Application.Features.Reappraisal.CreateBlockReappraisal;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Shared.Data;
using Shared.Exceptions;

namespace Request.Tests.Request.Reappraisal;

/// <summary>The purpose-09 guard runs first: a prior that is not Completed stops the command before a group number
/// is generated or anything is read, so the endpoint can hand the due row back.</summary>
public class CreateBlockReappraisalGuardTests
{
    [Fact]
    public async Task A_prior_that_is_not_Completed_is_rejected_before_anything_else_runs()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetAppraisalReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new AppraisalReferenceResult("69000001", 1m, null, "InProgress"));
        var groupNumbers = Substitute.For<IReappraisalGroupNumberGenerator>();
        var createService = Substitute.For<ICreateRequestService>();
        var user = new UserInfoDto("U1", "u1");

        var handler = new CreateBlockReappraisalCommandHandler(
            createService, groupNumbers, Substitute.For<ISqlConnectionFactory>(), null!, mediator,
            NullLogger<CreateBlockReappraisalCommandHandler>.Instance);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateBlockReappraisalCommand(Guid.NewGuid(), user, user), TestContext.Current.CancellationToken));

        Assert.Contains("completed", ex.Message);
        await groupNumbers.DidNotReceive().GenerateAsync(Arg.Any<CancellationToken>());
        await createService.DidNotReceiveWithAnyArgs().CreateRequestAsync(default!, default);
    }
}
