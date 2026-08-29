// Moto.Marketplace.Api/Services/StripePaymentService.cs
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace Moto.Marketplace.Api.Services
{
    public interface IPaymentService
    {
        Task<PaymentResult> CreateCheckoutSessionAsync(string pluginId, string userId, decimal amount, string currency);
        Task<bool> VerifyPaymentAsync(string paymentIntentId);
        Task<string> GenerateLicenseKeyAsync(string userId, string pluginId);
    }

    public sealed class PaymentResult
    {
        public bool Success { get; init; }
        public string? CheckoutUrl { get; init; }
        public string? SessionId { get; init; }
        public string? Error { get; init; }
    }

    public sealed class StripePaymentService : IPaymentService
    {
        private readonly IConfiguration _config;
        private readonly IPurchaseRepository _purchases;

        public StripePaymentService(IConfiguration config, IPurchaseRepository purchases)
        {
            _config = config;
            _purchases = purchases;
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        public async Task<PaymentResult> CreateCheckoutSessionAsync(
            string pluginId, string userId, decimal amount, string currency)
        {
            try
            {
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = currency.ToLower(),
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"MOTO Plugin - {pluginId}",
                                },
                                UnitAmount = (long)(amount * 100), // Stripe utilise des centimes
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    SuccessUrl = $"{_config["App:BaseUrl"]}/purchase/success?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_config["App:BaseUrl"]}/purchase/cancel",
                    Metadata = new Dictionary<string, string>
                    {
                        { "plugin_id", pluginId },
                        { "user_id", userId }
                    }
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                return new PaymentResult
                {
                    Success = true,
                    CheckoutUrl = session.Url,
                    SessionId = session.Id
                };
            }
            catch (Exception ex)
            {
                return new PaymentResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<bool> VerifyPaymentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                return paymentIntent.Status == "succeeded";
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GenerateLicenseKeyAsync(string userId, string pluginId)
        {
            // Générer une clé de licence unique
            var licenseKey = $"MOTO-{Guid.NewGuid():N}-{pluginId.ToUpper().Substring(0, 4)}";

            await _purchases.SavePurchaseAsync(new PurchaseRecord
            {
                UserId = userId,
                PluginId = pluginId,
                LicenseKey = licenseKey,
                PurchasedUtc = DateTime.UtcNow
            });

            return licenseKey;
        }
    }
}
