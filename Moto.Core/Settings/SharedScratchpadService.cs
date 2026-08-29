using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public sealed class ScratchpadSnippet
{
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Idée "Shared scratchpads" (P2) — notes/snippets temporaires partagés.
/// La synchro P2P utilise le transport CollabSession existant (non réinventé).
/// </summary>
public sealed class SharedScratchpadService
{
    private static readonly string ScratchDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "scratchpads");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<ScratchpadSnippet> _snippets = new();
    private readonly object _lock = new();

    public event EventHandler? ScratchpadChanged;

    public SharedScratchpadService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        Directory.CreateDirectory(ScratchDir);
    }

    public void AppendSnippet(ScratchpadSnippet snippet)
    {
        if (!_settings.Shared.Collab.SharedScratchpadsEnabled.Value) return;
        lock (_lock) _snippets.Add(snippet);
        Persist();
        ScratchpadChanged?.Invoke(this, EventArgs.Empty);
        // La synchro P2P est déléguée au transport CollabSession existant.
    }

    public IReadOnlyList<ScratchpadSnippet> GetSnippets()
    {
        lock (_lock) return _snippets.ToArray();
    }

    private void Persist()
    {
        lock (_lock)
            File.WriteAllText(Path.Combine(ScratchDir, "scratchpad.json"),
                              JsonSerializer.Serialize(_snippets));
    }
}
