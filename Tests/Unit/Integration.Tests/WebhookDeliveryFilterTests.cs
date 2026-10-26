using Dapper;
using FluentAssertions;
using Integration.Application.Features.WebhookDeliveries;

namespace Integration.Tests;

public class WebhookDeliveryFilterTests
{
    private static (List<string> Conditions, DynamicParameters Parameters) Build(
        Guid? subscriptionId = null, string? systemCode = null, string? eventType = null, string? search = null,
        DateTime? from = null, DateTime? to = null)
    {
        var parameters = new DynamicParameters();
        return (WebhookDeliveryFilter.Conditions(parameters, subscriptionId, systemCode, eventType, search, from, to), parameters);
    }

    [Fact]
    public void NoFilters_NoWhere()
    {
        var (conditions, _) = Build();

        conditions.Should().BeEmpty();
        WebhookDeliveryFilter.Where(conditions).Should().BeEmpty();
    }

    [Fact]
    public void SubscriptionId_IsExactMatch()
    {
        var id = Guid.NewGuid();

        var (conditions, p) = Build(subscriptionId: id);

        conditions.Should().Equal("d.SubscriptionId = @SubscriptionId");
        p.Get<Guid>("SubscriptionId").Should().Be(id);
    }

    [Fact]
    public void SystemCodeAndEventType_AreExactMatches()
    {
        var (conditions, p) = Build(systemCode: "CLS", eventType: "QUOTATION_READY");

        conditions.Should().Equal("s.SystemCode = @SystemCode", "d.EventType = @EventType");
        p.Get<string>("SystemCode").Should().Be("CLS");
        p.Get<string>("EventType").Should().Be("QUOTATION_READY");
    }

    [Fact]
    public void Search_Text_IsEscapedLike_WithoutIdMatch()
    {
        var (conditions, p) = Build(search: "50%_[x]");

        conditions.Should().ContainSingle().Which.Should().NotContain("d.Id");
        p.Get<string>("SearchLike").Should().Be(@"%50\%\_\[x]%");
    }

    [Fact]
    public void Search_Guid_AlsoMatchesDeliveryId()
    {
        var id = Guid.NewGuid();

        var (conditions, p) = Build(search: id.ToString());

        conditions.Should().ContainSingle().Which.Should().Contain("d.Id = @SearchId");
        p.Get<Guid>("SearchId").Should().Be(id);
    }

    [Fact]
    public void Where_JoinsWithAnd()
    {
        var (conditions, _) = Build(from: new DateTime(2026, 1, 1), to: new DateTime(2026, 2, 1));

        WebhookDeliveryFilter.Where(conditions).Should().Be("WHERE d.CreatedAt >= @FromDate AND d.CreatedAt <= @ToDate");
    }
}
