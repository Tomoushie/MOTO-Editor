using System.Text.RegularExpressions;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Mcp;

/// <summary>
/// Item 84 — Protection contre les injections de prompt.
/// </summary>
public sealed class PromptInjectionProtector
{
    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;

    private static readonly string[] DangerousPatterns =
    {
        @"ignore previous instructions",
        @"disregard all prior",
        @"you are now",
        @"act as",
        @"pretend you are",
        @"<\|im_start\|>",
        @"<\|im_end\|>"
    };

    public PromptInjectionProtector(StructuredLogCollector log, SettingsEngine settings)
    {
        _log = log;
        _settings = settings;
    }

    public bool IsSafe(string prompt)
    {
        if (!_settings.Shared.Mcp.PromptInjectionProtection.Value) return true;

        foreach (var pattern in DangerousPatterns)
        {
            if (Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase))
            {
                _log.Warning("PromptInjection", "Pattern suspect détecté", new { pattern });
                return false;
            }
        }
        return true;
    }

    public string Sanitize(string prompt)
    {
        // Échappe les tokens spéciaux
        return prompt.Replace("<|", "&lt;|").Replace("|>", "|&gt;");
    }
}
