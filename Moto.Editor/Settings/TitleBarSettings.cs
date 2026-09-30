// Moto.Editor/Settings/TitleBarSettings.cs
using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit les réglages de la famille « Fenêtre &amp; Layout / Title Bar »
    /// (clés <c>tb_*</c>) pour la VRAIE barre de titre du dépôt
    /// (<see cref="Moto.Editor.Views.CustomMenuBarView"/>, instanciée par
    /// <c>MainPage.xaml</c> sous <c>x:Name="MenuBar"</c>).
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées au catalogue depuis l'origine mais
    /// lues par AUCUN code compilé — la fenêtre Réglages les affichait, rien ne les
    /// appliquait (constat mesuré par scripts/settings-coverage.ps1). Même patron que
    /// <see cref="TabBarSettings"/> (chantier tabs_* du 30/09) : un seul endroit qui
    /// traduit le catalogue en effets visuels, appelé depuis
    /// <c>MainPage.ApplyLayoutSettings</c> (démarrage, chaque changement de réglage et
    /// retour de plein écran).
    ///
    /// ⚠️ PIÈGE PAYÉ (28/09, re-documenté ici car c'est LE piège de ce dépôt) :
    /// <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie <c>false</c> quand
    /// la clé est absente du store — le moteur ne consulte JAMAIS le catalogue
    /// (<c>SettingsEngineCore.cs</c> : <c>GetBool(key, false)</c>). Sur une installation
    /// neuve, appliquer <c>tb_menus</c> ou <c>tb_project_items</c> sans défaut aurait donc
    /// MASQUÉ la barre de menus ou le nom du projet alors que le catalogue les déclare à
    /// <c>true</c>. Toutes les lectures ci-dessous passent explicitement le défaut DÉCLARÉ,
    /// qui reste ainsi la seule source de vérité.
    ///
    /// CE QUI N'EST PAS FAIT ICI (volontairement, voir le rapport de fin de chantier) :
    ///   - <c>tb_branch_icon</c> : aucune police du dépôt ne possède de glyphe « branche »
    ///     (constat déjà documenté dans <c>Controls/MotoIcons.cs</c> : « aucun glyphe de
    ///     branche n'existe dans cette police »). Afficher un glyphe approchant serait une
    ///     donnée inventée ;
    ///   - <c>tb_worktree</c> : aucun concept de worktree git n'existe dans le dépôt
    ///     (une seule occurrence de « worktree » hors catalogue, et c'est une variable
    ///     locale de parsing de <c>git status</c>) ;
    ///   - <c>tb_onboarding</c> : aucune bannière de nouvelles fonctionnalités n'existe ;
    ///   - <c>tb_sign_in</c> / <c>tb_user_menu</c> / <c>tb_user_picture</c> : il n'existe
    ///     AUCUN compte utilisateur MOTO (pas de service de compte, pas de photo, pas de
    ///     menu utilisateur). Le seul compte réel est le GitHub OAuth
    ///     (<c>GitHubAccountService</c>), et son point d'entrée dans la barre est déjà
    ///     l'avatar/engrenage — le recâbler sur ces 3 clés afficherait un état de
    ///     connexion MOTO qui n'existe pas.
    /// </summary>
    internal static class TitleBarSettings
    {
        /// <summary>Vrai si la barre doit afficher les menus de navigation (Fichiers/Recherche/…).</summary>
        internal static bool ShowMenus(SettingsEngine settings)
            => settings.GetBool("tb_menus", DeclaredBool("tb_menus"));

        /// <summary>Vrai si la barre doit afficher l'hôte et le nom du projet.</summary>
        internal static bool ShowProjectItems(SettingsEngine settings)
            => settings.GetBool("tb_project_items", DeclaredBool("tb_project_items"));

        /// <summary>Vrai si la barre doit afficher le nom de la branche git courante.</summary>
        internal static bool ShowBranchName(SettingsEngine settings)
            => settings.GetBool("tb_branch_name", DeclaredBool("tb_branch_name"));

        /// <summary>
        /// Position des 3 contrôles de fenêtre. Valeurs possibles au catalogue :
        /// "Platform Default" (défaut), "Left", "Right". Toute valeur inconnue est
        /// ramenée au défaut DÉCLARÉ (jamais à une position inventée) — la barre ne
        /// peut donc jamais se retrouver dans un état que le catalogue n'annonce pas.
        /// </summary>
        internal static TitleBarButtonLayout ResolveButtonLayout(SettingsEngine settings)
        {
            var raw = settings.GetString("tb_button_layout", DeclaredString("tb_button_layout"));
            if (string.Equals(raw, "Left", StringComparison.OrdinalIgnoreCase))
                return TitleBarButtonLayout.Left;
            if (string.Equals(raw, "Right", StringComparison.OrdinalIgnoreCase))
                return TitleBarButtonLayout.Right;

            // "Platform Default" (et toute valeur inconnue) : Windows place les contrôles
            // de fenêtre à DROITE — c'est la position réelle actuelle de la barre, donc
            // le défaut du catalogue décrit bien le comportement observé, on ne change rien.
            return TitleBarButtonLayout.Right;
        }

        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte/énuméré (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;
    }

    /// <summary>Position des contrôles de fenêtre (Réduire/Agrandir/Fermer) dans la barre de titre.</summary>
    internal enum TitleBarButtonLayout
    {
        Left,
        Right
    }
}
