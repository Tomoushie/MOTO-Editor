// Moto.Core.Tests/Plugins/MarketplaceE2ETests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moto.Core.Plugins;
using Moto.Core.Plugins.Marketplace;
using Xunit;

namespace Moto.Core.Tests.Plugins
{
    public class MarketplaceE2ETests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _pluginsDir;

        public MarketplaceE2ETests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _pluginsDir = Path.Combine(_tempDir, "plugins");
            Directory.CreateDirectory(_pluginsDir);
        }

        [Fact]
        public void E2E_PluginManifest_IsValid()
        {
            var manifest = new PluginManifestPro
            {
                Id = "cortex-booster",
                Name = "Cortex Booster",
                Version = "1.0.0",
                Author = "MOTO Team",
                Kind = PluginKind.Ai,
                Category = PluginCategory.Productivity,
                Dependencies = new[]
                {
                    new PluginDependencyInfo
                    {
                        PluginId = "moto-core",
                        VersionRange = ">=1.0.0",
                        IsOptional = false
                    }
                }
            };

            Assert.Equal("cortex-booster", manifest.Id);
            Assert.Equal(PluginKind.Ai, manifest.Kind);
            Assert.Single(manifest.Dependencies);
        }

        [Fact]
        public void E2E_PluginKinds_AreCorrect()
        {
            // Plugin IA
            var aiPlugin = new PluginManifestPro { Id = "ai-doc-enhancer", Kind = PluginKind.Ai };
            Assert.Equal(PluginKind.Ai, aiPlugin.Kind);

            // Plugin UI
            var uiPlugin = new PluginManifestPro { Id = "zen-glass-theme", Kind = PluginKind.Ui };
            Assert.Equal(PluginKind.Ui, uiPlugin.Kind);

            // Plugin système
            var sysPlugin = new PluginManifestPro { Id = "auto-port-linux", Kind = PluginKind.System };
            Assert.Equal(PluginKind.System, sysPlugin.Kind);
        }

        [Fact]
        public void E2E_PluginCategories_AreValid()
        {
            var categories = Enum.GetValues<PluginCategory>();
            Assert.True(categories.Length >= 8);
            Assert.Contains(PluginCategory.Productivity, categories);
            Assert.Contains(PluginCategory.Theme, categories);
            Assert.Contains(PluginCategory.Debugger, categories);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }
}
