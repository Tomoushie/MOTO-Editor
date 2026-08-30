using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public enum AnnotationKind { Note, Todo, MeetingNote }

public sealed class LineAnnotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public string Text { get; set; } = "";
    public AnnotationKind Kind { get; set; } = AnnotationKind.Note;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? MeetingId { get; set; }
}

/// <summary>
/// Idées "Annotation layers" (P2) et "Meeting notes linked to files" (P3).
/// Overlays persistantes attachées aux lignes, stockées dans .moto/annotations/.
/// </summary>
public sealed class AnnotationLayerService
{
    private static readonly string AnnotationDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "annotations");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<LineAnnotation> _annotations = new();
    private readonly object _lock = new();

    public event EventHandler? AnnotationsChanged;

    public AnnotationLayerService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        Directory.CreateDirectory(AnnotationDir);
        Load();
    }

    public void AddAnnotation(LineAnnotation annotation)
    {
        if (!SettingsCatalog.Collab.AnnotationLayersEnabled.Value) return;
        if (annotation.Kind == AnnotationKind.MeetingNote &&
            !SettingsCatalog.Collab.MeetingNotesLinkingEnabled.Value) return;

        lock (_lock) _annotations.Add(annotation);
        Persist();
        AnnotationsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Idée "Meeting notes linked to files" — relie une note de réunion à un emplacement de code.</summary>
    public void LinkMeetingNote(string meetingId, string filePath, int line, string note)
    {
        AddAnnotation(new LineAnnotation
        {
            FilePath = filePath,
            Line = line,
            Text = note,
            Kind = AnnotationKind.MeetingNote,
            MeetingId = meetingId
        });
    }

    public IReadOnlyList<LineAnnotation> GetAnnotationsForFile(string filePath)
    {
        lock (_lock) return _annotations.Where(a => a.FilePath == filePath).ToList();
    }

    private void Persist()
    {
        lock (_lock)
            File.WriteAllText(Path.Combine(AnnotationDir, "annotations.json"),
                              JsonSerializer.Serialize(_annotations));
    }

    private void Load()
    {
        var path = Path.Combine(AnnotationDir, "annotations.json");
        if (!File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<LineAnnotation>>(File.ReadAllText(path));
            if (loaded != null) lock (_lock) { _annotations.Clear(); _annotations.AddRange(loaded); }
        }
        catch (Exception ex) { _log.Error("AnnotationLayer", "Échec chargement", new { ex.Message }); }
    }
}
