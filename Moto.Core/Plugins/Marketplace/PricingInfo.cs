// Moto.Core/Plugins/Marketplace/PricingInfo.cs
namespace Moto.Core.Plugins.Marketplace
{
    public sealed class PricingInfo
    {
        public PricingModel Model { get; init; } = PricingModel.Free;
        public decimal Price { get; init; } // En EUR, prix unique
        public string Currency { get; init; } = "EUR";
        public string? StripePriceId { get; init; } // ID Stripe pour paiement
        public bool RequiresLicense { get; init; }
    }

    public enum PricingModel
    {
        Free,           // Gratuit
        OneTimePurchase // Achat unique (pas d'abonnement)
    }

    public sealed class PurchaseRecord
    {
        public string UserId { get; init; } = string.Empty;
        public string PluginId { get; init; } = string.Empty;
        public decimal AmountPaid { get; init; }
        public string Currency { get; init; } = "EUR";
        public DateTime PurchasedUtc { get; init; } = DateTime.UtcNow;
        public string StripePaymentIntentId { get; init; } = string.Empty;
        public string LicenseKey { get; init; } = string.Empty;
        public LicenseStatus Status { get; set; } = LicenseStatus.Active;
    }

    public enum LicenseStatus { Active, Revoked, Expired }
}
