// Moto.Editor/Settings/TerminalSettings.cs
using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit et applique les réglages de la famille « Terminal »
    /// (clés <c>terminal_*</c>) — le VRAI dock du bas
    /// (<see cref="Moto.Editor.Views.TerminalPanelView"/>), alimenté par
    /// <c>Moto.Core.Services.TerminalService</c> (cmd/bash en direct).
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées au catalogue (catégorie
    /// « Terminal », 22 clés) mais lues par AUCUN code compilé — la fenêtre
    /// Réglages les affichait, rien ne les appliquait (constat
    /// <c>scripts/settings-coverage.ps1</c>). Même méthode que
    /// <see cref="TabBarSettings"/>, <see cref="PanelSettings"/>,
    /// <see cref="GitPanelSettings"/> et <see cref="DockPanelSettings"/> :
    /// un seul endroit qui traduit le catalogue en effets réels.
    ///
    /// ⚠️ PIÈGE PAYÉ DEUX FOIS (28/09, 01/10) — c'est LE piège de ce dépôt :
    /// <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie
    /// <c>false</c> quand la clé est absente du store (le moteur ne consulte
    /// JAMAIS le catalogue). Sur une installation neuve, appliquer
    /// <c>terminal_audible_bell</c> sans défaut aurait donc MASQUÉ la valeur
    /// déclarée. Toutes les lectures ci-dessous passent explicitement le défaut
    /// DÉCLARÉ, seule source de vérité.
    ///
    /// OÙ C'EST APPLIQUÉ :
    ///   - <c>terminal_font_size</c>, <c>terminal_font_family</c>,
    ///     <c>terminal_default_height</c> : <c>MainPage.ApplyTerminalSettings</c>
    ///     (appelée par <c>ApplyLayoutSettings</c> et par le préfixe
    ///     <c>terminal_</c> de <c>SettingsWindow.RealSettingChanged</c>) ;
    ///   - <c>terminal_max_scroll_lines</c>, <c>terminal_audible_bell</c> :
    ///     <c>MainViewModel.OnTerminalOutput</c> (à chaque ligne du shell).
    ///
    /// CE QUI RESTE INERTE DANS CETTE FAMILLE (une raison par clé, jamais un
    /// oubli) — voir aussi la note de fin de fichier pour le reste à faire :
    ///   - <c>terminal_font_weight</c> : MAUI 8 n'a PAS de `FontWeight` sur
    ///     `Label` (seulement `FontAttributes` None/Bold/Italic — `FontWeight`
    ///     n'est arrivé qu'en MAUI 10) : une graisse numérique 100-900 n'a
    ///     aucun support d'affichage ici ;
    ///   - <c>terminal_cursor_shape</c> / <c>terminal_cursor_blinking</c> /
    ///     <c>terminal_alternate_scroll</c> : suppose un émulateur VT. La
    ///     sortie est une liste de lignes (`CollectionView` de `TerminalLine`),
    ///     le seul curseur réel est celui du `Entry` de saisie (natif WinUI,
    ///     non configurable) — il n'y a ni curseur de terminal ni écran
    ///     alterné à configurer ;
    ///   - <c>terminal_option_as_meta</c> : clé Option = touche Meta, sémantique
    ///     macOS ; l'application est Windows uniquement (`net8.0-windows`) ;
    ///   - <c>terminal_copy_on_select</c> / <c>terminal_keep_selection_on_copy</c>
    ///     : la sortie n'est PAS sélectionnable (des `Label` dans une
    ///     `CollectionView`, sans modèle de sélection) — câbler exigerait de
    ///     construire ce modèle ;
    ///   - <c>terminal_open_links_mouse</c> : aucune détection de liens dans la
    ///     sortie du shell (donnée inexistante) ;
    ///   - <c>terminal_default_width</c> : le terminal est un dock DU BAS qui
    ///     occupe les 3 colonnes du <c>RootGrid</c> — il n'a pas de largeur
    ///     réglable (seule sa hauteur est ajustable, poignée
    ///     <c>BottomDockResizeHandle</c>) ;
    ///   - <c>terminal_show_scrollbar</c> : contrairement au panneau Git (qui
    ///     enferme son contenu dans un `ScrollView` explicite), la sortie du
    ///     terminal est une `CollectionView`, qui n'expose PAS la visibilité de
    ///     scrollbar en MAUI 8 — l'appliquer reviendrait à fouiller le
    ///     `ControlTemplate` WinUI natif (instable d'une version à l'autre) ;
    ///   - <c>terminal_scroll_multiplier</c> : MAUI ne permet pas de configurer
    ///     le pas de défilement de la molette d'une `CollectionView` ;
    ///   - <c>terminal_thread_init_cmd</c> : annonce une commande au démarrage
    ///     d'un « thread terminal » — le concept de thread terminal n'existe
    ///     nulle part dans le code (recherche complète 01/10).
    /// </summary>
    internal static class TerminalSettings
    {
        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

        // ------------------------------------------------------------------
        // Accès typés
        // ------------------------------------------------------------------

        /// <summary>
        /// Taille de police du terminal, bornée aux bornes DÉCLARÉES au
        /// catalogue (8..30, défaut 15).
        /// </summary>
        internal static int FontSize(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_font_size");
            return Math.Clamp(s.GetInt("terminal_font_size", declared), 8, 30);
        }

        /// <summary>
        /// Famille de police du terminal (défaut déclaré : « Consolas »).
        /// Une valeur vide au stockage retombe sur le défaut déclaré plutôt que
        /// de produire un Label sans police (le XAML réclame une famille non
        /// vide pour rester sur la même métrique que la saisie).
        /// </summary>
        internal static string FontFamily(SettingsEngine s)
        {
            var raw = s.GetString("terminal_font_family", DeclaredString("terminal_font_family"));
            return string.IsNullOrWhiteSpace(raw) ? DeclaredString("terminal_font_family") : raw;
        }

        /// <summary>
        /// Hauteur du dock terminal, bornée aux bornes DÉCLARÉES (100..1200,
        /// défaut 320). Le XAML part de 220 ; le défaut déclaré (320) prime au
        /// premier ApplyTerminalSettings — c'est lui que la fenêtre Réglages
        /// affiche, donc c'est lui qui doit s'appliquer.
        /// </summary>
        internal static int DefaultHeight(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_default_height");
            return Math.Clamp(s.GetInt("terminal_default_height", declared), 100, 1200);
        }

        /// <summary>
        /// Lignes d'historique conservées (0 = illimité, convention déjà
        /// utilisée par <c>gp_commit_max_len</c> au catalogue). bornes 0..100000.
        /// </summary>
        internal static int MaxScrollLines(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_max_scroll_lines");
            return Math.Clamp(s.GetInt("terminal_max_scroll_lines", declared), 0, 100000);
        }

        /// <summary>Sonnerie sur le caractère BEL (défaut déclaré : Off).</summary>
        internal static bool AudibleBell(SettingsEngine s)
            => s.GetBool("terminal_audible_bell", DeclaredBool("terminal_audible_bell"));
    }
}
