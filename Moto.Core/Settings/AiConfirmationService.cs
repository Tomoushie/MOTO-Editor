// Moto.Core/Settings/AiConfirmationService.cs
// Service de confirmation pour les actions sensibles déclenchées par l'IA.
// Permet à l'utilisateur de valider/annuler avant application.
using System;
using System.Threading.Tasks;

namespace Moto.Core.Settings
{
    /// <summary>Type d'action nécessitant une confirmation.</summary>
    public enum ConfirmationAction
    {
        ModifySetting,
        InstallPlugin,
        RollbackSettings,
        MigrateSettings,
        DeleteFile,
        ModifyCode,
        ExecuteCommand
    }

    /// <summary>Détails de la demande de confirmation.</summary>
    public sealed class ConfirmationRequest
    {
        public ConfirmationAction Action { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public string ConfirmText { get; init; } = "Appliquer";
        public string CancelText { get; init; } = "Annuler";
        public bool IsDestructive { get; init; }
    }

    /// <summary>Résultat d'une confirmation.</summary>
    public sealed class ConfirmationResult
    {
        public bool Confirmed { get; init; }
        public string? Reason { get; init; }

        public static ConfirmationResult Yes() => new() { Confirmed = true };
        public static ConfirmationResult No(string? reason = null) => new() { Confirmed = false, Reason = reason };
    }

    /// <summary>
    /// Service de confirmation : délègue l'affichage UI à un handler injecté.
    /// Le moteur reste portable (pas de dépendance MAUI).
    /// </summary>
    public sealed class AiConfirmationService
    {
        /// <summary>
        /// Handler UI injecté par MainPage.
        /// Reçoit une ConfirmationRequest et retourne true/false.
        /// </summary>
        public Func<ConfirmationRequest, Task<bool>>? ConfirmationHandler { get; set; }

        /// <summary>
        /// Demande confirmation à l'utilisateur.
        /// Si aucun handler n'est injecté, refuse par défaut (sécurité).
        /// </summary>
        public async Task<ConfirmationResult> RequestAsync(ConfirmationRequest request)
        {
            if (ConfirmationHandler == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Confirmation] Aucun handler UI, refus par défaut : {request.Title}");
                return ConfirmationResult.No("Aucun handler de confirmation disponible.");
            }

            try
            {
                var confirmed = await ConfirmationHandler(request);
                return confirmed ? ConfirmationResult.Yes() : ConfirmationResult.No("Annulé par l'utilisateur.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Confirmation] Erreur handler : {ex.Message}");
                return ConfirmationResult.No($"Erreur : {ex.Message}");
            }
        }

        // ── Helpers pour les cas courants ──

        public Task<ConfirmationResult> ConfirmSettingChangeAsync(string key, object? oldValue, object newValue)
            => RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.ModifySetting,
                Title = "🤖 MOTO AI veut modifier un paramètre",
                Message = $"Modification de '{key}'",
                Details = $"Ancienne valeur : {oldValue?.ToString() ?? "(null)"}\n" +
                          $"Nouvelle valeur : {newValue?.ToString() ?? "(null)"}",
                ConfirmText = "Appliquer",
                IsDestructive = false
            });

        public Task<ConfirmationResult> ConfirmPluginInstallAsync(string pluginName, string version)
            => RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.InstallPlugin,
                Title = "🧩 Installer un plugin",
                Message = $"Installer {pluginName} v{version} ?",
                Details = "Le plugin sera téléchargé depuis le marketplace et activé au prochain redémarrage.",
                ConfirmText = "Installer",
                IsDestructive = false
            });

        public Task<ConfirmationResult> ConfirmRollbackAsync(string backupName)
            => RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.RollbackSettings,
                Title = "🔄 Restaurer les paramètres",
                Message = $"Restaurer depuis {backupName} ?",
                Details = "Les paramètres actuels seront sauvegardés avant restauration.",
                ConfirmText = "Restaurer",
                IsDestructive = true
            });

        public Task<ConfirmationResult> ConfirmMigrationAsync(int keyCount)
            => RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.MigrateSettings,
                Title = "🔄 Migrer les paramètres",
                Message = $"{keyCount} paramètres à migrer vers le nouveau format.",
                Details = "Un backup sera créé automatiquement.",
                ConfirmText = "Migrer",
                IsDestructive = false
            });

        public Task<ConfirmationResult> ConfirmDeleteFileAsync(string filePath)
            => RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.DeleteFile,
                Title = "🗑️ Supprimer un fichier",
                Message = $"Supprimer {System.IO.Path.GetFileName(filePath)} ?",
                Details = $"Chemin : {filePath}\nCette action est irréversible.",
                ConfirmText = "Supprimer",
                CancelText = "Annuler",
                IsDestructive = true
            });
    }
}
