using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.Services;
using Moto.Editor.Models;
using Moto.Editor.Settings;

namespace Moto.Editor.Views;

/// <summary>
/// Item 90 — Vue Git intégrée. Consomme GitService (item 83).
/// MOTO Editor affiche/lance les commandes ; les opérations Git sont déléguées au git CLI.
///
/// ★ MODIFIÉ (01/10, chantier « Panneaux / Git Panel », clés <c>gp_*</c>) : la vue
/// n'ignorait plus seulement ses réglages, elle n'avait AUCUN moyen de les recevoir —
/// elle était construite par la fenêtre spécialisée « Git » sans jamais être rattachée à
/// <c>MainPage.ApplyLayoutSettings</c>. Elle applique maintenant les 12 clés d'affichage et de
/// comportement de sa famille (voir <see cref="Settings.GitPanelSettings"/>), toutes branchées
/// sur des données RÉELLES de <see cref="GitService"/> : aucun compteur, aucun statut et aucune
/// statistique de diff n'est inventé ici.
///
/// ★ MODIFIÉ (01/10, chantier <c>git_*</c>) : trois clés de la famille « Version Control »
/// arrivent aussi dans cette vue (voir <see cref="Settings.GitSettings"/>) —
/// <c>git_path_style</c> et <c>git_stage_restore_buttons</c> au niveau des lignes
/// (<c>GitChangeNode</c>), <c>git_diff_base</c> au niveau du « Diff du projet ».
///
/// La géométrie (largeur, dock) et l'ouverture au démarrage ne sont PAS traitées dans cette vue :
/// elles concernent la FENÊTRE Git, qui n'appartient pas au panneau — c'est
/// <c>MainPage.ApplyPanelGeometrySettings</c> qui les applique (même répartition que
/// <c>pp_*</c> entre <c>FileExplorerView</c> et <c>MainPage</c>).
/// </summary>
public partial class GitPanelView : ContentView
{
    private readonly GitService _git;

    /// <summary>★ AJOUT (01/10) : dernières valeurs appliquées, pour ne pas relire le store à chaque rafraîchissement.</summary>
    private bool _labeledStatus;
    private bool _groupByFolder;
    private bool _showDiffStats;
    private bool _openFileOnClick;
    private int _commitMaxLength;

    /// <summary>★ AJOUT (01/10, git_path_style) : dernier style de chemin appliqué (true = « Path First »).</summary>
    private bool _pathNameFirst;

    /// <summary>★ AJOUT (01/10, git_stage_restore_buttons) : dernière visibilité des boutons stage/restore.</summary>
    private bool _stageButtons = true;

    /// <summary>
    /// ★ MODIFIÉ (01/10) : propriétés passées en <c>internal</c> — le type de ligne
    /// <see cref="GitChangeNode"/> l'est aussi, et une propriété publique ne peut pas exposer un
    /// type moins accessible (CS0053). Rien d'externe ne consomme ces collections : seuls les
    /// trois <c>CollectionView</c> du panneau s'y lient, dans le même assembly.
    /// </summary>
    internal ObservableCollection<GitChangeNode> Staged { get; } = new();
    internal ObservableCollection<GitChangeNode> Unstaged { get; } = new();
    internal ObservableCollection<GitChangeNode> Untracked { get; } = new();

    /// <summary>
    /// ★ AJOUT (01/10) : un fichier du panneau a été cliqué. Levé pour que MainPage ouvre le
    /// fichier dans l'éditeur (réglage <c>gp_click_behavior</c> = « File Diff »). Une
    /// <c>ContentView</c> n'a pas de Navigation propre — même patron que
    /// <c>DocPanelView.OpenFileRequested</c> / <c>SettingsWindow.ApiKeysRequested</c>.
    /// </summary>
    public event Action<string>? FileOpenRequested;

    public GitPanelView(GitService git)
    {
        InitializeComponent();
        _git = git;

        StagedList.ItemsSource = Staged;
        UnstagedList.ItemsSource = Unstaged;
        UntrackedList.ItemsSource = Untracked;

        _ = RefreshAsync();
    }

    /// <summary>
    /// ★ AJOUT (01/10) : applique les réglages de la famille « Panneaux / Git Panel » (clés
    /// <c>gp_*</c>) plus l'arrivement des clés <c>git_path_style</c> /
    /// <c>git_stage_restore_buttons</c> (famille « Version Control », voir
    /// <see cref="Settings.GitSettings"/>). Appelée par MainPage — au démarrage, à chaque
    /// changement de réglage et au retour de plein écran, exactement comme
    /// <c>EditorPane.ApplySettings</c> (tabs_*) et <c>ExplorerPanel.ApplySettings</c> (pp_*).
    /// </summary>
    public void ApplySettings(Moto.Core.Settings.SettingsEngine settings)
    {
        if (settings is null) return;

        // Style de statut (gp_status_style) et groupement (gp_group) : deux choix d'AFFICHAGE,
        // donc relus ici et répercutés sur les lignes déjà chargées (pas de nouvel appel git).
        _labeledStatus = !GitPanelSettings.StatusUsesIcons(settings);
        _groupByFolder = GitPanelSettings.GroupByFolder(settings);

        // ★ AJOUT (01/10, git_path_style) : libellé affiché pour chaque fichier — nom d'abord
        // (défaut déclaré) ou chemin complet. Choix d'AFFICHAGE pur : la clé Path (envoyée à
        // git) n'est jamais réécrite.
        _pathNameFirst = GitSettings.PathIsPathFirst(settings);

        // ★ AJOUT (01/10, git_stage_restore_buttons) : visibilité des boutons stage/restore
        // (défaut déclaré : visible) — largeur de colonne posée sur chaque ligne, sans nouvel
        // appel git.
        _stageButtons = GitSettings.StageRestoreButtons(settings);

        // Statistiques de diff (gp_diff_stats, défaut déclaré activé) : leur chargement demande
        // un appel git supplémentaire — on ne le fait que si le réglage le demande vraiment.
        var wantStats = GitPanelSettings.DiffStats(settings);
        var statsChanged = wantStats != _showDiffStats;
        _showDiffStats = wantStats;

        // Comportement au clic (gp_click_behavior).
        _openFileOnClick = GitPanelSettings.ClickOpensFileDiff(settings);

        // Longueur max du titre de commit (gp_commit_max_len, 0 = illimité).
        _commitMaxLength = GitPanelSettings.CommitMaxLength(settings);

        // Repli de la section « non suivis » (gp_collapse_untracked) : le groupe entier
        // (titre + liste), pas seulement la liste.
        UntrackedGroup.IsVisible = !GitPanelSettings.CollapseUntracked(settings);

        // Barre de défilement (gp_scrollbar). Le catalogue annonce « Affichage de la
        // scrollbar » : appliqué au défilement du panneau, le seul qui existe.
        ChangesScroll.VerticalScrollBarVisibility = GitPanelSettings.ResolveScrollbar(settings);

        ApplyRowDisplay();

        // Les statistiques viennent de git : les (re)charger seulement quand le réglage bascule,
        // ou quand la vue n'en a encore aucune alors qu'on les veut.
        if (statsChanged && _showDiffStats && Staged.Count + Unstaged.Count + Untracked.Count > 0)
            _ = LoadDiffStatsAsync();
        else if (!_showDiffStats)
            ClearDiffStats();
    }

    /// <summary>
    /// ★ AJOUT (01/10) : reporte les réglages d'affichage sur chaque ligne déjà en mémoire.
    /// Les statistiques de diff ne sont PAS touchées ici : elles viennent de git et sont
    /// conservées tant que le réglage qui les gouverne n'a pas changé.
    /// </summary>
    private void ApplyRowDisplay()
    {
        foreach (var node in AllNodes())
            node.ApplyDisplay(_labeledStatus, _groupByFolder, _pathNameFirst, _stageButtons);
    }

    private IEnumerable<GitChangeNode> AllNodes()
        => Staged.Concat(Unstaged).Concat(Untracked);

    private void ClearDiffStats()
    {
        foreach (var node in AllNodes())
            node.SetDiffStats(0, 0);
    }

    private async Task RefreshAsync()
    {
        try
        {
            var status = await _git.GetStatusAsync();
            // ★ AJOUT (01/10, gp_fallback_branch) : la branche est affichée TELLE QUE git l'a
            // renvoyée. Quand le dossier n'est pas un dépôt (ou que git échoue), on l'annonce au
            // lieu d'afficher la « branche par défaut » du catalogue — recopier « main » ici
            // ferait croire à une branche qui n'existe pas.
            BranchLabel.Text = string.IsNullOrWhiteSpace(status.CurrentBranch)
                ? "branche : aucune branche détectée"
                : $"branche : {status.CurrentBranch}";

            Replace(Staged, status.StagedFiles, GitChangeKind.Staged);
            Replace(Unstaged, status.UnstagedFiles, GitChangeKind.Unstaged);
            Replace(Untracked, status.UntrackedFiles, GitChangeKind.Untracked);

            // ★ MODIFIÉ (01/10, gp_sort) : ordre réel des sections. « Path » (défaut) ne déplace
            // rien : l'ordre Indexé → Modifié → Non suivi est déjà celui du cheminement d'un
            // fichier dans git, c'est l'ordre de référence. « Name » trie par nom, « Status » par
            // lettre d'état — deux ordres que git ne renvoie pas de lui-même.
            var sort = Moto.Core.Settings.SettingsEngine.Shared is { } s
                ? GitPanelSettings.ResolveSort(s)
                : GitSortMode.Path;
            SortSection(Staged, sort);
            SortSection(Unstaged, sort);

            ApplyRowDisplay();

            StatusLabel.Text = status.IsClean
                ? "✔ Clean"
                : $"{Staged.Count}↑ {Unstaged.Count}✎ {Untracked.Count}?";

            if (_showDiffStats)
                _ = LoadDiffStatsAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"⚠ {ex.Message}";
        }
    }

    /// <summary>
    /// Remplit une section en construisant les lignes depuis les chemins RÉELS renvoyés par git
    /// (remplacement en place : les CollectionView écoutent la même instance observable).
    /// </summary>
    private static void Replace(ObservableCollection<GitChangeNode> target, IReadOnlyList<string> files, GitChangeKind kind)
    {
        target.Clear();
        foreach (var file in files)
            target.Add(GitChangeNode.For(file, kind));
    }

    private static void SortSection(ObservableCollection<GitChangeNode> section, GitSortMode mode)
    {
        if (mode == GitSortMode.Path || section.Count < 2) return;

        var sorted = mode == GitSortMode.Name
            ? section.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : section.OrderBy(n => n.StatusLetter, StringComparer.Ordinal).ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = section.IndexOf(sorted[i]);
            if (current != i) section.Move(current, i);
        }
    }

    /// <summary>
    /// ★ AJOUT (01/10, gp_diff_stats) : ajouts/suppressions RÉELS par fichier, lus depuis
    /// <c>GitService.GetDiffStatsAsync()</c> (git CLI, une seule commande pour tout le dépôt).
    /// Décoché = rien n'est demandé à git et la colonne se replie (voir
    /// <c>GitChangeNode.StatsColumnWidth</c>). Un fichier absent de la réponse git ne reçoit
    /// AUCUNE statistique : on n'affiche ni « +0 −0 » ni un chiffre inventé (un fichier non suivi
    /// n'est dans aucun diff, un binaire n'a pas de comptage de lignes).
    /// </summary>
    private async Task LoadDiffStatsAsync()
    {
        try
        {
            var stats = await _git.GetDiffStatsAsync();
            foreach (var node in AllNodes())
            {
                if (stats.TryGetValue(node.Path, out var value) && !value.IsBinary)
                    node.SetDiffStats(value.Additions, value.Removals);
                else
                    node.SetDiffStats(0, 0);
            }
        }
        catch
        {
            // Statistiques non disponibles (dépôt absent, git injoignable) : on efface plutôt que
            // d'afficher un chiffre inventé. Le panneau reste utilisable, c'est un bonus.
            ClearDiffStats();
        }
    }

    /// <summary>
    /// ★ AJOUT (01/10, gp_click_behavior) : clic sur un fichier. La sélection est remise à zéro
    /// aussitôt — les CollectionView ne peuvent PAS rester sélectionnées sur une ligne qui va
    /// disparaître au rafraîchissement suivant, et l'état `Selected` ne s'applique de toute façon
    /// pas sous Windows (piège déjà documenté dans ce dépôt).
    /// </summary>
    private async void OnChangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView list) return;
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not GitChangeNode node)
        {
            list.SelectedItem = null;
            return;
        }
        list.SelectedItem = null;

        if (_openFileOnClick)
        {
            // « File Diff » : le fichier cliqué est ouvert dans l'éditeur (MOTO sait déjà y
            // afficher le contenu d'un fichier) ; MainPage possède le chemin vers l'éditeur.
            FileOpenRequested?.Invoke(ResolveAbsolute(node.Path));
            return;
        }

        await ShowProjectDiffAsync(node.Path);
    }

    /// <summary>
    /// Résout le chemin ABSOLU d'un chemin relatif renvoyé par git, en s'appuyant sur le dossier
    /// de travail RÉEL du service (jamais sur un dossier deviné). Renvoie la chaîne d'origine si
    /// elle est déjà absolue ou si le dossier de travail est inconnu.
    /// </summary>
    private string ResolveAbsolute(string gitPath)
    {
        var root = _git.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(gitPath)) return gitPath;
        if (System.IO.Path.IsPathRooted(gitPath)) return gitPath;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(root, gitPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// ★ AJOUT (01/10, gp_click_behavior = « Project Diff », défaut) : déplie le diff RÉEL du
    /// projet — <c>GitService.GetDiffAsync()</c> renvoie, fichier par fichier, le contenu
    /// avant/après tel que git le calcule. Un fichier sans modification suivie par git (cas d'un
    /// fichier NON SUIVI : il n'est dans aucun diff) le dit explicitement au lieu d'afficher un
    /// faux « aucune différence ».
    ///
    /// ★ AJOUT (01/10, git_diff_base) : la BASE du diff est choisie par le réglage « Base du
    /// diff » — « Head » (défaut déclaré) = <c>git diff</c> sans argument (comportement
    /// inchangé), « Default Branch » = diff contre la branche par défaut du remote
    /// (<c>GitService.GetDefaultBranchAsync()</c>), la base réellement utilisée étant annoncée
    /// dans la ligne de statut. Branche par défaut introuvable = repli sur Head + message
    /// explicite — jamais une branche devinée.
    /// </summary>
    private async Task ShowProjectDiffAsync(string path)
    {
        try
        {
            string? baseRef = null;
            var baseNote = string.Empty;
            var settings = Moto.Core.Settings.SettingsEngine.Shared;
            if (settings is not null && GitSettings.DiffBaseIsDefaultBranch(settings))
            {
                baseRef = await _git.GetDefaultBranchAsync();
                baseNote = baseRef is null
                    ? " — branche par défaut introuvable, base : HEAD"
                    : $" — base : {baseRef}";
            }

            var diffs = await _git.GetDiffAsync(baseRef);
            var rows = diffs
                .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
                .Select(d => new GitDiffRow(d.FilePath, Summarize(d)))
                .ToList();

            DiffList.ItemsSource = rows;
            ProjectDiffGroup.IsVisible = rows.Count > 0;

            var mine = diffs.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            StatusLabel.Text = mine is null
                ? $"{rows.Count} fichier(s) modifié(s) — {path} n'apparaît pas dans le diff (non suivi ?){baseNote}"
                : $"{rows.Count} fichier(s) modifié(s) — {path} : {Summarize(mine)}{baseNote}";
        }
        catch (Exception ex)
        {
            ProjectDiffGroup.IsVisible = false;
            StatusLabel.Text = $"⚠ {ex.Message}";
        }
    }

    /// <summary>
    /// Compte RÉELLEMENT les lignes du diff renvoyé par git (celles qui commencent par « + » ou
    /// « − », en excluant les en-têtes « +++ »/« −−− »). Aucune estimation : si git ne renvoie
    /// rien pour ce fichier, le résumé le dit.
    /// </summary>
    private static string Summarize(GitDiff diff)
    {
        var added = 0;
        var removed = 0;
        foreach (var line in (diff.NewContent ?? string.Empty).Split('\n'))
            if (!string.IsNullOrEmpty(line)) added++;
        foreach (var line in (diff.OldContent ?? string.Empty).Split('\n'))
            if (!string.IsNullOrEmpty(line)) removed++;

        return added == 0 && removed == 0
            ? "aucune ligne modifiée renvoyée par git"
            : $"+{added} −{removed}";
    }

    private async void OnStageClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is GitChangeNode node)
        {
            await _git.StageAsync(node.Path);
            await RefreshAsync();
        }
    }

    private async void OnUnstageClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is GitChangeNode node)
        {
            await _git.UnstageAsync(node.Path);
            await RefreshAsync();
        }
    }

    private async void OnCommitClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommitMessageEntry.Text)) return;

        // ★ AJOUT (01/10, gp_commit_max_len) : la longueur maximale annoncée par le catalogue
        // (« Longueur max du titre de commit », 0 = illimité) est appliquée au message RÉELLEMENT
        // envoyé à git commit, pas seulement à l'affichage : un titre tronqué seulement à l'écran
        // aurait laissé partir dans l'historique un titre plus long que ce que le réglage annonce.
        var message = GitPanelSettings.TruncateCommitTitle(CommitMessageEntry.Text.Trim(), _commitMaxLength);
        var result = await _git.CommitAsync(message);
        StatusLabel.Text = result == GitOperationResult.Success ? "✔ Commit OK" : "⚠ Commit échoué";
        CommitMessageEntry.Text = "";
        await RefreshAsync();
    }

    private async void OnPushClicked(object? sender, EventArgs e)
    {
        var result = await _git.PushAsync();
        StatusLabel.Text = result == GitOperationResult.Success ? "✔ Push OK" : "⚠ Push échoué";
    }

    private async void OnPullClicked(object? sender, EventArgs e)
    {
        var result = await _git.PullAsync();
        StatusLabel.Text = result == GitOperationResult.Success ? "✔ Pull OK" : "⚠ Pull échoué";
        await RefreshAsync();
    }

    private async void OnFetchClicked(object? sender, EventArgs e)
    {
        await _git.FetchAsync();
        StatusLabel.Text = "✔ Fetch OK";
    }

    private async void OnBranchesClicked(object? sender, EventArgs e)
    {
        var branches = await _git.ListBranchesAsync();
        StatusLabel.Text = branches.Count > 0 ? $"🌿 {string.Join(", ", branches)}" : "Aucune branche";
    }

    private async void OnLogClicked(object? sender, EventArgs e)
    {
        var log = await _git.GetLogAsync(5);
        StatusLabel.Text = log.Count > 0 ? $"📜 {log[0].Message}" : "Aucun commit";
    }

    /// <summary>Ligne du diff de projet (modèle d'affichage, jamais une donnée git brute).</summary>
    internal sealed record GitDiffRow(string FilePath, string Summary);
}
