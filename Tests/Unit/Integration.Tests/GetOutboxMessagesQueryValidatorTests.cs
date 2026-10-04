using FluentAssertions;
using Integration.Application.Features.OutboxMessages.GetOutboxMessages;

namespace Integration.Tests;

public class GetOutboxMessagesQueryValidatorTests
{
    private static GetOutboxMessagesQuery QueryFor(string? module) =>
        new(PageNumber: 1, PageSize: 20, Status: "Failed", Module: module, Search: null);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("request")]
    [InlineData("reporting")]
    public void Module_NullBlankOrWhitelisted_IsAccepted(string? module)
    {
        new GetOutboxMessagesQueryValidator().Validate(QueryFor(module)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Request")]
    [InlineData("unknown")]
    [InlineData("request]; DROP TABLE [request].[IntegrationEventOutbox]--")]
    public void Module_NotOnTheWhitelist_IsRejected(string module)
    {
        new GetOutboxMessagesQueryValidator().Validate(QueryFor(module)).IsValid.Should().BeFalse();
    }
}
