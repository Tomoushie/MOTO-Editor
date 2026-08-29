// Moto.Marketplace.Api/Services/RefundService.cs
using Microsoft.Extensions.Configuration;
using Stripe;

namespace Moto.Marketplace.Api.Services
{
    public interface IRefundService
    {
        Task<RefundResult> RequestRefundAsync(string licenseKey, string userId, string reason);
        Task<RefundResult> ProcessRefundAsync(string purchaseId);
        Task<IReadOnlyList<RefundRequest>> GetPendingRefundsAsync();
    }

    public sealed class RefundResult
    {
        public bool Success { get; init; }
        public string? RefundId { get; init; }
        public string? Error { get; init; }
        public decimal? RefundedAmount { get; init; }
    }

    public sealed class RefundRequest
    {
        public string Id { get; init; } = string.Empty;
        public string PurchaseId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public DateTime RequestedUtc { get; init; } = DateTime.UtcNow;
        public RefundStatus Status { get; set; } = RefundStatus.Pending;
    }

    public enum RefundStatus { Pending, Approved, Rejected, Processed }

    public sealed class StripeRefundService : IRefundService
    {
        private readonly IPurchaseRepository _purchases;
        private readonly IRefundRepository _refunds;
        private readonly IConfiguration _config;
        private readonly ILogger<StripeRefundService> _logger;

        public StripeRefundService(
            IPurchaseRepository purchases,
            IRefundRepository refunds,
            IConfiguration config,
            ILogger<StripeRefundService> logger)
        {
            _purchases = purchases;
            _refunds = refunds;
            _config = config;
            _logger = logger;
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        public async Task<RefundResult> RequestRefundAsync(string licenseKey, string userId, string reason)
        {
            var purchase = await _purchases.GetPurchaseByLicenseKeyAsync(licenseKey);
            if (purchase == null)
                return new RefundResult { Success = false, Error = "Licence non trouvée" };

            if (purchase.UserId != userId)
                return new RefundResult { Success = false, Error = "Licence non autorisée" };

            if (purchase.Status != LicenseStatus.Active)
                return new RefundResult { Success = false, Error = "Licence déjà révoquée" };

            // Vérifier si le remboursement est dans les 30 jours
            var daysSincePurchase = (DateTime.UtcNow - purchase.PurchasedUtc).TotalDays;
            if (daysSincePurchase > 30)
                return new RefundResult { Success = false, Error = "Délai de remboursement dépassé (30 jours)" };

            var refundRequest = new RefundRequest
            {
                Id = Guid.NewGuid().ToString(),
                PurchaseId = purchase.StripePaymentIntentId,
                UserId = userId,
                Reason = reason
            };

            await _refunds.SaveRefundRequestAsync(refundRequest);
            _logger.LogInformation("[Refund] Demande créée : {RefundId}", refundRequest.Id);

            return new RefundResult { Success = true };
        }

        public async Task<RefundResult> ProcessRefundAsync(string purchaseId)
        {
            try
            {
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = purchaseId,
                    Reason = RefundReasons.RequestedByCustomer
                };

                var service = new RefundService();
                var refund = await service.CreateAsync(refundOptions);

                _logger.LogInformation("[Refund] Remboursement effectué : {RefundId}", refund.Id);

                return new RefundResult
                {
                    Success = true,
                    RefundId = refund.Id,
                    RefundedAmount = refund.Amount / 100m
                };
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "[Refund] Erreur Stripe");
                return new RefundResult { Success = false, Error = e.Message };
            }
        }

        public async Task<IReadOnlyList<RefundRequest>> GetPendingRefundsAsync()
        {
            return await _refunds.GetPendingRefundsAsync();
        }
    }
}
