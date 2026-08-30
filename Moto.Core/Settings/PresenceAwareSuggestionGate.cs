using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

/// <summary>
/// Idée "Presence-aware suggestions" (P2) — évite l'IA lourde quand beaucoup de
/// collaborateurs sont actifs. S'interface avec CollabPresence existant et gate
/// ContextEngine / ProactiveSuggestions (MOTO AI assiste, ne structure pas).
/// </summary>
public sealed class PresenceAwareSuggestionGate
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private int _activeCollaborators;

    public PresenceAwareSuggestionGate(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Appelé par CollabPresence quand la présence change.</summary>
    public void UpdateActiveCollaborators(int count)
    {
        _activeCollaborators = count;
        _log.Debug("PresenceGate", "Présence mise à jour", new { count });
    }

    /// <summary>Retourne false si l'IA lourde doit être suspendue.</summary>
    public bool ShouldRunHeavyAi()
    {
        if (!SettingsCatalog.Collab.PresenceAwareSuggestions.Value) return true;
        int threshold = SettingsCatalog.Collab.PresenceHeavyAiThreshold.Value;
        bool allowed = _activeCollaborators < threshold;
        if (!allowed)
            _log.Info("PresenceGate", "IA lourde suspendue (forte présence)",
                      new { _activeCollaborators, threshold });
        return allowed;
    }
}
