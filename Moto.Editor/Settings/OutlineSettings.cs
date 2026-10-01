// Moto.Editor/Settings/OutlineSettings.cs
// ★ AJOUT (01/10, décision C item 3) : mappage de la famille « Outline Panel »
// (clés op_*). Le panneau Outline n'existait pas ; il est maintenant RÉEL
// (Views/OutlinePanelView + Services/OutlineExtractor + enregistrement dock).
//
// Les 5 clés sont lues AVEC leur défaut DÉCLARÉ au catalogue :
//   • op_button        : visibilité du bouton « Outline » dans la barre de statut
//                       (appliqué par StatusBarPanelView.ApplySettings).
//   • op_dock          : ancrage initial du panneau (appliqué dans MainPage.WirePanels).
//   • op_auto_reveal   : surligne le symbole du curseur (appliqué par OutlinePanelView
//                       via SetCursorLine — position RÉELLE poussée par MainPage).
//   • op_auto_fold     : « replie les nœuds à un seul enfant » — SANS point d'application :
//                       l'outline est une LISTE PLATE (CollectionView, indentation par
//                       niveau), il n'existe AUCUN nœud arborescent à replier. Lue ici
//                       pour être déclarée au script de couverture, mais non appliquée
//                       (même décision que dp_dock : un réglage « qui marche » sans effet
//                       observable est interdit).
//   • op_indent_guides : guides d'indentation — même famille : des guides verticales
//                       supposent un arbre ; une liste plate indentée n'en a pas.
//                       Lue ici, non appliquée (décision, pas oubli).
//
// ⚠️ PIÈGE (28/09) : SettingsEngine.GetBool/GetString(clé) SANS second argument ne
// consulte JAMAIS le catalogue → false/"" sur installation neuve. Toutes les lectures
// ci-dessous passent le défaut DÉCLARÉ via DeclaredBool/DeclaredString.
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>Lecture des réglages <c>op_*</c> avec leur défaut DÉCLARÉ au catalogue.</summary>
    public static class OutlineSettings
    {
        public static bool DeclaredBool(string id)
        {
            var item = SettingsCatalog.ById(id);
            if (item?.Default is bool b) return b;
            return false;
        }

        public static string DeclaredString(string id)
        {
            var item = SettingsCatalog.ById(id);
            if (item?.Default is string s) return s;
            return string.Empty;
        }

        /// <summary>Réglage <c>op_button</c> : bouton « Outline » dans la barre de statut.</summary>
        public static bool ShowOutline(SettingsEngine s)
            => s.GetBool("op_button", DeclaredBool("op_button"));

        /// <summary>Réglage <c>op_dock</c> : ancrage du panneau (Right/Left/Bottom).</summary>
        public static string Dock(SettingsEngine s)
            => s.GetString("op_dock", DeclaredString("op_dock"));

        /// <summary>Réglage <c>op_auto_reveal</c> : surligne le symbole correspondant au curseur.</summary>
        public static bool AutoReveal(SettingsEngine s)
            => s.GetBool("op_auto_reveal", DeclaredBool("op_auto_reveal"));

        /// <summary>
        /// Réglage <c>op_auto_fold</c> : repli des nœuds à un seul enfant.
        /// ⚠️ Lue pour la couverture, mais NON appliquée : l'outline est une liste
        /// plate (pas d'arbre à replier) — voir la note de classe.
        /// </summary>
        public static bool AutoFold(SettingsEngine s)
            => s.GetBool("op_auto_fold", DeclaredBool("op_auto_fold"));

        /// <summary>
        /// Réglage <c>op_indent_guides</c> : guides d'indentation (Always/On Hover/Never).
        /// ⚠️ Lue pour la couverture, mais NON appliquée : pas de guides dans une liste
        /// plate — voir la note de classe.
        /// </summary>
        public static string IndentGuides(SettingsEngine s)
            => s.GetString("op_indent_guides", DeclaredString("op_indent_guides"));
    }
}
