using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Moto.Core.Services;

namespace Moto.Editor.Views;

/// <summary>
/// Item 90 — Vue Git intégrée. Consomme GitService (item 83).
/// MOTO Editor affiche/lance les commandes ; les opérations Git sont déléguées au git CLI.
/// </summary>
public partial class GitPanelView : ContentView
{
    private readonly GitService _git;

    public ObservableCollection<string> Staged { get; } = new();
    public ObservableCollection<string> Unstaged { get; } = new();
    public ObservableCollection<string> Untracked { get; } = new();

    public GitPanelView(GitService git)
    {
        InitializeComponent();
        _git = git;

        StagedList.ItemsSource = Staged;
        UnstagedList.ItemsSource = Unstaged;
        UntrackedList.ItemsSource = Untracked;

        _ = RefreshAsync();
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        try
        {
            var status = await _git.GetStatusAsync();
            BranchLabel.Text = $"branche : {status.CurrentBranch}";

            Staged.Clear();
            foreach (var f in status.StagedFiles) Staged.Add(f);
            Unstaged.Clear();
            foreach (var f in status.UnstagedFiles) Unstaged.Add(f);
            Untracked.Clear();
            foreach (var f in status.UntrackedFiles) Untracked.Add(f);

            StatusLabel.Text = status.IsClean ? "✔ Clean" : $"{Staged.Count}↑ {Unstaged.Count}✎ {Untracked.Count}?";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"⚠ {ex.Message}";
        }
    }

    private async void OnStageClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string file)
        {
            await _git.StageAsync(file);
            await RefreshAsync();
        }
    }

    private async void OnUnstageClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string file)
        {
            await _git.UnstageAsync(file);
            await RefreshAsync();
        }
    }

    private async void OnCommitClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommitMessageEntry.Text)) return;
        var result = await _git.CommitAsync(CommitMessageEntry.Text);
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
}
