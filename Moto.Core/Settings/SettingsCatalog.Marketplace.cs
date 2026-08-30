namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public static MarketplaceSettings Marketplace { get; } = new();

    public partial class MarketplaceSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> TrialEnabled { get; } = new("marketplace.trial.enabled", true, "Active la période d'essai");
        public SettingItem<int> TrialDays { get; } = new("marketplace.trial.days", 14, "Durée essai (jours)");
        public SettingItem<bool> PluginSandboxEnabled { get; } = new("marketplace.sandbox.enabled", true, "Sandbox plugins non vérifiés");
        public SettingItem<bool> AutoVulnScan { get; } = new("marketplace.vulnscan.auto", true, "Scan vulnérabilités auto");
        public SettingItem<bool> MicroDonationsEnabled { get; } = new("marketplace.donations.enabled", true, "Micro-dons plugins OSS");
        public SettingItem<string> PaymentCurrency { get; } = new("marketplace.payment.currency", "EUR", "Devise paiements");
    }
}
