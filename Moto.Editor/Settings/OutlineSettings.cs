// Moto.Editor/Settings/OutlineSettings.cs
// ★ AJOUT (01/10, décision C item 3) : mappage de la famille « Outline Panel »
// (clés op_*). Le panneau Outline n'existait pas ; il est maintenant RÉEL
// (Views/OutlinePanelView + Services/OutlineExtractor + enregistrement dock).
//
// Câblées ici les 2 clés dont la donnée/le comportement existent :
//   • op_button : visibilité du bouton « Outline » dans la barre de statut.
//   • op_dock   : ancrage Right/Left du panneau (Bottom n'est pas un hôte du
//                 système modulaire AddFloatingPanel, qui n'a que gauche/droite).
//
// ⚠️ RESTENT INERTES (hors périmètre de cette tranche) :
//   • op_auto_reveal   : exigerait de suivre la position du curseur dans l'outline
//                       (aucun « révéler » n'est encore branché).
//   • op_auto_fold     : repli des nœuds à un seul enfant (raffinement visuel).
//   • op_indent_guides : guides d'indentation (raffinement visuel).
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
    }
}
