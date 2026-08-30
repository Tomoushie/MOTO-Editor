// Moto.Core/Plugins/PluginRegistry.cs
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Plugins
{
    public sealed class PluginRegistry
    {
        private readonly SettingsEngine _settings;
        private readonly ILogger<PluginRegistry> _logger;
        private readonly List<IPlugin> _plugins = new();

        public PluginRegistry(SettingsEngine settings, ILogger<PluginRegistry> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<IPlugin> GetActivePlugins() => _plugins.AsReadOnly();

        public void Register(IPlugin plugin)
        {
            if (plugin is null)
                return;

            _plugins.Add(plugin);
            _logger.LogInformation("[Plugins] Plugin enregistré : {Id}", plugin.Id);
        }
    }
}
