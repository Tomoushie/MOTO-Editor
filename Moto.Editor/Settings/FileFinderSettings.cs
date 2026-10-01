// Moto.Editor/Settings/FileFinderSettings.cs
// ★ AJOUT (01/10, décision C item 5) : mappage de la famille « File Finder »
// (clés file_finder_*). Le « file finder » (Quick Open) n'existe pas en tant que tel ;
// la recherche de fichiers par nom (Views/SearchView) en est l'équivalent réel. Est
// câblée ici la clé dont la donnée existe :
//   • file_finder_icons : icône du type de fichier dans les résultats de la recherche.
//
// ⚠️ RESTENT INERTES (hors périmètre, constat VÉRIFIÉ) :
//   • file_finder_include_ignored : doublon sémantique de search_include_ignored
//     (déjà câblé dans SearchView.ApplyVisibilityRules) — le « Smart » de l'enum
//     n'a pas d'équivalent précis dans le service.
//   • file_finder_skip_focus : la recherche ne « focus » jamais le fichier actif
//     (il n'y a pas de surlignage du fichier actif dans les résultats), donc rien à
//     basculer — le comportement « ne pas focus » est déjà celui d'origine.
//   • file_scan_* (depth/exclusions/inclusions) et file_types : aucun indexeur de
//     fichiers configurable n'existe (FileTreeService scanne à la demande, sans
//     profondeur/exclusions réglables) — chantier séparé (Quick Open complet).
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>Lecture des réglages <c>file_finder_*</c> avec leur défaut DÉCLARÉ.</summary>
    public static class FileFinderSettings
    {
        public static bool DeclaredBool(string id)
        {
            var item = SettingsCatalog.ById(id);
            if (item?.Default is bool b) return b;
            return false;
        }

        /// <summary>Réglage <c>file_finder_icons</c> : icônes dans les résultats de recherche.</summary>
        public static bool ShowIcons(SettingsEngine s)
            => s.GetBool("file_finder_icons", DeclaredBool("file_finder_icons"));
    }
}
