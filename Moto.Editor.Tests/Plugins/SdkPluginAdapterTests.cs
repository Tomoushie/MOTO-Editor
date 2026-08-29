// Moto.Core.Tests/Plugins/SdkPluginAdapterTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.Plugins;
using Xunit;
using MotoSdk = Moto.Plugin.SDK;

namespace Moto.Core.Tests.Plugins
{
    /// <summary>
    /// Tests de l'adaptateur SDK : validation du mapping entre
    /// Moto.Plugin.SDK et Moto.Core.Plugins.
    /// </summary>
    public class SdkPluginAdapterTests
    {
        [Fact]
        public void Adapter_Maps_Basic_Properties_Correctly()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPlugin();
            var adapter = new SdkPluginAdapter(sdkPlugin);

            // Assert
            Assert.Equal("test-plugin", adapter.Id);
            Assert.Equal("Test Plugin", adapter.DisplayName);
            Assert.Equal("1.0.0", adapter.Version);
            Assert.Equal("A test plugin.", adapter.Description);
        }

        [Fact]
        public void Adapter_Maps_Settings_Correctly()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPlugin();
            var adapter = new SdkPluginAdapter(sdkPlugin);

            // Act
            var settings = adapter.Settings;

            // Assert
            Assert.Equal(2, settings.Count);
            Assert.Equal("auto_format", settings[0].Key);
            Assert.Equal(SettingType.Toggle, settings[0].Type);
            Assert.Equal(true, settings[0].DefaultValue);

            Assert.Equal("indent_size", settings[1].Key);
            Assert.Equal(SettingType.Int, settings[1].Type);
            Assert.Equal(4, settings[1].DefaultValue);
        }

        [Fact]
        public async Task Adapter_Delegates_InitializeAsync()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPlugin();
            var adapter = new SdkPluginAdapter(sdkPlugin);
            var context = new PluginContext
            {
                WorkspaceRoot = "/test/workspace",
                Settings = new FakeSettingsAccessor()
            };

            // Act
            await adapter.InitializeAsync(context);

            // Assert
            Assert.True(sdkPlugin.InitializeCalled);
        }

        [Fact]
        public async Task Adapter_Delegates_ExecuteCommandAsync()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPlugin();
            var adapter = new SdkPluginAdapter(sdkPlugin);

            // Act
            var result = await adapter.ExecuteCommandAsync("/test format", "context");

            // Assert
            Assert.Equal("Formatted!", result);
            Assert.True(sdkPlugin.ExecuteCalled);
        }

        [Fact]
        public async Task Adapter_Delegates_GetSuggestionsAsync()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPlugin();
            var adapter = new SdkPluginAdapter(sdkPlugin);

            // Act
            var suggestions = await adapter.GetSuggestionsAsync("/test.cs", "content");

            // Assert
            Assert.Single(suggestions);
            Assert.Equal("Fix spacing", suggestions[0].Title);
            Assert.Equal(0.9, suggestions[0].Confidence);
        }

        [Fact]
        public void Adapter_Maps_SettingType_Correctly()
        {
            // Arrange
            var sdkPlugin = new FakeSdkPluginWithAllTypes();
            var adapter = new SdkPluginAdapter(sdkPlugin);

            // Act
            var settings = adapter.Settings;

            // Assert
            Assert.Equal(SettingType.Toggle, settings[0].Type);
            Assert.Equal(SettingType.Int, settings[1].Type);
            Assert.Equal(SettingType.Enum, settings[2].Type);
            Assert.Equal(SettingType.String, settings[3].Type);
        }
    }

    // ── Fakes pour les tests ──

    internal sealed class FakeSdkPlugin : MotoSdk.IPlugin
    {
        public bool InitializeCalled { get; private set; }
        public bool ExecuteCalled { get; private set; }

        public string SdkVersion => "1.0";
        public string Id => "test-plugin";
        public string DisplayName => "Test Plugin";
        public string Version => "1.0.0";
        public string Description => "A test plugin.";

        public IReadOnlyList<MotoSdk.PluginSettingDefinition> Settings => new[]
        {
            new MotoSdk.PluginSettingDefinition
            {
                Key = "auto_format",
                DisplayName = "Auto Format",
                Description = "Enable auto formatting.",
                Type = MotoSdk.SettingType.Toggle,
                DefaultValue = true
            },
            new MotoSdk.PluginSettingDefinition
            {
                Key = "indent_size",
                DisplayName = "Indent Size",
                Description = "Number of spaces.",
                Type = MotoSdk.SettingType.Int,
                DefaultValue = 4
            }
        };

        public Task InitializeAsync(MotoSdk.PluginContext context)
        {
            InitializeCalled = true;
            return Task.CompletedTask;
        }

        public Task<string?> ExecuteCommandAsync(string command, string context)
        {
            ExecuteCalled = true;
            return Task.FromResult<string?>("Formatted!");
        }

        public Task<IReadOnlyList<MotoSdk.PluginSuggestion>> GetSuggestionsAsync(
            string filePath, string content)
        {
            var suggestions = new List<MotoSdk.PluginSuggestion>
            {
                new MotoSdk.PluginSuggestion
                {
                    Title = "Fix spacing",
                    Description = "Remove trailing whitespace.",
                    Action = "/test format",
                    Confidence = 0.9
                }
            };
            return Task.FromResult<IReadOnlyList<MotoSdk.PluginSuggestion>>(suggestions);
        }

        public void Dispose() { }
    }

    internal sealed class FakeSdkPluginWithAllTypes : MotoSdk.IPlugin
    {
        public string SdkVersion => "1.0";
        public string Id => "all-types";
        public string DisplayName => "All Types";
        public string Version => "1.0.0";
        public string Description => "Plugin with all setting types.";

        public IReadOnlyList<MotoSdk.PluginSettingDefinition> Settings => new[]
        {
            new MotoSdk.PluginSettingDefinition { Key = "toggle", Type = MotoSdk.SettingType.Toggle, DefaultValue = false },
            new MotoSdk.PluginSettingDefinition { Key = "int", Type = MotoSdk.SettingType.Int, DefaultValue = 0 },
            new MotoSdk.PluginSettingDefinition { Key = "enum", Type = MotoSdk.SettingType.Enum, DefaultValue = "a", EnumValues = new[] { "a", "b" } },
            new MotoSdk.PluginSettingDefinition { Key = "string", Type = MotoSdk.SettingType.String, DefaultValue = "" }
        };

        public Task InitializeAsync(MotoSdk.PluginContext context) => Task.CompletedTask;
        public Task<string?> ExecuteCommandAsync(string command, string context) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<MotoSdk.PluginSuggestion>> GetSuggestionsAsync(string filePath, string content)
            => Task.FromResult<IReadOnlyList<MotoSdk.PluginSuggestion>>(Array.Empty<MotoSdk.PluginSuggestion>());
        public void Dispose() { }
    }

    internal sealed class FakeSettingsAccessor : IPluginSettingsAccessor
    {
        public T Get<T>(string key, T defaultValue) => defaultValue;
        public void Set<T>(string key, T value) { }
        public event Action<string, object>? Changed { add { } remove { } }
    }
}
