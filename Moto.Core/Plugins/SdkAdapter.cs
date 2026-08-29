// Moto.Core/Plugins/SdkAdapter.cs
// Adaptateur entre le SDK public et les types internes Moto.Core.Plugins.
// Cet adaptateur évite tout refactor massif : l'existant continue de compiler.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MotoSdk = Moto.Plugin.SDK;

namespace Moto.Core.Plugins
{
    /// <summary>
    /// Wrapper qui expose un plugin SDK sous la forme attendue par PluginRegistry.
    /// Utilisé uniquement si vous migrez Moto.Core.Plugins vers le SDK.
    /// </summary>
    public sealed class SdkPluginAdapter : IPlugin
    {
        private readonly MotoSdk.IPlugin _sdkPlugin;

        public SdkPluginAdapter(MotoSdk.IPlugin sdkPlugin)
            => _sdkPlugin = sdkPlugin ?? throw new ArgumentNullException(nameof(sdkPlugin));

        public string Id => _sdkPlugin.Id;
        public string DisplayName => _sdkPlugin.DisplayName;
        public string Version => _sdkPlugin.Version;
        public string Description => _sdkPlugin.Description;

        public IReadOnlyList<PluginSettingDefinition> Settings
        {
            get
            {
                var list = new List<PluginSettingDefinition>();
                foreach (var s in _sdkPlugin.Settings)
                {
                    list.Add(new PluginSettingDefinition(
                        Key: s.Key,
                        DisplayName: s.DisplayName,
                        Description: s.Description,
                        Type: MapType(s.Type),
                        DefaultValue: s.DefaultValue ?? GetDefaultForType(s.Type),
                        EnumValues: s.EnumValues
                    ));
                }
                return list;
            }
        }

        public Task InitializeAsync(PluginContext context)
        {
            // Pont : on passe le contexte interne au SDK.
            var sdkContext = new MotoSdk.PluginContext
            {
                WorkspaceRoot = context.WorkspaceRoot,
                Settings = new SdkSettingsAdapter(context.Settings),
                Logger = new SdkLoggerAdapter()
            };
            return _sdkPlugin.InitializeAsync(sdkContext);
        }

        public Task<string?> ExecuteCommandAsync(string command, string context)
            => _sdkPlugin.ExecuteCommandAsync(command, context);

        public async Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(string filePath, string content)
        {
            var sdkSuggestions = await _sdkPlugin.GetSuggestionsAsync(filePath, content);
            var list = new List<PluginSuggestion>();
            foreach (var s in sdkSuggestions)
            {
                list.Add(new PluginSuggestion(s.Title, s.Description, s.Action, s.Confidence));
            }
            return list;
        }

        public void Dispose() => _sdkPlugin.Dispose();

        private static SettingType MapType(MotoSdk.SettingType type) => type switch
        {
            MotoSdk.SettingType.Toggle => SettingType.Toggle,
            MotoSdk.SettingType.Int => SettingType.Int,
            MotoSdk.SettingType.Enum => SettingType.Enum,
            MotoSdk.SettingType.String => SettingType.String,
            _ => SettingType.Toggle
        };

        private static object GetDefaultForType(MotoSdk.SettingType type) => type switch
        {
            MotoSdk.SettingType.Toggle => false,
            MotoSdk.SettingType.Int => 0,
            MotoSdk.SettingType.String => string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>Adaptateur paramètres interne → SDK.</summary>
    internal sealed class SdkSettingsAdapter : MotoSdk.IPluginSettingsAccessor
    {
        private readonly IPluginSettingsAccessor _inner;
        public SdkSettingsAdapter(IPluginSettingsAccessor inner) => _inner = inner;

        public T Get<T>(string key, T defaultValue) => _inner.Get(key, defaultValue);
        public void Set<T>(string key, T value) => _inner.Set(key, value);

        public event Action<string, object?>? Changed
        {
            add => _inner.Changed += (k, v) => value?.Invoke(k, v);
            remove { } // Simplification : la suppression d'handler est rare côté plugin.
        }
    }

    /// <summary>Adaptateur logger par défaut (redirige vers Debug).</summary>
    internal sealed class SdkLoggerAdapter : MotoSdk.IPluginLogger
    {
        public void Debug(string message) => System.Diagnostics.Debug.WriteLine($"[Plugin] {message}");
        public void Info(string message) => System.Diagnostics.Debug.WriteLine($"[Plugin] {message}");
        public void Warn(string message) => System.Diagnostics.Debug.WriteLine($"[Plugin][WARN] {message}");
        public void Error(string message, Exception? ex = null)
            => System.Diagnostics.Debug.WriteLine($"[Plugin][ERROR] {message} {ex?.Message}");
    }
}
