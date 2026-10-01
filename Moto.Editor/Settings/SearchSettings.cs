using Moto.Core.Settings;

namespace Moto.Editor.Settings;

/// <summary>
/// Mappage de la famille de réglages <c>search_*</c> + <c>seed_search_from_cursor</c>
/// (catégorie « Recherche &amp; Fichiers », section « Recherche », catalogue
/// <c>SettingsCatalog.cs:95-102</c>). 8 clés déclarées, toutes inertes au 01/10.
///
/// CÂBLÉE (1/8) : <see cref="IncludeIgnored"/> — pilote le filtrage .gitignore du
/// panneau de recherche de fichiers (<c>SearchView</c>). Le panneau possède SA propre
/// instance de <c>FileTreeService</c> (<c>SearchView.xaml.cs:24</c>), distincte de celle
/// de l'explorateur : c'est pour cela que la recherche affichait TOUJOURS les fichiers
/// gitignorés alors que l'explorateur les masquait quand <c>pp_hide_gitignore</c> est
/// activé. Le défaut déclaré a été corrigé (false → true, voir le commentaire au
/// catalogue) pour rejoindre cet état réel — aucun changement visible au câblage.
///
/// ⚠️ Double clé volontairement tranchée : <c>file_finder_include_ignored</c> (Enum
/// Smart/Always/Never, section « File Finder », catalogue L103) décrit EXACTEMENT le
/// même widget avec une autre polarité. Un seul interrupteur par widget :
/// <c>search_include_ignored</c> est le propriétaire ; <c>file_finder_include_ignored</c>
/// reste inerte (logique « Smart » jamais définie de toute façon).
///
/// INERTES (7) — une raison par clé (vérifiées le 01/10, preuves fichier:ligne) :
///   • <c>search_whole_word</c> / <c>search_case_sensitive</c> / <c>search_smartcase</c> /
///     <c>search_regex</c> / <c>search_wrap</c> / <c>search_center_on_match</c> : la
///     recherche de CONTENU (find in file, find in files, barre Ctrl+F) N'EXISTE PAS.
///     L'éditeur WebView n'expose aucune fonction find (<c>CodeEditorView.xaml.cs:360-721</c> :
///     set/goLine/... seulement), le Ctrl+F du navigateur est désactivé (<c>id.:253-256</c>),
///     aucun FindBar, aucun ReplaceAll, aucun raccourci Ctrl+F (<c>MainPage.Shortcuts.cs:40-65</c>).
///     Seul existe le matching de NOM de fichier (<c>FileTreeService.SearchFiles:169</c>,
///     figé IgnoreCase) — l'appliquer à ces clés détournerait leur libellé (doctrine
///     <c>GitSettings.cs:43-47</c>). Câbler = construire le find, pas activer un réglage.
///     ⚠️ <c>search_wrap</c> a pour défaut déclaré TRUE : une lecture par
///     GetBool SANS second argument (le piège du dépôt, cf. plus bas) renverrait
///     false et inverserait le réglage. Le mansonge produit préexistant <c>edit.search</c> (Ctrl+F annoncé
///     « Recherche dans le fichier » au catalogue de commandes,
///     <c>CommandPaletteEngine.cs:152</c>) qui ouvre en réalité le bandeau IA
///     (<c>MainPage.Routing.cs:60</c>) fait partie du même chantier.
///   • <c>seed_search_from_cursor</c> : le pré-remplissage à l'ouverture du panneau
///     exige l'API « mot sous le curseur » (word-at-caret) — inexistante côté JS et C#.
///     Seul le mode <c>On Selection</c> serait implémentable via
///     <c>CodeEditorView.GetSelectedText()</c>, mais le mode par défaut <c>Always</c>
///     resterait faux. Reporté avec la construction du find.
///
/// ⚠️ PIÈGE DU DÉPÔT : <c>SettingsEngine.GetBool("clé")</c> SANS second argument
/// renvoie <c>false</c> si la clé est absente du store — le moteur ne consulte jamais
/// le catalogue. Toujours passer le défaut déclaré.
/// </summary>
internal static class SearchSettings
{
    /// <summary>Valeur par défaut déclarée au catalogue (source de vérité).</summary>
    internal static bool DeclaredBool(string id)
        => SettingsCatalog.ById(id)?.Default is bool value && value;

    /// <summary>
    /// <c>search_include_ignored</c> (défaut déclaré : true — corrigé le 01/10) :
    /// vrai = les fichiers du .gitignore apparaissent dans les résultats de
    /// <c>SearchView</c>. Faux = ils sont masqués.
    /// </summary>
    internal static bool IncludeIgnored(SettingsEngine s)
        => s.GetBool("search_include_ignored", DeclaredBool("search_include_ignored"));
}
