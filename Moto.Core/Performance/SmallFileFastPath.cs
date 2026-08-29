// Moto.Core/Performance/SmallFileFastPath.cs
namespace Moto.Core.Performance;

/// <summary>
/// Bypass complet des pipelines lourds pour fichiers < N KB.
/// </summary>
public sealed class SmallFileFastPath
{
    private const long SizeThresholdBytes = 50 * 1024; // 50 KB

    public static SmallFileFastPath Instance { get; private set; } = null!;

    public SmallFileFastPath()
    {
        Instance = this;
    }

    /// <summary>
    /// Vérifie si un fichier doit utiliser le fast path.
    /// </summary>
    public bool ShouldUseFastPath(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length < SizeThresholdBytes;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Charge un fichier via le fast path (sans LSP, sans indexing).
    /// </summary>
    public async Task<string> LoadFastAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }
}
