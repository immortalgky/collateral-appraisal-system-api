using Integration.Application.Features.WebhookSubscriptions.CreateWebhookSubscription;
using Integration.Application.Features.WebhookSubscriptions.UpdateWebhookSubscription;
using Integration.Domain.WebhookSubscriptions;
using Shared.Exceptions;

namespace Collateral.Tests.Webhooks;

/// <summary>
/// Security rules of the webhook subscription (Integration module; its unit tests live here, like the
/// file-interface ones). An editor without WEBHOOK_SECRET_REVEAL must not be able to redirect the stored
/// ClientSecret — or a bearer token minted from it — to a host of their choosing, and no secret may reach
/// the request log.
/// </summary>
public class WebhookSubscriptionSecretTests
{
    private static WebhookSubscription TokenBearer() =>
        WebhookSubscription.Create("LOS", "https://los/cb", null, WebhookAuthType.TokenBearer,
            "https://los/token", "CAS", "ENC:v1:stored", "PUT", "APPRAISAL_PMA_UPDATED");

    [Theory]
    [InlineData("https://evil/token", "CAS", "https://los/cb")]
    [InlineData("https://los/token", "EVIL", "https://los/cb")]
    [InlineData("https://los/token", "CAS", "https://evil/cb")]
    public void Keeping_the_stored_client_secret_is_refused_when_its_target_changes(
        string tokenEndpoint, string clientId, string callbackUrl)
    {
        var subscription = TokenBearer();

        Assert.Throws<DomainException>(() => subscription.Update(
            callbackUrl, "PUT", WebhookAuthType.TokenBearer, tokenEndpoint, clientId, null, null));
        Assert.Equal("https://los/token", subscription.TokenEndpoint);
    }

    [Fact]
    public void Stored_client_secret_is_kept_when_only_the_method_changes()
    {
        var subscription = TokenBearer();

        subscription.Update("https://los/cb", "POST", WebhookAuthType.TokenBearer,
            "https://los/token", "CAS", null, null);

        Assert.Equal("ENC:v1:stored", subscription.ClientSecret);
    }

    [Fact]
    public void A_new_client_secret_allows_changing_the_target()
    {
        var subscription = TokenBearer();

        subscription.Update("https://new/cb", "PUT", WebhookAuthType.TokenBearer,
            "https://new/token", "CAS2", null, "ENC:v1:new");

        Assert.Equal("ENC:v1:new", subscription.ClientSecret);
    }

    [Fact]
    public void Hmac_callback_change_requires_re_entering_the_secret_key()
    {
        var subscription = WebhookSubscription.Create("LOS", "https://los/cb", "ENC:v1:hmac");

        Assert.Throws<DomainException>(() => subscription.Update(
            "https://evil/cb", "POST", WebhookAuthType.Hmac, null, null, null, null));

        subscription.Update("https://los/cb", "PUT", WebhookAuthType.Hmac, null, null, null, null);
        Assert.Equal("ENC:v1:hmac", subscription.SecretKey);

        subscription.Update("https://new/cb", "POST", WebhookAuthType.Hmac, null, null, "ENC:v1:new", null);
        Assert.Equal("ENC:v1:new", subscription.SecretKey);
    }

    [Fact]
    public void Commands_never_print_secrets()
    {
        const string secret = "Hunter2!Secret";
        object[] commands =
        [
            new CreateWebhookSubscriptionCommand("LOS", "https://los/cb", secret, ClientSecret: secret),
            new UpdateWebhookSubscriptionCommand(Guid.NewGuid(), "https://los/cb", secret,
                WebhookAuthType.Hmac, "POST", ClientSecret: secret)
        ];

        foreach (var command in commands)
            Assert.DoesNotContain(secret, command.ToString());
    }
}
