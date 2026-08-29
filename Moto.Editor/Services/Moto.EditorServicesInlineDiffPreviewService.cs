using System.Collections.Generic;
using System.Linq;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Editor.Services;

/// <summary>
/// Item 54 — Preview diff inline : génère un diff visuel avant d'appliquer
/// une suggestion IA. Ne modifie JAMAIS le document (règle d'architecture :
/// MOTO propose, l'utilisateur/XENO applique).
/// </summary>
public sealed class InlineDiffPreviewService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public InlineDiffPreviewService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public IReadOnlyList<DiffLine> ComputeDiff(string original, string proposed)
    {
        if (!_settings.Shared.Editor.Ux.InlineDiffPreview.Value)
            return System.Array.Empty<DiffLine>();

        var originalLines = original.Split('\n');
        var proposedLines = proposed.Split('\n');
        var result = new List<DiffLine>();

        int max = System.Math.Max(originalLines.Length, proposedLines.Length);
        for (int i = 0; i < max; i++)
        {
            string? orig = i < originalLines.Length ? originalLines[i] : null;
            string? prop = i < proposedLines.Length ? proposedLines[i] : null;

            if (orig == prop)
                result.Add(new DiffLine(DiffKind.Unchanged, orig ?? ""));
            else
            {
                if (orig != null) result.Add(new DiffLine(DiffKind.Removed, orig));
                if (prop != null) result.Add(new DiffLine(DiffKind.Added, prop));
            }
        }

        _log.Debug("InlineDiff", "Diff calculé", new { lines = result.Count });
        return result;
    }
}

public enum DiffKind { Unchanged, Added, Removed }
public sealed record DiffLine(DiffKind Kind, string Content);
