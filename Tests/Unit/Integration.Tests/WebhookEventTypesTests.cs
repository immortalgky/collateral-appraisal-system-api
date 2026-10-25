using FluentAssertions;
using Integration.Domain.WebhookSubscriptions;

namespace Integration.Tests;

public class WebhookEventTypesTests
{
    [Fact]
    public void All_IsEveryConstant_Sorted_AndHasTen()
    {
        var constants = typeof(WebhookEventTypes).GetFields()
            .Where(f => f is { IsLiteral: true, FieldType.Name: nameof(String) })
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        constants.Should().HaveCount(10);
        WebhookEventTypes.All.Should().BeEquivalentTo(constants);
        WebhookEventTypes.All.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }
}
