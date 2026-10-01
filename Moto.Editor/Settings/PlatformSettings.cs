using Moto.Core.Settings;

namespace Moto.Editor.Settings;

/// <summary>
/// Mappage de la famille de réglages <c>platform_*</c> (catégorie « Agent /
/// Platform Engine », fichier catalogue <c>SettingsCatalog.Platform.cs</c>).
///
/// 8 clés déclarées. AVANT ce lot, UNE seule était lue — et avec le piège
/// payé trois fois : <c>MainPage.Panels.cs:671</c> appelait
/// <c>GetBool("platform_auto_detect")</c> SANS défaut déclaré → <c>false</c>
/// sur installation neuve, alors que la fenêtre Réglages affiche « ON »
/// (l'UI prend le défaut du catalogue). Faux état classique : réglage affiché
/// activé, auto-analyse jamais faite. La lecture passe par
/// <see cref="AutoDetect"/> qui fournit TOUJOURS le défaut déclaré (true).
///
/// ⚠️ Les 7 autres clés restent inertes — la chaîne produit est MORTE en
/// amont (vérifié le 01/10, preuves fichier:ligne) :
///   • <c>platform_include_linux</c> : <c>PlatformGenerators.AddLinux</c> est
///     appelé sans condition, mais <c>BuildProposal</c> n'a AUCUN appelant et
///     <c>report.Proposals</c> n'est jamais rempli — le panneau affiche « 0
///     portage(s) proposé(s) » en permanence, aucune proposition à filtrer ;
///   • <c>platform_generate_ci</c> / <c>platform_ci_provider</c> /
///     <c>platform_auto_validate</c> / <c>platform_incremental_validate</c> :
///     propriétés <c>PlatformEngine.GenerateCi/CiProvider/AutoValidate/
///     IncrementalValidate</c> jamais assignées depuis les réglages, mais
///     leur seul consommateur atteignable (<c>ApplyAsync</c>, bouton «
///     Générer » de <c>PlatformView</c>) est injoignable tant que la liste des
///     propositions est vide (idem ci-dessus). Assigner ces propriétés sans
///     plus ne changerait AUCUN comportement observable = câblage cosmétique,
///     interdit ;
///   • <c>platform_avalonia_linux</c> : <c>AvaloniaLinuxGenerator.Generate</c>
///     est appelé inconditionnellement dans la même <c>AddLinux</c> orpheline ;
///   • <c>platform_smart_detect</c> : <c>PlatformEngine.AttachContinuous-
///     Detection</c> n'a AUCUN appelant (et dépendrait en plus de
///     <c>doc_auto_update</c> via <c>DocEngine.SourceFileChanged</c>).
/// Construire les propositions de portage = construire la fonctionnalité
/// (chantier), pas câbler un réglage.
///
/// ⚠️ PIÈGE DU DÉPÔT : <c>SettingsEngine.GetBool("clé")</c> SANS second
/// argument renvoie <c>false</c> si la clé est absente du store — le moteur ne
/// consulte jamais le catalogue. Toujours passer le défaut déclaré.
/// </summary>
internal static class PlatformSettings
{
    /// <summary>Valeur par défaut déclarée au catalogue (source de vérité).</summary>
    internal static bool DeclaredBool(string id)
        => SettingsCatalog.ById(id)?.Default is bool value && value;

    /// <summary>
    /// <c>platform_auto_detect</c> (défaut déclaré : true) : gate de
    /// l'auto-analyse du panneau Plateforme à l'ouverture d'un projet
    /// (<c>MainPage.Panels.LoadWorkspace</c>).
    /// </summary>
    internal static bool AutoDetect(SettingsEngine s)
        => s.GetBool("platform_auto_detect", DeclaredBool("platform_auto_detect"));
}
