using Moto.Core.Settings;

namespace Moto.Editor.Settings;

/// <summary>
/// Mappage de la famille de réglages <c>agent_*</c> (catégorie « Apparence /
/// Police agent » et « IA Locale »).
///
/// 11 clés déclarées au catalogue :
///   • <c>agent_font_size</c> (int 8..30, défaut 13) — SEULE clé du lot
///     <c>agent_*</c> encore à câbler côté éditeur : c'est la taille du texte du
///     panneau de chat IA (<c>Views/AiChatView</c>). Appliquée par
///     <c>MainPage.ApplyAgentAndCollabPanelSettings</c> (ressource
///     <c>AgentFontSize</c>, en <c>DynamicResource</c> dans le XAML — même
///     mécanisme que <c>TerminalFontSize</c> pour le dock du bas).
///   • Les 8 clés « IA Locale » (<c>agent_engine</c>, <c>agent_model</c>,
///     <c>agent_num_ctx</c>, <c>agent_max_steps</c>, <c>agent_max_minutes</c>,
///     <c>agent_tool_mode</c>, <c>agent_thought</c>, <c>agent_verify_command</c>)
///     sont déjà lues par <c>Moto.Core/Moto.AI/Autonomy/V2/AgentV2Settings.cs</c>
///     — opérantes, effet pris au PROCHAIN run de l'agent (pas en direct).
///   • <c>agent_skills</c> et <c>agent_sandbox</c> sont des boutons
///     <c>Action</c> : le mécanisme <c>SettingItem.ActionRequested</c> n'a aucun
///     abonné et aucun écran « skills »/« permissions sandbox » n'existe —
///     inertes volontaires (voir CLAUDE.md).
///
/// ⚠️ PIÈGE DU DÉPÔT (payé trois fois) : <c>SettingsEngine.GetInt("clé")</c>
/// SANS second argument renvoie 0 quand la clé est absente du store — le moteur
/// ne consulte jamais le catalogue. Toujours passer le défaut déclaré.
/// </summary>
internal static class AgentSettings
{
    /// <summary>Valeur par défaut déclarée au catalogue (source de vérité).</summary>
    internal static int DeclaredInt(string id)
        => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

    internal static string DeclaredString(string id)
        => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

    /// <summary>
    /// <c>agent_font_size</c> : taille du texte du panneau IA, clampée aux
    /// bornes DÉCLARÉES du catalogue (8..30).
    /// </summary>
    internal static int FontSize(SettingsEngine s)
    {
        var declared = DeclaredInt("agent_font_size");
        return Math.Clamp(s.GetInt("agent_font_size", declared), 8, 30);
    }

    /// <summary>
    /// ★ AJOUT (06/10, câblage thinking_display) : le réglage <c>thinking_display</c>
    /// (catégorie « Agent Configuration », hors préfixe <c>agent_*</c>) gouverne
    /// l'affichage du raisonnement du CHAT. « Always Expanded » déplie par défaut ;
    /// les autres valeurs (« Auto », « Preview », « Always Collapsed ») le replient —
    /// l'utilisateur peut toujours cliquer sur « ✦ Thinking » pour déplier.
    /// </summary>
    internal static bool AlwaysExpandThinking(SettingsEngine s)
        => string.Equals(s.GetString("thinking_display", DeclaredString("thinking_display")), "Always Expanded", StringComparison.OrdinalIgnoreCase);
}
