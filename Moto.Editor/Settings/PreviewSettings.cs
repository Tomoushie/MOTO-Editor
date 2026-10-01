// Moto.Editor/Settings/PreviewSettings.cs
// ★ AJOUT (01/10, décision C item 2) : mappage de la famille « Onglets aperçu »
// (clés preview_*). Ces 6 clés étaient inertes : le concept d'« onglet aperçu »
// (onglet temporaire en italique, remplacé par le prochain) n'existait nulle part.
//
// Le concept est maintenant RÉEL (EditorDocument.IsPreview + MainViewModel.OpenFilePath
// asPreview + rendu italique + permanence à l'édition). Sont câblées ici les 2 clés dont
// la donnée/le déclencheur existent :
//   • preview_enabled       : interrupteur maître (si faux, aucun aperçu n'est créé).
//   • preview_project_panel : l'explorateur de fichiers ouvre en APERÇU (simple clic).
//
// ⚠️ RESTENT INERTES (hors périmètre de cette tranche, constat VÉRIFIÉ) :
//   • preview_file_finder   : le file finder n'existe pas (item 5 de la feuille de route).
//   • preview_multibuffer   : aucun « multibuffer » n'existe.
//   • preview_code_nav      : aucune navigation de code (go-to-definition) n'existe.
//   • preview_keep_on_nav   : dépend de preview_code_nav (inexistant).
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// Lecture des réglages <c>preview_*</c> avec leur valeur par défaut DÉCLARÉE au
    /// catalogue. Même patron que <see cref="StatusBarSettings"/> / <see cref="TabBarSettings"/>.
    /// </summary>
    public static class PreviewSettings
    {
        /// <summary>Défaut booléen déclaré au catalogue pour cette clé.</summary>
        public static bool DeclaredBool(string id)
        {
            var item = SettingsCatalog.ById(id);
            if (item?.Default is bool b) return b;
            return false;
        }

        /// <summary>Réglage <c>preview_enabled</c> : interrupteur maître des onglets aperçu.</summary>
        public static bool Enabled(SettingsEngine s)
            => s.GetBool("preview_enabled", DeclaredBool("preview_enabled"));

        /// <summary>Réglage <c>preview_project_panel</c> : l'explorateur ouvre en aperçu.</summary>
        public static bool ProjectPanel(SettingsEngine s)
            => s.GetBool("preview_project_panel", DeclaredBool("preview_project_panel"));
    }
}
