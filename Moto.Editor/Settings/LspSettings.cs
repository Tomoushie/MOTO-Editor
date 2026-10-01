// Moto.Editor/Settings/LspSettings.cs
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (LSP) : lit et applique les réglages de la famille « Langages &amp; Outils / LSP »
    /// (clés <c>lsp_enabled</c>, <c>lsp_completions</c>, <c>lsp_diagnostics</c>, <c>lsp_highlights</c>).
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées dans le catalogue de Réglages mais lues par
    /// AUCUN code compilé (sauf lsp_diagnostics, lu SANS défaut dans SettingsApplier — voir le piège
    /// ci-dessous) — la fenêtre Réglages les affichait, rien ne les appliquait réellement.
    ///
    /// ⚠️ PIÈGE PAYÉ (voir TabBarSettings.cs) : <c>SettingsEngine.GetBool("clé")</c> SANS second
    /// argument renvoie <c>false</c> quand la clé est absente du store — le moteur ne consulte
    /// JAMAIS le catalogue. Les lectures ci-dessous passent donc explicitement le défaut DÉCLARÉ.
    ///
    /// Effet : <c>lsp_enabled</c> gouverne l'initialisation du gestionnaire LSP (au démarrage) ;
    /// <c>lsp_completions</c>/<c>lsp_highlights</c> gouvernent les requêtes de complétion et de
    /// surbrillance sémantique émises par l'intégration éditeur ; <c>lsp_diagnostics</c> gouverne
    /// l'affichage des diagnostics dans l'éditeur (déjà branché via SettingsApplier).
    /// </summary>
    internal static class LspSettings
    {
        /// <summary>Le moteur LSP est-il activé ? (défaut déclaré : oui)</summary>
        internal static bool Enabled(SettingsEngine settings)
            => settings.GetBool("lsp_enabled", DeclaredBool("lsp_enabled"));

        /// <summary>La complétion LSP est-elle activée ? (défaut déclaré : oui)</summary>
        internal static bool CompletionsEnabled(SettingsEngine settings)
            => settings.GetBool("lsp_completions", DeclaredBool("lsp_completions"));

        /// <summary>Les diagnostics LSP sont-ils affichés ? (défaut déclaré : oui)</summary>
        internal static bool DiagnosticsEnabled(SettingsEngine settings)
            => settings.GetBool("lsp_diagnostics", DeclaredBool("lsp_diagnostics"));

        /// <summary>La surbrillance sémantique LSP est-elle activée ? (défaut déclaré : oui)</summary>
        internal static bool HighlightsEnabled(SettingsEngine settings)
            => settings.GetBool("lsp_highlights", DeclaredBool("lsp_highlights"));

        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;
    }
}
