// Moto.Marketplace.Api/Controllers/WebhookController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using Moto.Marketplace.Api.Services;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IPurchaseRepository _purchases;
        private readonly IPluginRepository _plugins;
        private readonly IConfiguration _config;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IPurchaseRepository purchases,
            IPluginRepository plugins,
            IConfiguration config,
            ILogger<WebhookController> logger)
        {
            _purchases = purchases;
            _plugins = plugins;
            _config = config;
            _logger = logger;
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var webhookSecret = _config["Stripe:WebhookSecret"];

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret);

                _logger.LogInformation("[Stripe Webhook] Événement reçu : {Type}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        await HandleCheckoutCompleted(stripeEvent.Data.Object as Session);
                        break;

                    case "charge.refunded":
                        await HandleChargeRefunded(stripeEvent.Data.Object as Charge);
                        break;

                    case "charge.dispute.created":
                        await HandleDisputeCreated(stripeEvent.Data.Object as Dispute);
                        break;

                    default:
                        _logger.LogInformation("[Stripe Webhook] Type non géré : {Type}", stripeEvent.Type);
                        break;
                }

                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "[Stripe Webhook] Erreur signature");
                return BadRequest(new { error = "Signature invalide" });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Stripe Webhook] Erreur traitement");
                return BadRequest(new { error = e.Message });
            }
        }

        private async Task HandleCheckoutCompleted(Session session)
        {
            if (session == null) return;

            var pluginId = session.Metadata?.GetValueOrDefault("plugin_id");
            var userId = session.Metadata?.GetValueOrDefault("user_id");

            if (string.IsNullOrEmpty(pluginId) || string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[Stripe] Métadonnées manquantes dans session {SessionId}", session.Id);
                return;
            }

            // Générer la licence
            var licenseKey = $"MOTO-{Guid.NewGuid():N}-{pluginId.ToUpper().Substring(0, Math.Min(4, pluginId.Length))}";

            var purchase = new PurchaseRecord
            {
                UserId = userId,
                PluginId = pluginId,
                AmountPaid = session.AmountTotal / 100m, // Convertir centimes → euros
                Currency = session.Currency.ToUpper(),
                PurchasedUtc = DateTime.UtcNow,
                StripePaymentIntentId = session.PaymentIntent?.ToString() ?? session.Id,
                LicenseKey = licenseKey,
                Status = LicenseStatus.Active
            };

            await _purchases.SavePurchaseAsync(purchase);

            // Incrémenter le compteur de downloads
            var plugin = await _plugins.GetByIdAsync(pluginId);
            if (plugin != null)
            {
                plugin.DownloadCount++;
                await _plugins.UpdateAsync(plugin);
            }

            _logger.LogInformation(
                "[Stripe] Achat confirmé : {PluginId} par {UserId} → Licence {LicenseKey}",
                pluginId, userId, licenseKey);
        }

        private async Task HandleChargeRefunded(Charge charge)
        {
            if (charge == null) return;

            var purchases = await _purchases.GetPurchasesByPaymentIntentAsync(charge.PaymentIntentId);
            foreach (var purchase in purchases)
            {
                purchase.Status = LicenseStatus.Revoked;
                await _purchases.UpdatePurchaseAsync(purchase);
                _logger.LogInformation(
                    "[Stripe] Remboursement traité : licence {LicenseKey} révoquée",
                    purchase.LicenseKey);
            }
        }

        private async Task HandleDisputeCreated(Dispute dispute)
        {
            if (dispute == null) return;

            _logger.LogWarning(
                "[Stripe] Litige créé : {DisputeId} pour {Amount} {Currency}",
                dispute.Id, dispute.Amount / 100m, dispute.Currency);

            // Marquer les achats concernés pour investigation
            var purchases = await _purchases.GetPurchasesByPaymentIntentAsync(dispute.PaymentIntent);
            foreach (var purchase in purchases)
            {
                purchase.Status = LicenseStatus.Revoked;
                await _purchases.UpdatePurchaseAsync(purchase);
            }
        }
    }
}
