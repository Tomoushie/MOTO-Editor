using Moto.Core.Settings;

namespace Moto.Editor.Settings;

/// <summary>
/// Mappage de la famille de réglages <c>auto_*</c> (catégorie « Général » /
/// « Éditeur / Enregistrement auto » / « Éditeur / Indentation » /
/// « Agent / Conversation / Documentation », catalogue
/// <c>SettingsCatalog.cs:32,67,68,82,276,280</c> + <c>SettingsCatalog.Extensions.cs:75</c> +
/// <c>SettingsCatalog.Doc.cs:10</c> + <c>SettingsCatalog.AutoLink.cs:10,12,14</c> +
/// <c>SettingsCatalog.Context.cs:14</c> + <c>SettingsCatalog.Platform.cs:10,19,21</c>).
/// 15 clés déclarées au 01/10, dont 2 déjà câblées (<c>doc_auto_update</c>,
/// <c>platform_auto_detect</c>), 2 câblées par ce lot, 11 inertes.
///
/// CÂBLÉES PAR CE LOT (2/15) :
///   • <c>auto_update</c> (T, défaut true) — gate de <see cref="AutoUpdateService.CheckAsync"/>
///     (maintenant lit <c>auto_update</c> au lieu de <c>editor.update.autoCheck</c>).
///   • <c>auto_indent</c> (T, défaut true) — gate des handlers Tab/Enter dans le JS de
///     <c>CodeEditorView</c> via <see cref="CodeEditorView.SetAutoIndent"/>.
///
/// DÉJÀ CÂBLÉES AVANT CE LOT (2/15) :
///   • <c>doc_auto_update</c> (T, défaut true) — gate du watcher FS + debounce 3s dans
///     <c>DocEngine.OnProjectChanged</c> (DocEngine.cs:207).
///   • <c>platform_auto_detect</c> (T, défaut true) — gate de l'auto-analyse panneau
///     Plateforme (<c>PlatformSettings.AutoDetect</c>, MainPage.Panels.cs:671).
///
/// INERTES (11) — une raison par clé (vérifiées le 01/10, preuves fichier:ligne) :
///   • <c>auto_save</c> (E, "Off"/"On Focus Change"/"After Delay") / <c>auto_save_delay</c>
///     (I, 1000ms) : **aucune sauvegarde auto n'existe** — pas de timer, pas de dirty flag
///     (<c>EditorDocument.cs</c> n'a pas <c>IsDirty</c>), pas de <c>SaveTimer</c> dans
///     <c>CodeEditorView</c> ni <c>MainViewModel</c>. Câbler = construire la fonctionnalité
///     (chantier complet : timer debounce + FSW + dirty flag + <c>MainViewModel.SaveDocumentAsync</c>).
///   • <c>auto_compact</c> (T, true) / <c>auto_compact_threshold</c> (S, "90%") :
///     **pas de folding** dans <c>CodeEditorView</c> (JS : aucun <c>fold</c>/<c>collapse</c>), et
///     <c>AgentV2Settings</c> ne lit pas <c>auto_compact</c> (AgentSettings.cs:16-20).
///   • <c>auto_doc</c> (T, true) : **doublon sémantique** de <c>doc_auto_update</c> (même
///     catégorie Agent/Documentation, même défaut) — la clé active est <c>doc_auto_update</c>
///     (<c>DocEngine.cs:207</c>).
///   • <c>autolink_enabled</c> (T, true) / <c>autolink_auto_apply</c> (T, false) /
///     <c>autolink_scan_interval_sec</c> (I, 5) : <c>AutoLinkEngine</c> + UI + palette
///     **existent et fonctionnent** (analyse à la demande), mais **aucun des 3 n'est lu**.
///     Câblable : gate dans <c>ContextAnalyzer.cs:32</c>, timer dans <c>ContextAnalyzer</c>,
///     auto-apply via <c>AutoLinkEngine.Apply()</c> — chantier suivant.
///   • <c>context_auto_apply</c> (T, false) : <c>ContextAnalyzer</c> existe (L15) —
///     suggestions non auto-appliquées. Même pattern qu'autolink_auto_apply.
///   • <c>platform_auto_validate</c> / <c>platform_incremental_validate</c> (T, true) :
///     chaîne produit **MORTE** — <c>PlatformDetector.Analyze</c> ne remplit ni
///     <c>Detections</c> ni <c>Proposals</c>, <c>BuildProposal</c>/
///     <c>AttachContinuousDetection</c> sans appelant, <c>ApplyAsync</c> injoignable
///     (<c>PlatformSettings.cs:26-31</c>). Construire la fonctionnalité d'abord.
///
/// ⚠️ PIÈGE DU DÉPÔT : <c>SettingsEngine.GetBool("clé")</c> SANS second argument
/// renvoie <c>false</c> si la clé est absente du store — le moteur ne consulte jamais
/// le catalogue. Toujours passer le défaut déclaré.
/// </summary>
internal static class AutoSettings
{
    /// <summary>Valeur par défaut déclarée au catalogue (source de vérité).</summary>
    internal static bool DeclaredBool(string id)
        => SettingsCatalog.ById(id)?.Default is bool value && value;

    /// <summary>Valeur par défaut Enum déclarée au catalogue.</summary>
    internal static string? DeclaredEnum(string id)
        => SettingsCatalog.ById(id)?.Default as string;

    /// <summary>Valeur par défaut Int déclarée au catalogue.</summary>
    internal static int DeclaredInt(string id)
        => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

    /// <summary>
    /// <c>auto_update</c> (défaut déclaré : true) — gate de
    /// <see cref="AutoUpdateService.CheckAsync"/>.
    /// </summary>
    internal static bool AutoUpdate(SettingsEngine s)
        => s.GetBool("auto_update", DeclaredBool("auto_update"));

    /// <summary>
    /// <c>auto_indent</c> (défaut déclaré : true) — gate de l'indentation auto
    /// (handlers Tab/Enter) dans <c>CodeEditorView</c>.
    /// </summary>
    internal static bool AutoIndent(SettingsEngine s)
        => s.GetBool("auto_indent", DeclaredBool("auto_indent"));

    /// <summary>
    /// <c>auto_save</c> (E, "Off"/"On Focus Change"/"After Delay") — mode d'enregistrement
    /// auto. ★ AJOUT (01/10, décision C item 4) : la fonctionnalité est maintenant RÉELLE.
    /// </summary>
    internal static string AutoSave(SettingsEngine s)
        => s.GetString("auto_save", DeclaredEnum("auto_save") ?? "Off");

    /// <summary>
    /// <c>auto_save_delay</c> (I, 1000 ms) — délai avant l'enregistrement auto en mode
    /// « After Delay ». ★ AJOUT (01/10, décision C item 4).
    /// </summary>
    internal static int AutoSaveDelay(SettingsEngine s)
        => s.GetInt("auto_save_delay", DeclaredInt("auto_save_delay"));

    // ★ AJOUT (01/10, décision C AutoLink/Context) : les clés du pipeline AutoLink/Context,
    // désormais RÉELLES (ContextEngine résolu + scan périodique + auto-apply).

    /// <summary>Réglage <c>autolink_enabled</c> : gate du scan périodique AutoLink/Context.</summary>
    internal static bool AutolinkEnabled(SettingsEngine s)
        => s.GetBool("autolink_enabled", DeclaredBool("autolink_enabled"));

    /// <summary>Réglage <c>autolink_scan_interval_sec</c> : intervalle (s) du scan périodique.</summary>
    internal static int AutolinkScanIntervalSec(SettingsEngine s)
        => s.GetInt("autolink_scan_interval_sec", DeclaredInt("autolink_scan_interval_sec"));

    /// <summary>Réglage <c>context_auto_apply</c> : applique automatiquement les suggestions Context.</summary>
    internal static bool ContextAutoApply(SettingsEngine s)
        => s.GetBool("context_auto_apply", DeclaredBool("context_auto_apply"));

    // ⚠️ autolink_auto_apply (T, false) reste INERT : il faudrait résoudre AutoLinkEngine
    // (Moto.Core.AI.AutoLink) + analyser + auto-appliquer ses actions — pipeline séparée,
    // pas encore branchée. Pas d'accesseur ici (sinon faux positif de la couverture).
}