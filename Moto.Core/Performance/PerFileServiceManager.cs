// Moto.Core/Performance/PerFileServiceManager.cs
using System.Collections.Concurrent;
using Moto.Core.LSP;

namespace Moto.Core.Performance;

/// <summary>
/// Charge les services LSP/diagnostics uniquement pour les fichiers ouverts.
/// Décharge automatiquement après inactivité (5 min par défaut).
/// </summary>
public sealed class PerFileServiceManager : IDisposable
{
    private readonly ConcurrentDictionary<string, FileServiceSession> _sessions = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _idleTimeout;

    public static PerFileServiceManager Instance { get; private set; } = null!;

    public PerFileServiceManager(TimeSpan? idleTimeout = null)
    {
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
        Instance = this;
        _cleanupTimer = new Timer(CleanupIdleSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Récupère ou crée une session LSP pour un fichier.
    /// </summary>
    public FileServiceSession GetOrCreateSession(string filePath)
    {
        return _sessions.GetOrAdd(filePath, path =>
        {
            var session = new FileServiceSession(path);
            session.Initialize();
            return session;
        });
    }

    /// <summary>
    /// Marque un fichier comme actif (réinitialise le timer d'inactivité).
    /// </summary>
    public void MarkActive(string filePath)
    {
        if (_sessions.TryGetValue(filePath, out var session))
        {
            session.LastAccess = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Ferme explicitement une session (fichier fermé par l'utilisateur).
    /// </summary>
    public void CloseSession(string filePath)
    {
        if (_sessions.TryRemove(filePath, out var session))
        {
            session.Dispose();
        }
    }

    private void CleanupIdleSessions(object? state)
    {
        var cutoff = DateTime.UtcNow - _idleTimeout;
        var idleSessions = _sessions
            .Where(kvp => kvp.Value.LastAccess < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var path in idleSessions)
        {
            CloseSession(path);
        }
    }

    public int ActiveSessionCount => _sessions.Count;

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }
}

public class FileServiceSession : IDisposable
{
    public string FilePath { get; }
    public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    public LanguageServerClient? LspClient { get; private set; }
    public bool IsInitialized { get; private set; }

    public FileServiceSession(string filePath)
    {
        FilePath = filePath;
    }

    public void Initialize()
    {
        if (IsInitialized) return;

        // Charge le LSP uniquement pour ce fichier
        // LspClient = new LanguageServerClient(FilePath);
        // LspClient.Start();

        IsInitialized = true;
    }

    public void Dispose()
    {
        LspClient?.Dispose();
        IsInitialized = false;
    }
}
