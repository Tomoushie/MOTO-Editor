using Microsoft.Extensions.Logging;

namespace Moto.Core.Services;

/// <summary>
/// Service de polling adaptatif.
/// Adapte la fréquence selon l'activité : actif → 1s, inactif → 30s.
/// </summary>
public sealed class AdaptivePollingService : IDisposable
{
    private readonly ILogger<AdaptivePollingService> _logger;
    private readonly Dictionary<string, PollingChannel> _channels = new();
    private readonly System.Timers.Timer _masterTimer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _isActive = true;

    public AdaptivePollingService(ILogger<AdaptivePollingService> logger)
    {
        _logger = logger;
        _masterTimer = new System.Timers.Timer(1000); // Tick maître 1s
        _masterTimer.Elapsed += OnMasterTick;
        _masterTimer.AutoReset = true;
        _masterTimer.Start();
    }

    /// <summary>
    /// Enregistre une activité utilisateur (resynchronise le polling).
    /// </summary>
    public void NotifyActivity()
    {
        _lastActivity = DateTime.UtcNow;
        _isActive = true;
    }

    /// <summary>
    /// Crée un canal de polling adaptatif.
    /// </summary>
    public PollingChannel CreateChannel(
        string name,
        Func<Task> pollAction,
        TimeSpan? activeInterval = null,
        TimeSpan? idleInterval = null)
    {
        var channel = new PollingChannel(
            name,
            pollAction,
            activeInterval ?? TimeSpan.FromSeconds(1),
            idleInterval ?? TimeSpan.FromSeconds(30));

        _channels[name] = channel;
        _logger.LogDebug("Canal de polling créé: {Name}", name);

        return channel;
    }

    private async void OnMasterTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Vérifie si l'utilisateur est inactif depuis 30s
        var idleTime = DateTime.UtcNow - _lastActivity;
        if (idleTime > TimeSpan.FromSeconds(30))
        {
            _isActive = false;
        }

        foreach (var channel in _channels.Values)
        {
            if (!channel.IsEnabled) continue;

            var interval = _isActive ? channel.ActiveInterval : channel.IdleInterval;

            if (DateTime.UtcNow - channel.LastPoll >= interval)
            {
                channel.LastPoll = DateTime.UtcNow;
                try
                {
                    await channel.PollAction();
                    channel.SuccessCount++;
                }
                catch (Exception ex)
                {
                    channel.FailureCount++;
                    _logger.LogWarning(ex, "Échec du polling pour {Channel}", channel.Name);
                }
            }
        }
    }

    /// <summary>
    /// Statistiques globales du polling.
    /// </summary>
    public PollingStats GetStats() => new()
    {
        IsActive = _isActive,
        IdleSeconds = (DateTime.UtcNow - _lastActivity).TotalSeconds,
        ChannelCount = _channels.Count,
        Channels = _channels.Values.Select(c => new ChannelStats
        {
            Name = c.Name,
            SuccessCount = c.SuccessCount,
            FailureCount = c.FailureCount,
            IsEnabled = c.IsEnabled
        }).ToList()
    };

    public void Dispose()
    {
        _masterTimer.Stop();
        _masterTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class PollingChannel
{
    public string Name { get; }
    public Func<Task> PollAction { get; }
    public TimeSpan ActiveInterval { get; }
    public TimeSpan IdleInterval { get; }
    public DateTime LastPoll { get; set; } = DateTime.MinValue;
    public bool IsEnabled { get; set; } = true;
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }

    public PollingChannel(string name, Func<Task> action, TimeSpan active, TimeSpan idle)
    {
        Name = name;
        PollAction = action;
        ActiveInterval = active;
        IdleInterval = idle;
    }
}

public class PollingStats
{
    public bool IsActive { get; init; }
    public double IdleSeconds { get; init; }
    public int ChannelCount { get; init; }
    public List<ChannelStats> Channels { get; init; } = new();
}

public class ChannelStats
{
    public string Name { get; init; } = "";
    public long SuccessCount { get; init; }
    public long FailureCount { get; init; }
    public bool IsEnabled { get; init; }
}
