using FluentAssertions;
using Shared.Configurations;

namespace Shared.Tests.Configuration;

public class OutboxDeliveryJobOptionsTests
{
    [Fact]
    public void Validate_Defaults_Pass()
    {
        var act = () => new OutboxDeliveryJobOptions().Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_PublishTimeoutExactlyHalfOfLease_Passes()
    {
        var options = new OutboxDeliveryJobOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
            PublishTimeout = TimeSpan.FromSeconds(15)
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_PublishTimeoutOverHalfOfLease_Throws()
    {
        // 10s renewal threshold (30/3) + 20s publish = 30s without a renewal == the whole lease.
        var options = new OutboxDeliveryJobOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
            PublishTimeout = TimeSpan.FromSeconds(20)
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*half of LeaseDuration*");
    }
}
