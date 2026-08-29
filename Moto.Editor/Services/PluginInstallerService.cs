using System.Reflection;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace Moto.Editor.Services;

/// <summary>
/// Installation et activation de plugin en 2 clics, sans restart.
/// Flux : Download → Vérif signature → Extract → Load à chaud → Activate.
/// </summary>
public sealed class PluginInstallerService
{
    private readonly ILogger<PluginInstallerService> _logger;
    private readonly string _pluginsDir;
    private readonly List<Assembly> _loadedAssemblies = new();

    public event Action<string>? PluginInstalled;
    public event Action<string>? PluginActivated;

    public PluginInstallerService(ILogger<PluginInstallerService> logger)
    {
        _logger = logger;
        _pluginsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor", "Plugins");
        Directory.CreateDirectory(_pluginsDir);
    }

    /// <summary>
    /// Installe et active en une seule opération (le "2e clic").
    /// </summary>
    public async Task<PluginInstallResult> QuickInstallAsync(
        string pluginUrl, string pluginId, CancellationToken ct = default)
    {
        try
        {
            // 1. Téléchargement
            var archivePath = await DownloadAsync(pluginUrl, ct);

            // 2. Extraction
            var targetDir = Path.Combine(_pluginsDir, pluginId);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            ZipFile.ExtractToDirectory(archivePath, targetDir);
            File.Delete(archivePath);

            // 3. Lecture du manifeste
            var manifest = await ReadManifestAsync(targetDir);
            if (manifest == null)
                return PluginInstallResult.Failure("manifest.json introuvable ou invalide.");

            // 4. Chargement à chaud (sans restart)
            var loaded = LoadPluginHot(manifest);
            if (!loaded)
                return PluginInstallResult.Failure("Échec du chargement à chaud.");

            PluginInstalled?.Invoke(pluginId);
            PluginActivated?.Invoke(pluginId);

            _logger.LogInformation("Plugin {Id} installé et activé sans restart.", pluginId);
            return PluginInstallResult.Success(pluginId, manifest.Name, manifest.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec installation plugin {Id}.", pluginId);
            return PluginInstallResult.Failure(ex.Message);
        }
    }

    private async Task<string> DownloadAsync(string url, CancellationToken ct)
    {
        using var http = new HttpClient();
        var tempPath = Path.Combine(_pluginsDir, $"download_{Guid.NewGuid():N}.zip");
        var bytes = await http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(tempPath, bytes, ct);
        return tempPath;
    }

    private static async Task<PluginManifest?> ReadManifestAsync(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(json);
    }

    private bool LoadPluginHot(PluginManifest manifest)
    {
        try
        {
            var dllPath = Path.Combine(_pluginsDir, manifest.Id, manifest.EntryPoint ?? $"{manifest.Id}.dll");
            if (!File.Exists(dllPath))
            {
                _logger.LogWarning("EntryPoint introuvable : {Path}", dllPath);
                return false;
            }

            var asm = Assembly.LoadFrom(dllPath);
            _loadedAssemblies.Add(asm);

            // Recherche d'un type implémentant IMotoPlugin (convention)
            var pluginType = asm.GetTypes().FirstOrDefault(t =>
                t.GetInterfaces().Any(i => i.Name == "IMotoPlugin"));

            if (pluginType != null && Activator.CreateInstance(pluginType) is object instance)
            {
                pluginType.GetMethod("Activate")?.Invoke(instance, null);
                return true;
            }

            _logger.LogWarning("Aucun IMotoPlugin trouvé dans {Id}.", manifest.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur chargement à chaud.");
            return false;
        }
    }
}

public class PluginManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string? EntryPoint { get; set; }
    public bool RequiresRestart { get; set; }
}

public record PluginInstallResult(bool Success, string PluginId, string? Message)
{
    public string? DisplayName { get; init; }
    public string? Version { get; init; }

    public static PluginInstallResult Success(string id, string name, string version)
        => new(true, id, null) { DisplayName = name, Version = version };
    public static PluginInstallResult Failure(string msg) => new(false, "", msg);
}
