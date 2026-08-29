using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Editor.Services;

/// <summary>
/// Item 54 — Session bookmarks : persiste onglets ouverts + positions curseur.
/// Stocké dans %AppData%/MotoEditor/session.json. Aucun format propriétaire.
/// </summary>
public sealed class SessionBookmarkService
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", "session.json");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public SessionBookmarkService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void SaveSession(IReadOnlyList<SessionBookmark> bookmarks)
    {
        if (!_settings.Shared.Editor.Ux.SessionBookmarksEnabled.Value) return;
        try
        {
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(bookmarks));
            _log.Info("SessionBookmark", "Session sauvegardée", new { count = bookmarks.Count });
        }
        catch (Exception ex)
        {
            _log.Error("SessionBookmark", "Échec sauvegarde session", new { ex.Message });
        }
    }

    public IReadOnlyList<SessionBookmark> LoadSession()
    {
        if (!_settings.Shared.Editor.Ux.SessionBookmarksEnabled.Value) return Array.Empty<SessionBookmark>();
        try
        {
            if (!File.Exists(SessionPath)) return Array.Empty<SessionBookmark>();
            return JsonSerializer.Deserialize<List<SessionBookmark>>(File.ReadAllText(SessionPath))
                   ?? new List<SessionBookmark>();
        }
        catch (Exception ex)
        {
            _log.Error("SessionBookmark", "Échec chargement session", new { ex.Message });
            return Array.Empty<SessionBookmark>();
        }
    }
}

public sealed class SessionBookmark
{
    public string FilePath { get; set; } = "";
    public int CursorLine { get; set; }
    public int CursorColumn { get; set; }
    public bool IsPinned { get; set; }
}
