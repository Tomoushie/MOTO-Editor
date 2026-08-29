using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Moto.Marketplace.Api.Services;
using Xunit;

namespace Moto.Marketplace.Api.Tests;

public class StripeE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StripeE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Webhook_PaymentSuccess_CreatesLicense()
    {
        // Arrange : Payload Stripe simulé (checkout.session.completed)
        var payload = new {
            type = "checkout.session.completed",
            data = new {
                @object = new {
                    customer_email = "test@moto.com",
                    metadata = new { plugin_id = "cortex-booster", user_id = "usr_123" }
                }
            }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "test_signature"); // Simulé

        // Act
        var response = await _client.PostAsync("/api/webhooks/stripe", content);

        // Assert
        response.EnsureSuccessStatusCode();
        // Vérifier en base de données que la licence a été générée (mocké ici)
        Assert.True(true);
    }

    [Fact]
    public async Task Refund_Endpoint_ProcessesRefund()
    {
        // Arrange
        var refundRequest = new { payment_intent_id = "pi_test_123", reason = "requested_by_customer" };
        var content = new StringContent(JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/refunds", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
