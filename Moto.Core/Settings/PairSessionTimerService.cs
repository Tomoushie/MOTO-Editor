using System;
using System.Timers;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

/// <summary>
/// Idée "Time-boxed pair sessions" (P2) — timer de session collaborative.
/// Les ephemeral cursors sont gérés par CollabPresence existant ; ici, seul le timer.
/// </summary>
public sealed class PairSessionTimerService : IDisposable
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private System.Timers.Timer? _timer;
    private DateTime _endsAtUtc;

    public bool IsActive { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public event EventHandler? SessionEnded;
    public event EventHandler? Tick;

    public PairSessionTimerService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void StartSession(int? minutesOverride = null)
    {
        if (!_settings.Shared.Collab.PairSessionsEnabled.Value) return;
        int minutes = minutesOverride ?? _settings.Shared.Collab.PairSessionDefaultMinutes.Value;

        _endsAtUtc = DateTime.UtcNow.AddMinutes(minutes);
        IsActive = true;
        _timer = new System.Timers.Timer(1000) { AutoReset = true };
        _timer.Elapsed += OnTick;
        _timer.Start();
        _log.Info("PairSession", "Session démarrée", new { minutes });
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        Remaining = _endsAtUtc - DateTime.UtcNow;
        if (Remaining <= TimeSpan.Zero)
        {
            EndSession();
            SessionEnded?.Invoke(this, EventArgs.Empty);
        }
        else Tick?.Invoke(this, EventArgs.Empty);
    }

    public void EndSession()
    {
        IsActive = false;
        _timer?.Stop();
        _log.Info("PairSession", "Session terminée");
    }

    public void Dispose() => _timer?.Dispose();
}
