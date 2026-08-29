using System;
using System.Collections.Generic;
using Moto.Core.Logging;

namespace Moto.Core.Plugins.Marketplace;

public sealed class SubscriptionBundle
{
    public string BundleId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public IReadOnlyList<string> IncludedPluginIds { get; set; } = Array.Empty<string>();
    public int MaxSeats { get; set; } = 1;
}

/// <summary>
/// Item 93 — Bundles d'abonnement (plugins + thèmes + family seats).
/// </summary>
public sealed class SubscriptionBundleService
{
    private readonly StructuredLogCollector _log;
    private readonly List<SubscriptionBundle> _bundles = new();

    public SubscriptionBundleService(StructuredLogCollector log) => _log = log;

    public void AddBundle(SubscriptionBundle bundle)
    {
        _bundles.Add(bundle);
        _log.Info("SubscriptionBundle", "Bundle ajouté", new { bundle.BundleId });
    }

    public IReadOnlyList<SubscriptionBundle> GetBundles() => _bundles;
}

/// <summary>
/// Item 99 — Micro-dons pour plugins open source.
/// </summary>
public sealed class MicroDonationService
{
    private readonly StructuredLogCollector _log;

    public MicroDonationService(StructuredLogCollector log) => _log = log;

    public async Task<bool> DonateAsync(string pluginId, decimal amount, string currency)
    {
        // En production : intégration Stripe/PayPal
        _log.Info("MicroDonation", "Don effectué", new { pluginId, amount, currency });
        await System.Threading.Tasks.Task.CompletedTask;
        return true;
    }
}
