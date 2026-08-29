// Moto.Core/Settings/ConfirmationPolicyEngine.cs
// Politique de confirmation configurable : strict / relaxed.
using System;
using System.Collections.Generic;

namespace Moto.Core.Settings
{
    public enum ConfirmationMode
    {
        /// <summary>Toujours confirmer (défaut).</summary>
        Strict,
        /// <summary>Confirmer uniquement les actions destructives.</summary>
        Relaxed
    }

    /// <summary>
    /// Moteur de politique de confirmation.
    /// Lit le paramètre "ai_confirm_settings_changes" (strict/relaxed).
    /// </summary>
    public sealed class ConfirmationPolicyEngine
    {
        private readonly SettingsEngine _settings;

        public ConfirmationPolicyEngine(SettingsEngine settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public ConfirmationMode GetMode()
        {
            var modeStr = _settings.GetString("ai_confirm_mode", "strict");
            return modeStr.ToLowerInvariant() switch
            {
                "relaxed" => ConfirmationMode.Relaxed,
                _ => ConfirmationMode.Strict
            };
        }

        /// <summary>
        /// Détermine si une action nécessite une confirmation.
        /// </summary>
        public bool RequiresConfirmation(ConfirmationAction action)
        {
            var mode = GetMode();

            // En mode strict : tout nécessite confirmation
            if (mode == ConfirmationMode.Strict)
                return true;

            // En mode relaxed : uniquement les actions destructives
            return action switch
            {
                ConfirmationAction.DeleteFile => true,
                ConfirmationAction.RollbackSettings => true,
                ConfirmationAction.MigrateSettings => true,
                _ => false
            };
        }

        /// <summary>
        /// Journal des confirmations (pour audit).
        /// </summary>
        public void LogConfirmation(ConfirmationAction action, bool confirmed, string details = "")
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Confirmation] {action} : {(confirmed ? "CONFIRMÉ" : "ANNULÉ")} — {details}");
        }
    }
}
