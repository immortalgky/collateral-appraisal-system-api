using FluentAssertions;
using Integration.Application.Features.OutboxMessages;

namespace Integration.Tests;

public class OutboxModuleWhitelistTests
{
    [Theory]
    [InlineData("request")]
    [InlineData("appraisal")]
    [InlineData("document")]
    [InlineData("workflow")]
    [InlineData("collateral")]
    [InlineData("reporting")]
    public void IsValid_WhitelistedModule_ReturnsTrue(string module)
    {
        OutboxModuleWhitelist.IsValid(module).Should().BeTrue();
    }

    [Fact]
    public void IsValid_NullModule_ReturnsFalse()
    {
        OutboxModuleWhitelist.IsValid(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("Request")] // wrong case — whitelist is exact lowercase schema names
    [InlineData("unknown")]
    [InlineData("request]; DROP TABLE [request].[IntegrationEventOutbox]--")]
    [InlineData("request].[IntegrationEventOutbox] WHERE 1=1--")]
    public void IsValid_UnknownOrInjectionLikeModule_ReturnsFalse(string module)
    {
        OutboxModuleWhitelist.IsValid(module).Should().BeFalse();
    }
}
