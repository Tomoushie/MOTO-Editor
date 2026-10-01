// Moto.Editor/Settings/DockPanelSettings.cs
using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit et applique les réglages de GÉOMÉTRIE / DOCK des familles
    /// « Panneaux » restantes — <c>ap_*</c> (Agent Panel), <c>cp_*</c> (Collaboration Panel)
    /// et <c>dp_*</c> (Debugger Panel).
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées au catalogue (SettingsCatalog.cs,
    /// catégorie « Panneaux ») et affichées dans la fenêtre Réglages, mais lues par AUCUN code
    /// compilé — constat mesuré par scripts/settings-coverage.ps1. Même chantier, même méthode
    /// que TabBarSettings (28/09, <c>tabs_*</c>) et PanelSettings (01/10, <c>pp_*</c>) :
    /// un fichier de mappage unique, des lectures à clé LITTÉRALE, le défaut DÉCLARÉ toujours
    /// passé explicitement.
    ///
    /// ⚠️ PIÈGE (28/09, re-payé ici) : <c>SettingsEngine.GetBool("clé")</c> SANS second argument
    /// renvoie <c>false</c> quand la clé est absente du store — le moteur ne consulte JAMAIS le
    /// catalogue. Toutes les lectures ci-dessous passent donc explicitement le défaut DÉCLARÉ.
    ///
    /// À QUOI CORRESPOND RÉELLEMENT <c>ap_*</c> — vérifié dans MainPage.Panels.cs / .Routing.cs :
    /// il n'existe AUCUN <c>AgentPanelView</c> dans le dépôt. Le libellé du catalogue
    /// (« Bouton panneau IA dans la barre de statut », « Dock agent », « Position du panneau IA »)
    /// désigne le panneau de chat IA réel : <see cref="Moto.Editor.Views.AiChatView"/>, enregistré
    /// sur le système <c>AddFloatingPanel</c> (titre « MOTO AI », KindFor → "aichat"). C'est donc
    /// LUI que la géométrie <c>ap_*</c> pilote.
    ///
    /// PÉRIMÈTRE — un réglage n'est câblé que si la géométrie qu'il promet a un point
    /// d'application RÉEL (l'élément existe déjà, seul son réglage était ignoré) :
    ///   - <c>ap_dock</c> → colonne du dock qui héberge le chat, via le mécanisme EXISTANT
    ///     <c>ApplySidePanelLayout</c> (_panelsSwapped) — aucun 2e système de dock créé ;
    ///   - <c>ap_width</c> / <c>ap_height</c> → AiDockPanel et AiChatView ;
    ///   - <c>cp_dock</c> / <c>cp_width</c> → CollabPanelView (overlay flottant réel) ;
    ///   - <c>dp_dock</c> → voir la note « dp_dock » ci-dessous : il n'a PAS de point
    ///     d'application réel aujourd'hui, donc laissé inerte (décision, pas oubli).
    ///
    /// RESTENT INERTES (raisons exactes, voir aussi le rapport de fin de chantier) :
    ///   - <c>ap_button</c>, <c>cp_button</c> : ils prétendent configurer « un bouton dans la
    ///     barre de statut », or StatusBarPanelView.xaml n'en contient AUCUN (ses seuls éléments
    ///     nommés sont StatusLabel/RightChips/ErrorsLabel/WarningsLabel/StateChips/SandboxLabel/
    ///     LockedLabel/AiStatusLabel). Créer ces boutons serait un AJOUT DE FONCTIONNALITÉ, pas
    ///     un câblage — laissé inerte, comme constaté le 01/10.
    ///   - <c>ap_limit_width</c> / <c>ap_max_width</c> : « Contenu centré à largeur max »
    ///     suppose une colonne de contenu CENTRÉE. Le chat occupe toute la largeur de sa colonne
    ///     (AiChatView n'a pas de conteneur centré) : la seule largeur maximale qui existe est
    ///     celle des BULLES (420 px, figée dans le DataTemplate d'AiChatView.xaml). Borner les
    ///     bulles à une valeur réglable ne borne PAS « le contenu » comme l'annonce le libellé,
    ///     et <c>ap_limit_width</c> décoché n'aurait alors aucun sens (il faudrait une largeur
    ///     illimitée, qui n'existe pas ici). Laissé inerte plutôt que de détourner le libellé.
    ///   - <c>ap_flexible</c> : « Largeur flexible quand docké latéralement ». La poignée
    ///     d'étirement du dock (OnAiDockResizePanUpdated) est TOUJOURS active et bornée à
    ///     280..700 px ; la rendre réellement « non flexible » signifierait DÉSACTIVER un
    ///     redimensionnement qui fonctionne — un retrait de fonctionnalité, interdit par la
    ///     règle « changements additifs uniquement ». Laissé inerte.
    ///   - <c>dp_dock</c> : « Position du panneau débogueur ». VÉRIFIÉ le 01/10 : le panneau
    ///     Debug du système AddFloatingPanel (<c>_debugPanel</c>, MainPage.xaml.cs) n'a AUCUN
    ///     chemin qui le rende visible — rien ne met jamais <c>_debugPanel.IsVisible = true</c>
    ///     (ses seules occurrences hors construction sont TitleFor/KindFor et le foreach de
    ///     AddFloatingPanel, qui le masque). Le seul écran Debug RÉELLEMENT atteignable est une
    ///     FENÊTRE SÉPARÉE (« debug », MainPage.Extensions.cs) qui ouvre DebugPanelProView, hors
    ///     du RootGrid : un dock Bottom/Right/Left n'y a aucun sens. Câbler dp_dock reviendrait
    ///     donc à déplacer un panneau que personne ne peut ouvrir — un réglage qui « marche »
    ///     sans effet observable, exactement ce que la règle du dépôt interdit. Laissé inerte.
    /// </summary>
    internal static class DockPanelSettings
    {
        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte/énuméré (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

        /// <summary>
        /// Bornes DÉCLARÉES au catalogue (Min/Max) — les mêmes que celles que la fenêtre
        /// Réglages impose à la saisie. Les reprendre ici évite qu'une valeur écrite à la main
        /// dans le fichier de réglages (ou héritée d'une ancienne version du catalogue) fasse
        /// exploser la mise en page : c'est exactement le risque documenté du « grand
        /// HeightRequest dans une ligne * ».
        /// </summary>
        private static int DeclaredMin(string id) => SettingsCatalog.ById(id)?.Min ?? 0;

        private static int DeclaredMax(string id) => SettingsCatalog.ById(id)?.Max ?? int.MaxValue;

        // ------------------------------------------------------------------
        // ap_* — Agent Panel (le panneau de chat IA réel : AiChatView)
        // ------------------------------------------------------------------

        /// <summary>Côté du dock qui héberge le panneau IA : "Left" (défaut) ou "Right".</summary>
        internal static bool DockLeft(SettingsEngine s)
            => !string.Equals(
                s.GetString("ap_dock", DeclaredString("ap_dock")),
                "Right",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Largeur du dock IA, bornée aux bornes DÉCLARÉES (200..1200, pas 20).
        /// ⚠️ La poignée d'étirement borne le GESTE à 280..700 px ; le réglage, lui, va
        /// jusqu'aux bornes du catalogue : c'est une largeur de départ choisie, pas un geste.
        /// </summary>
        internal static double Width(SettingsEngine s)
        {
            var value = s.GetInt("ap_width", DeclaredInt("ap_width"));
            return Math.Clamp(value, DeclaredMin("ap_width"), DeclaredMax("ap_width"));
        }

        /// <summary>
        /// Hauteur visée pour le panneau de chat, bornée aux bornes DÉCLARÉES (100..1200).
        /// Elle est ensuite PLAFONNÉE par la hauteur réellement disponible (même mécanisme que
        /// le chat qui prend la hauteur visible de sa colonne, voir AiChatView.FitToViewport) —
        /// un réglage ne doit jamais faire déborder le dock sous la barre de statut.
        /// </summary>
        internal static double Height(SettingsEngine s)
        {
            var value = s.GetInt("ap_height", DeclaredInt("ap_height"));
            return Math.Clamp(value, DeclaredMin("ap_height"), DeclaredMax("ap_height"));
        }

        // ------------------------------------------------------------------
        // cp_* — Collaboration Panel (CollabPanelView, overlay flottant réel)
        // ------------------------------------------------------------------

        /// <summary>Côté du panneau collaboration : "Right" (défaut) ou "Left".</summary>
        internal static bool CollabDockLeft(SettingsEngine s)
            => string.Equals(
                s.GetString("cp_dock", DeclaredString("cp_dock")),
                "Left",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Largeur du panneau collaboration, bornée aux bornes DÉCLARÉES (150..800).</summary>
        internal static double CollabWidth(SettingsEngine s)
        {
            var value = s.GetInt("cp_width", DeclaredInt("cp_width"));
            return Math.Clamp(value, DeclaredMin("cp_width"), DeclaredMax("cp_width"));
        }

        // ------------------------------------------------------------------
        // dp_* — Debugger Panel : AUCUN câblage (voir la note de classe).
        // Aucun accesseur n'est exposé ici : un accesseur non appelé serait du code
        // mort, et le script de couverture compte une clé comme « opérante » dès
        // qu'une lecture à clé littérale existe — l'exposer ferait MENTIR la mesure
        // sans qu'aucun pixel ne change.
        // ------------------------------------------------------------------
    }
}
