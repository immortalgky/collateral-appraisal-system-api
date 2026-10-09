using Appraisal.Contracts.Appraisals;
using FluentAssertions;
using Integration.Application.Features.Appraisals.GetAppraisalDocuments;
using Integration.Application.Services;
using MediatR;
using NSubstitute;

namespace Integration.Tests;

public class GetAppraisalDocumentsQueryHandlerTests
{
    [Fact]
    public async Task The_number_is_looked_up_the_way_the_reappraisal_flow_reads_it()
    {
        var id = Guid.NewGuid();
        var lookup = Substitute.For<IAppraisalLookupService>();
        lookup.ResolvePriorAppraisalByNumberAsync("62A00645", Arg.Any<CancellationToken>())
            .Returns(new PriorAppraisalRef(id, "Completed"));
        var sender = Substitute.For<ISender>();
        var expected = new CarryForwardDocumentsResult(id, "62A00645", []);
        sender.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await new GetAppraisalDocumentsQueryHandler(lookup, sender)
            .Handle(new GetAppraisalDocumentsQuery(" B62a00645 "), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }
}
