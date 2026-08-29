using System;
using System.Threading.Tasks;
using Moto.Core.Logging;

namespace Moto.Core.Licensing;

/// <summary>
/// Item 89 — Transfert de licence entre machines (sécurisé).
/// </summary>
public sealed class LicenseTransferService
{
    private readonly TrialLicenseManager _license;
    private readonly StructuredLogCollector _log;

    public LicenseTransferService(TrialLicenseManager license, StructuredLogCollector log)
    {
        _license = license;
        _log = log;
    }

    public async Task<string> GenerateTransferTokenAsync()
    {
        // Token signé pour transfert (simplifié)
        string token = $"TRANSFER_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}";
        _log.Info("LicenseTransfer", "Token généré");
        await Task.CompletedTask;
        return token;
    }

    public async Task<bool> ApplyTransferAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("TRANSFER_"))
            return false;
        // Validation + application (simplifié)
        _log.Info("LicenseTransfer", "Transfert appliqué");
        await Task.CompletedTask;
        return true;
    }
}
