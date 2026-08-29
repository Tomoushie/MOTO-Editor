using System;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public enum CollabRole { Editor, Reviewer, Observer }

/// <summary>
/// Idée "Role-based UI" — rôle courant de l'utilisateur dans la session collab.
/// Les vues lisent ce rôle pour adapter leurs panneaux (sans jamais casser l'UI existante).
/// </summary>
public sealed class CollabRoleService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private CollabRole _currentRole = CollabRole.Editor;

    public CollabRole CurrentRole => _settings.Shared.Collab.RoleBasedUiEnabled.Value ? _currentRole : CollabRole.Editor;
    public event EventHandler<CollabRole>? RoleChanged;

    public CollabRoleService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void SetRole(CollabRole role)
    {
        if (_currentRole == role) return;
        _currentRole = role;
        _log.Info("CollabRole", "Rôle changé", new { role = role.ToString() });
        RoleChanged?.Invoke(this, role);
    }

    public bool CanEdit => CurrentRole == CollabRole.Editor;
    public bool CanReview => CurrentRole is CollabRole.Editor or CollabRole.Reviewer;
}
