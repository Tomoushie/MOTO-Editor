using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Services;

public enum GitOperationResult { Success, Failure, Cancelled }

public sealed class GitStatus
{
    public string CurrentBranch { get; set; } = "";
    public IReadOnlyList<string> StagedFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UnstagedFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UntrackedFiles { get; set; } = Array.Empty<string>();
    public bool IsClean => !StagedFiles.Any() && !UnstagedFiles.Any() && !UntrackedFiles.Any();
}

public sealed class GitDiff
{
    public string FilePath { get; set; } = "";
    public string OldContent { get; set; } = "";
    public string NewContent { get; set; } = "";
}

/// <summary>
/// Item 83 — Intégration Git complète via TerminalService.
/// Pas de lib tierce : utilise git CLI. Respecte "MOTO n'invente pas de systèmes".
/// </summary>
public sealed class GitService
{
    private readonly TerminalService _terminal;
    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;

    public GitService(TerminalService terminal, StructuredLogCollector log, SettingsEngine settings)
    {
        _terminal = terminal;
        _log = log;
        _settings = settings;
    }

    /// <summary>Initialise un nouveau dépôt Git.</summary>
    public async Task<GitOperationResult> InitAsync(string path)
    {
        if (!_settings.Shared.Git.GitEnabled.Value) return GitOperationResult.Cancelled;
        var result = await _terminal.ExecuteAsync($"git init \"{path}\"");
        _log.Info("Git", "Init", new { path, result });
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Ajoute un remote.</summary>
    public async Task<GitOperationResult> AddRemoteAsync(string name, string url)
    {
        var result = await _terminal.ExecuteAsync($"git remote add {name} \"{url}\"");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Stage un fichier (ou partie).</summary>
    public async Task<GitOperationResult> StageAsync(string filePath, int? startLine = null, int? endLine = null)
    {
        string cmd = (startLine.HasValue && endLine.HasValue)
            ? $"git add -p \"{filePath}\"" // patch mode interactif
            : $"git add \"{filePath}\"";
        var result = await _terminal.ExecuteAsync(cmd);
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Unstage un fichier.</summary>
    public async Task<GitOperationResult> UnstageAsync(string filePath)
    {
        var result = await _terminal.ExecuteAsync($"git restore --staged \"{filePath}\"");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Untrack un fichier (remove from index).</summary>
    public async Task<GitOperationResult> UntrackAsync(string filePath)
    {
        var result = await _terminal.ExecuteAsync($"git rm --cached \"{filePath}\"");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Commit avec message.</summary>
    public async Task<GitOperationResult> CommitAsync(string message, bool amend = false)
    {
        string cmd = amend ? $"git commit --amend -m \"{message}\"" : $"git commit -m \"{message}\"";
        var result = await _terminal.ExecuteAsync(cmd);
        _log.Info("Git", "Commit", new { message, amend });
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Push vers remote.</summary>
    public async Task<GitOperationResult> PushAsync(string remote = "origin", string branch = "", bool force = false)
    {
        if (!_settings.Shared.Git.ConfirmBeforePush.Value && !force)
        {
            _log.Warning("Git", "Push annulé : confirmation requise");
            return GitOperationResult.Cancelled;
        }
        string branchArg = string.IsNullOrEmpty(branch) ? "" : $" {branch}";
        string forceArg = force ? " --force" : "";
        var result = await _terminal.ExecuteAsync($"git push {remote}{branchArg}{forceArg}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Pull depuis remote.</summary>
    public async Task<GitOperationResult> PullAsync(string remote = "origin", string branch = "")
    {
        string branchArg = string.IsNullOrEmpty(branch) ? "" : $" {branch}";
        var result = await _terminal.ExecuteAsync($"git pull {remote}{branchArg}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Fetch depuis remote.</summary>
    public async Task<GitOperationResult> FetchAsync(string remote = "origin")
    {
        var result = await _terminal.ExecuteAsync($"git fetch {remote}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Merge une branche.</summary>
    public async Task<GitOperationResult> MergeAsync(string branch, bool noCommit = false)
    {
        string noCommitArg = noCommit ? " --no-commit" : "";
        var result = await _terminal.ExecuteAsync($"git merge {branch}{noCommitArg}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Rebase sur une branche.</summary>
    public async Task<GitOperationResult> RebaseAsync(string branch)
    {
        var result = await _terminal.ExecuteAsync($"git rebase {branch}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Change de branche (checkout).</summary>
    public async Task<GitOperationResult> CheckoutAsync(string branch, bool create = false)
    {
        string createArg = create ? " -b" : "";
        var result = await _terminal.ExecuteAsync($"git checkout{createArg} {branch}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Crée une nouvelle branche.</summary>
    public async Task<GitOperationResult> CreateBranchAsync(string name)
    {
        var result = await _terminal.ExecuteAsync($"git branch {name}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Supprime une branche.</summary>
    public async Task<GitOperationResult> DeleteBranchAsync(string name, bool force = false)
    {
        string forceArg = force ? " -D" : " -d";
        var result = await _terminal.ExecuteAsync($"git branch{forceArg} {name}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Liste les branches.</summary>
    public async Task<IReadOnlyList<string>> ListBranchesAsync(bool includeRemote = false)
    {
        string remoteArg = includeRemote ? " -a" : "";
        var result = await _terminal.ExecuteAsync($"git branch{remoteArg}");
        if (result.ExitCode != 0) return Array.Empty<string>();
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(b => b.Trim().TrimStart('*'))
                          .Where(b => !string.IsNullOrWhiteSpace(b))
                          .ToList();
    }

    /// <summary>Statut du dépôt.</summary>
    public async Task<GitStatus> GetStatusAsync()
    {
        var result = await _terminal.ExecuteAsync("git status --porcelain");
        if (result.ExitCode != 0) return new GitStatus();

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var staged = new List<string>();
        var unstaged = new List<string>();
        var untracked = new List<string>();

        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            char indexStatus = line[0];
            char workTreeStatus = line[1];
            string file = line.Substring(3).Trim();

            if (indexStatus == '?') untracked.Add(file);
            else if (indexStatus != ' ') staged.Add(file);
            if (workTreeStatus != ' ' && indexStatus != '?') unstaged.Add(file);
        }

        var branchResult = await _terminal.ExecuteAsync("git branch --show-current");
        string branch = branchResult.ExitCode == 0 ? branchResult.Output.Trim() : "unknown";

        return new GitStatus
        {
            CurrentBranch = branch,
            StagedFiles = staged,
            UnstagedFiles = unstaged,
            UntrackedFiles = untracked
        };
    }

    /// <summary>Diff entre deux commits ou staged/unstaged.</summary>
    public async Task<IReadOnlyList<GitDiff>> GetDiffAsync(string? commit1 = null, string? commit2 = null, bool staged = false)
    {
        string cmd = staged ? "git diff --cached" : (commit1 != null && commit2 != null ? $"git diff {commit1} {commit2}" : "git diff");
        var result = await _terminal.ExecuteAsync(cmd);
        if (result.ExitCode != 0) return Array.Empty<GitDiff>();

        // Parsing simplifié du diff (pour une vraie intégration, utiliser un parser diff)
        var diffs = new List<GitDiff>();
        var lines = result.Output.Split('\n');
        string currentFile = "";
        var oldLines = new List<string>();
        var newLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git"))
            {
                if (!string.IsNullOrEmpty(currentFile))
                    diffs.Add(new GitDiff { FilePath = currentFile, OldContent = string.Join("\n", oldLines), NewContent = string.Join("\n", newLines) });
                currentFile = line.Split(' ').LastOrDefault()?.TrimStart('b/') ?? "";
                oldLines.Clear();
                newLines.Clear();
            }
            else if (line.StartsWith("-") && !line.StartsWith("---")) oldLines.Add(line.Substring(1));
            else if (line.StartsWith("+") && !line.StartsWith("+++")) newLines.Add(line.Substring(1));
        }
        if (!string.IsNullOrEmpty(currentFile))
            diffs.Add(new GitDiff { FilePath = currentFile, OldContent = string.Join("\n", oldLines), NewContent = string.Join("\n", newLines) });

        return diffs;
    }

    /// <summary>Log des commits (archéologie).</summary>
    public async Task<IReadOnlyList<GitCommit>> GetLogAsync(int maxCount = 50)
    {
        var result = await _terminal.ExecuteAsync($"git log --oneline -n {maxCount}");
        if (result.ExitCode != 0) return Array.Empty<GitCommit>();

        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(line =>
                          {
                              var parts = line.Split(' ', 2);
                              return new GitCommit
                              {
                                  Hash = parts[0],
                                  Message = parts.Length > 1 ? parts[1] : ""
                              };
                          })
                          .ToList();
    }

    /// <summary>Restaure un fichier à un commit donné.</summary>
    public async Task<GitOperationResult> RestoreFileAsync(string filePath, string commit)
    {
        var result = await _terminal.ExecuteAsync($"git checkout {commit} -- \"{filePath}\"");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Discard les changements unstaged.</summary>
    public async Task<GitOperationResult> DiscardChangesAsync(string filePath)
    {
        var result = await _terminal.ExecuteAsync($"git restore \"{filePath}\"");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Stash les changements.</summary>
    public async Task<GitOperationResult> StashAsync(string message = "")
    {
        string msgArg = string.IsNullOrEmpty(message) ? "" : $" -m \"{message}\"";
        var result = await _terminal.ExecuteAsync($"git stash{msgArg}");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Pop le dernier stash.</summary>
    public async Task<GitOperationResult> StashPopAsync()
    {
        var result = await _terminal.ExecuteAsync("git stash pop");
        return result.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }

    /// <summary>Configure git user.</summary>
    public async Task<GitOperationResult> ConfigureAsync(string userName, string userEmail)
    {
        var r1 = await _terminal.ExecuteAsync($"git config --global user.name \"{userName}\"");
        var r2 = await _terminal.ExecuteAsync($"git config --global user.email \"{userEmail}\"");
        return r1.ExitCode == 0 && r2.ExitCode == 0 ? GitOperationResult.Success : GitOperationResult.Failure;
    }
}

public sealed class GitCommit
{
    public string Hash { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? Timestamp { get; set; }
}
