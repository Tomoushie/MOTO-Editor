// Moto.Plugin.MarketplaceClient/MarketplaceClientPlugin.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moto.Plugin.SDK;
using Moto.Core.Plugins.Marketplace;

namespace Moto.Plugin.MarketplaceClient
{
    /// <summary>
    /// Plugin qui permet d'installer d'autres plugins directement depuis l'éditeur.
    /// Fournit des commandes : /marketplace search | install | list | update
    /// </summary>
    public sealed class MarketplaceClientPlugin : PluginBase
    {
        private MarketplaceClient? _client;

        public override string Id => "marketplace-client";
        public override string DisplayName => "🛒 Marketplace Client";
        public override string Version => "1.0.0";
        public override string Description => "Installez des plugins directement depuis l'éditeur.";

        public override IReadOnlyList<PluginSettingDefinition> Settings => new[]
        {
            new PluginSettingDefinition
            {
                Key = "marketplace_url",
                DisplayName = "URL du Marketplace",
                Description = "Adresse du serveur marketplace.",
                Type = SettingType.String,
                DefaultValue = "https://marketplace.moto-editor.dev/api/v1"
            }
        };

        protected override Task OnInitializeAsync(PluginContext context)
        {
            var url = GetSetting("marketplace_url", "https://marketplace.moto-editor.dev/api/v1");
            _client = new MarketplaceClient(url);
            Logger.Info($"[MarketplaceClient] Connecté à {url}");
            return Task.CompletedTask;
        }

        public override async Task<string?> ExecuteCommandAsync(string command, string context)
        {
            if (!command.StartsWith("/marketplace", StringComparison.OrdinalIgnoreCase))
                return null;

            var parts = command.Substring("/marketplace".Length).Trim().Split(' ', 2);
            var action = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1].Trim() : "";

            return action switch
            {
                "search" => await SearchAsync(arg),
                "install" => await InstallAsync(arg),
                "list" => await ListAsync(),
                "update" => await UpdateAsync(arg),
                "help" => "Commandes : /marketplace search <query> | install <id> | list | update <id> | help",
                _ => $"Commande inconnue : {action}"
            };
        }

        private async Task<string> SearchAsync(string query)
        {
            if (_client == null) return "❌ Client non initialisé.";

            Logger.Info($"[MarketplaceClient] Recherche : {query}");

            try
            {
                var results = await _client.GetCatalogAsync(query);
                if (results.Count == 0) return "Aucun plugin trouvé.";

                var lines = new List<string> { $"📋 {results.Count} résultat(s) :" };
                foreach (var plugin in results)
                {
                    lines.Add($"  • {plugin.Name} v{plugin.Version} par {plugin.Author}");
                    lines.Add($"    {plugin.Description}");
                    lines.Add($"    ⬇ {plugin.DownloadCount} · ★ {plugin.Rating:0.0}");
                    lines.Add($"    ID : {plugin.Id}");
                }

                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                return $"❌ Erreur : {ex.Message}";
            }
        }

        private async Task<string> InstallAsync(string pluginId)
        {
            if (_client == null) return "❌ Client non initialisé.";
            if (string.IsNullOrWhiteSpace(pluginId)) return "Usage : /marketplace install <id>";

            Logger.Info($"[MarketplaceClient] Installation : {pluginId}");

            try
            {
                var catalog = await _client.GetCatalogAsync();
                var plugin = catalog.Find(p => p.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

                if (plugin == null) return $"❌ Plugin non trouvé : {pluginId}";

                var pluginsDir = Path.Combine(WorkspaceRoot, "plugins");
                var result = await _client.InstallAsync(plugin, pluginsDir);

                if (result.Success)
                {
                    Logger.Info($"[MarketplaceClient] Installé : {result.InstalledPath}");
                    return $"✅ Plugin installé : {plugin.Name}\nRedémarrez l'éditeur pour l'activer.";
                }
                else
                {
                    return $"❌ Échec : {result.Message}";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Erreur : {ex.Message}";
            }
        }

        private async Task<string> ListAsync()
        {
            if (_client == null) return "❌ Client non initialisé.";

            try
            {
                var catalog = await _client.GetCatalogAsync();
                var lines = new List<string> { $"📋 {catalog.Count} plugin(s) disponible(s) :" };

                foreach (var plugin in catalog.Take(20)) // Limite à 20 pour ne pas spammer
                {
                    lines.Add($"  • {plugin.Name} v{plugin.Version} ({plugin.Id})");
                }

                if (catalog.Count > 20)
                    lines.Add($"  … et {catalog.Count - 20} autres.");

                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                return $"❌ Erreur : {ex.Message}";
            }
        }

        private async Task<string> UpdateAsync(string pluginId)
        {
            // TODO: implémenter la mise à jour
            return "🔄 Mise à jour non implémentée.";
        }

        public override void Dispose()
        {
            _client?.Dispose();
            base.Dispose();
        }
    }
}
