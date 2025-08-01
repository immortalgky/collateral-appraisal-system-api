using NSubstitute;
using Request.Data.Repository;
using Request.Requests.Services;
using Request.Requests.ValueObjects;

namespace Request.Tests.Request.Requests.Services;

public class AppraisalNumberGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_NormalInput_ShouldReturnNumber()
    {
        var requestRepository = Substitute.For<IRequestRepository>();
        requestRepository.GetNextAppraisalNumber(CancellationToken.None).Returns(1);
        var generator = new AppraisalNumberGenerator(requestRepository);
        var result = await generator.GenerateAsync(CancellationToken.None);
        Assert.IsType<AppraisalNumber>(result);
        Assert.Equal(9, result.Value.Length);
    }
}