using System;
using System.Collections.Generic;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public enum WhiteboardShape { Rectangle, Ellipse, Arrow, Freehand, Text }

public sealed class WhiteboardElement
{
    public WhiteboardShape Shape { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string ColorHex { get; set; } = "#FFFFFF";
    public string? Text { get; set; }
}

/// <summary>
/// Idée "Lightweight whiteboard" (P3) — tableau blanc vectoriel simple pour croquis
/// d'architecture (pas d'audio). Modèle de données seulement ; le rendu est une vue MAUI.
/// </summary>
public sealed class WhiteboardService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<WhiteboardElement> _elements = new();
    private readonly object _lock = new();

    public event EventHandler? WhiteboardChanged;

    public WhiteboardService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void AddElement(WhiteboardElement element)
    {
        if (!SettingsCatalog.Collab.WhiteboardEnabled.Value) return;
        lock (_lock) _elements.Add(element);
        WhiteboardChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_lock) _elements.Clear();
        WhiteboardChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<WhiteboardElement> GetElements()
    {
        lock (_lock) return _elements.ToArray();
    }
}
