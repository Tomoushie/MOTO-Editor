using System;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Services;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public sealed class PullRequestInfo
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = ""; // open / closed
}

/// <summary>
/// Idée "Lightweight PR integration" (P1) — open/close/comment depuis l'éditeur.
/// Respecte la règle d'architecture : MOTO Editor "lance des commandes" ; les vraies
/// opérations git/PR sont déléguées au TerminalService existant (pas de système inventé).
/// </summary>
public sealed class LightweightPrService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly TerminalService _terminal;

    public LightweightPrService(SettingsEngine settings, StructuredLogCollector log, TerminalService terminal)
    {
        _settings = settings;
        _log = log;
        _terminal = terminal;
    }

    public async Task OpenPrAsync(string title, string branch)
    {
        if (!_settings.Shared.Collab.LightweightPrEnabled.Value) return;
        await _terminal.ExecuteAsync($"gh pr create --title \"{title}\" --head {branch}");
        _log.Info("LightweightPr", "PR ouverte", new { title, branch });
    }

    public async Task ClosePrAsync(int number)
    {
        if (!_settings.Shared.Collab.LightweightPrEnabled.Value) return;
        await _terminal.ExecuteAsync($"gh pr close {number}");
        _log.Info("LightweightPr", "PR fermée", new { number });
    }

    public async Task CommentPrAsync(int number, string comment)
    {
        if (!_settings.Shared.Collab.LightweightPrEnabled.Value) return;
        await _terminal.ExecuteAsync($"gh pr comment {number} --body \"{comment}\"");
        _log.Info("LightweightPr", "PR commentée", new { number });
    }
}
