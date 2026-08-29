// Moto.Marketplace.Server/Program.cs
// API REST minimale pour héberger le catalogue de plugins.
// Usage : dotnet run --urls "http://localhost:5000"
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── Catalogue statique (à remplacer par une base de données en production) ──
var catalog = new List<MarketplaceEntry>
{
    new()
    {
        Id = "sample-format",
        Name = "🎨 Sample Format",
        Author = "MOTO Team",
        Version = "1.0.0",
        Description = "Formatage automatique du code selon vos conventions.",
        DownloadUrl = "http://localhost:5000/plugins/sample-format-1.0.0.dll",
        Sha256 = "a1b2c3d4e5f6...", // À calculer avec : sha256sum sample-format.dll
        DownloadCount = 142,
        Rating = 4.8
    },
    new()
    {
        Id = "python-assistant",
        Name = "🐍 Python Assistant",
        Author = "Community",
        Version = "2.1.0",
        Description = "Suggestions intelligentes pour Python (PEP 8, type hints).",
        DownloadUrl = "http://localhost:5000/plugins/python-assistant-2.1.0.dll",
        Sha256 = "f6e5d4c3b2a1...",
        DownloadCount = 89,
        Rating = 4.5
    }
};

// ── Endpoint : liste des plugins ──
app.MapGet("/api/v1/plugins", (string? search) =>
{
    var results = string.IsNullOrWhiteSpace(search)
        ? catalog
        : catalog.Where(p =>
            p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

    return Results.Json(results);
});

// ── Endpoint : détails d'un plugin ──
app.MapGet("/api/v1/plugins/{id}", (string id) =>
{
    var plugin = catalog.FirstOrDefault(p => p.Id == id);
    return plugin is null ? Results.NotFound() : Results.Json(plugin);
});

// ── Endpoint : téléchargement d'un plugin ──
app.MapGet("/plugins/{filename}", (string filename) =>
{
    var pluginsDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
    var filePath = Path.Combine(pluginsDir, filename);

    if (!File.Exists(filePath))
        return Results.NotFound($"Plugin non trouvé : {filename}");

    return Results.File(filePath, "application/octet-stream");
});

// ── Endpoint : health check ──
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// ── Modèles ──
record MarketplaceEntry
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public long DownloadCount { get; init; }
    public double Rating { get; init; }
}
