using System;
using System.Collections.Generic;
using Moto.Core.Logging;

namespace Moto.Core.Plugins;

public enum PublisherVerificationStatus { Unverified, Pending, Verified, Revoked }

public sealed class PublisherInfo
{
    public string PublisherId { get; set; } = "";
    public string Name { get; set; } = "";
    public PublisherVerificationStatus Status { get; set; } = PublisherVerificationStatus.Unverified;
    public DateTime? VerifiedAtUtc { get; set; }
}

/// <summary>
/// Item 91 — Programme de vérification des publishers (KYC + signatures).
/// </summary>
public sealed class VerifiedPublisherService
{
    private readonly StructuredLogCollector _log;
    private readonly Dictionary<string, PublisherInfo> _publishers = new();

    public VerifiedPublisherService(StructuredLogCollector log) => _log = log;

    public void RegisterPublisher(PublisherInfo info)
    {
        _publishers[info.PublisherId] = info;
        _log.Info("VerifiedPublisher", "Publisher enregistré", new { info.PublisherId, info.Status });
    }

    public PublisherInfo? GetPublisher(string publisherId)
    {
        return _publishers.TryGetValue(publisherId, out var info) ? info : null;
    }

    public bool IsVerified(string publisherId)
    {
        return _publishers.TryGetValue(publisherId, out var info) &&
               info.Status == PublisherVerificationStatus.Verified;
    }
}
