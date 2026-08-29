// Moto.Core/Performance/IncrementalIndexer.cs
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Moto.Core.Performance;

/// <summary>
/// Index incrémental pour recherche/GoTo : ne réindexe que les fichiers modifiés.
/// </summary>
public sealed class IncrementalIndexer
{
    private readonly ConcurrentDictionary<string, FileIndex> _index = new();
    private readonly string _indexCachePath;

    public static IncrementalIndexer Instance { get; private set; } = null!;

    public IncrementalIndexer(string cachePath)
    {
        _indexCachePath = cachePath;
        Instance = this;
        LoadFromDisk();
    }

    /// <summary>
    /// Indexe un fichier (ou skip si non modifié).
    /// </summary>
    public async Task IndexFileAsync(string filePath)
    {
        var hash = await ComputeFileHashAsync(filePath);

        if (_index.TryGetValue(filePath, out var existing) && existing.Hash == hash)
        {
            return; // Non modifié, skip
        }

        var content = await File.ReadAllTextAsync(filePath);
        var symbols = ExtractSymbols(content);

        _index[filePath] = new FileIndex
        {
            FilePath = filePath,
            Hash = hash,
            Symbols = symbols,
            LastIndexed = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Recherche un symbole dans l'index.
    /// </summary>
    public List<SymbolInfo> Search(string query)
    {
        return _index.Values
            .SelectMany(idx => idx.Symbols)
            .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();
    }

    private List<SymbolInfo> ExtractSymbols(string content)
    {
        // Extraction basique (à remplacer par Roslyn pour C#)
        var symbols = new List<SymbolInfo>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("class ") || line.StartsWith("public class "))
            {
                var name = line.Split(' ')[^1].TrimEnd('{');
                symbols.Add(new SymbolInfo { Name = name, Type = "class", Line = i });
            }
        }

        return symbols;
    }

    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    private void LoadFromDisk()
    {
        // TODO: Charger l'index depuis le disque
    }

    public void SaveToDisk()
    {
        // TODO: Sauvegarder l'index sur disque
    }
}

public class FileIndex
{
    public string FilePath { get; set; } = "";
    public string Hash { get; set; } = "";
    public List<SymbolInfo> Symbols { get; set; } = new();
    public DateTime LastIndexed { get; set; }
}

public class SymbolInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // class, method, property
    public int Line { get; set; }
}
