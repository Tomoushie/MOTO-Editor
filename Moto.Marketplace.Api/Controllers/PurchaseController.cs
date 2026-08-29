// Moto.Marketplace.Api/Controllers/PurchaseController.cs
using Microsoft.AspNetCore.Mvc;
using Moto.Marketplace.Api.Services;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PurchaseController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPluginRepository _plugins;
        private readonly IPurchaseRepository _purchases;

        public PurchaseController(
            IPaymentService paymentService,
            IPluginRepository plugins,
            IPurchaseRepository purchases)
        {
            _paymentService = paymentService;
            _plugins = plugins;
            _purchases = purchases;
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request)
        {
            var plugin = await _plugins.GetByIdAsync(request.PluginId);
            if (plugin == null)
                return NotFound(new { error = "Plugin non trouvé" });

            if (plugin.Pricing?.Model != PricingModel.OneTimePurchase)
                return BadRequest(new { error = "Ce plugin n'est pas à vendre" });

            var result = await _paymentService.CreateCheckoutSessionAsync(
                request.PluginId,
                request.UserId,
                plugin.Pricing.Price,
                plugin.Pricing.Currency);

            return result.Success
                ? Ok(new { checkoutUrl = result.CheckoutUrl, sessionId = result.SessionId })
                : BadRequest(new { error = result.Error });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPurchase([FromBody] VerifyRequest request)
        {
            var isValid = await _paymentService.VerifyPaymentAsync(request.PaymentIntentId);
            if (!isValid)
                return BadRequest(new { error = "Paiement invalide" });

            var licenseKey = await _paymentService.GenerateLicenseKeyAsync(request.UserId, request.PluginId);
            return Ok(new { licenseKey, message = "Achat confirmé !" });
        }

        [HttpGet("licenses/{userId}")]
        public async Task<IActionResult> GetUserLicenses(string userId)
        {
            var licenses = await _purchases.GetPurchasesByUserAsync(userId);
            return Ok(licenses);
        }

        [HttpPost("validate-license")]
        public async Task<IActionResult> ValidateLicense([FromBody] ValidateLicenseRequest request)
        {
            var purchase = await _purchases.GetPurchaseByLicenseKeyAsync(request.LicenseKey);
            if (purchase == null)
                return BadRequest(new { valid = false, error = "Licence invalide" });

            if (purchase.UserId != request.UserId || purchase.PluginId != request.PluginId)
                return BadRequest(new { valid = false, error = "Licence non autorisée pour cet utilisateur/plugin" });

            if (purchase.Status != LicenseStatus.Active)
                return BadRequest(new { valid = false, error = "Licence révoquée ou expirée" });

            return Ok(new { valid = true, purchasedUtc = purchase.PurchasedUtc });
        }
    }

    public record CheckoutRequest(string PluginId, string UserId);
    public record VerifyRequest(string PaymentIntentId, string UserId, string PluginId);
    public record ValidateLicenseRequest(string LicenseKey, string UserId, string PluginId);
}
