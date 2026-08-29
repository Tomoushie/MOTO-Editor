using System;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Editor.Services;

/// <summary>
/// Items 54/55 — Orchestre Compact Mode, Focus Mode, Adaptive Font, Theme micro-tuning.
/// Applique les changements via SettingsApplier (live), sans toucher aux handlers existants.
/// </summary>
public sealed class UxModeService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public bool IsCompactMode => _settings.Shared.Editor.Ux.CompactMode.Value;
    public bool IsFocusMode => _settings.Shared.Editor.Ux.FocusMode.Value;

    public event EventHandler? CompactModeChanged;
    public event EventHandler? FocusModeChanged;

    public UxModeService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;

        _settings.Shared.Editor.Ux.CompactMode.Changed += (_, _) => CompactModeChanged?.Invoke(this, EventArgs.Empty);
        _settings.Shared.Editor.Ux.FocusMode.Changed += (_, _) => FocusModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleCompactMode()
    {
        bool newValue = !IsCompactMode;
        _settings.Shared.Editor.Ux.CompactMode.Value = newValue; // persiste immédiatement
        _log.Info("UxMode", "Compact mode", new { newValue });
    }

    public void ToggleFocusMode()
    {
        bool newValue = !IsFocusMode;
        _settings.Shared.Editor.Ux.FocusMode.Value = newValue;
        _log.Info("UxMode", "Focus mode", new { newValue });
    }

    /// <summary>Item 55 — Micro-ajustement du thème, clampé pour rester dans MotoTheme.</summary>
    public void ApplyThemeMicroTuning(int brightnessDelta)
    {
        int clamped = Math.Clamp(brightnessDelta, -10, 10);
        _settings.Shared.Editor.Ux.ThemeMicroTuningBrightness.Value = clamped;
        _log.Info("UxMode", "Theme micro-tuning", new { clamped });
    }
}
