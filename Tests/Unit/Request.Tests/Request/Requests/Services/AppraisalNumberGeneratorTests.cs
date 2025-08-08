using NSubstitute;
using Request.Requests;
using Request.Requests.ValueObjects;
using Request.Services;
using Shared.Data;

namespace Request.Tests.Request.Requests.Services;

public class AppraisalNumberGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_NormalInput_ShouldReturnNumber()
    {
        // var requestRepository = Substitute.For<IRequestRepository>();
        // requestRepository.GetNextAppraisalNumberAsync(CancellationToken.None).Returns(1);
        //
        //
        // var sqlConnectionFactory = Substitute.For<ISqlConnectionFactory>();
        // var generator = new AppraisalNumberGenerator(sqlConnectionFactory);
        // var result = await generator.GenerateAsync(CancellationToken.None);
        // Assert.IsType<AppraisalNumber>(result);
        // Assert.Equal(9, result.Value.Length);
    }
}