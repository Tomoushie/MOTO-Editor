using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Moto.Core.Themes
{
    public sealed class ThemeDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Version { get; init; } = "1.0.0";
        public string Description { get; init; } = string.Empty;
        public ThemeColors Colors { get; init; } = new();
        public ThemeTypography Typography { get; init; } = new();
        public ThemeMetadata Metadata { get; init; } = new();
    }

    public sealed class ThemeColors
    {
        public string Background { get; set; } = "#1E1F24";
        public string BackgroundSide { get; set; } = "#2A2C31";
        public string BackgroundHover { get; set; } = "#35373C";
        public string Text1 { get; set; } = "#E5E7EB";
        public string Text2 { get; set; } = "#9CA3AF";
        public string Accent { get; set; } = "#D97757";
        public string Success { get; set; } = "#10B981";
        public string Warning { get; set; } = "#F59E0B";
        public string Error { get; set; } = "#EF4444";
        public string Info { get; set; } = "#3B82F6";
    }

    public sealed class ThemeTypography
    {
        public string FontFamily { get; set; } = "OpenSans";
        public int FontSizeBase { get; set; } = 12;
        public int FontSizeSmall { get; set; } = 10;
        public int FontSizeLarge { get; set; } = 16;
    }

    public sealed class ThemeMetadata
    {
        public long DownloadCount { get; set; }
        public double Rating { get; set; }
        public DateTime PublishedUtc { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Gestionnaire de thèmes avec support marketplace et application live.
    /// </summary>
    public sealed class ThemeManager
    {
        private readonly ILogger<ThemeManager> _logger;
        private readonly string _themesDirectory;
        private readonly string _currentThemePath;
        private ThemeDefinition? _currentTheme;

        public event Action<ThemeDefinition>? ThemeChanged;

        public ThemeManager(ILogger<ThemeManager> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _themesDirectory = Path.Combine(appData, "MotoEditor", "themes");
            _currentThemePath = Path.Combine(appData, "MotoEditor", "current-theme.json");

            Directory.CreateDirectory(_themesDirectory);
            LoadCurrentTheme();
        }

        public ThemeDefinition CurrentTheme => _currentTheme ?? GetDefaultTheme();

        /// <summary>
        /// Applique un thème à l'application MAUI.
        /// </summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            try
            {
                _currentTheme = theme;
                SaveCurrentTheme();

                // Application aux ressources MAUI
                if (Application.Current?.Resources != null)
                {
                    var resources = Application.Current.Resources;

                    resources["BgPanel"] = Color.FromArgb(theme.Colors.Background);
                    resources["BgSide"] = Color.FromArgb(theme.Colors.BackgroundSide);
                    resources["BgHover"] = Color.FromArgb(theme.Colors.BackgroundHover);
                    resources["Txt1"] = Color.FromArgb(theme.Colors.Text1);
                    resources["Txt2"] = Color.FromArgb(theme.Colors.Text2);
                    resources["Accent"] = Color.FromArgb(theme.Colors.Accent);
                    resources["Success"] = Color.FromArgb(theme.Colors.Success);
                    resources["Warning"] = Color.FromArgb(theme.Colors.Warning);
                    resources["Error"] = Color.FromArgb(theme.Colors.Error);
                    resources["Info"] = Color.FromArgb(theme.Colors.Info);
                }

                ThemeChanged?.Invoke(theme);
                _logger.LogInformation("[ThemeManager] Thème appliqué : {Name}", theme.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ThemeManager] Erreur application thème");
            }
        }

        /// <summary>
        /// Installe un thème depuis un fichier.
        /// </summary>
        public bool InstallTheme(string sourcePath)
        {
            try
            {
                var json = File.ReadAllText(sourcePath);
                var theme = JsonSerializer.Deserialize<ThemeDefinition>(json);
                if (theme == null) return false;

                var destPath = Path.Combine(_themesDirectory, $"{theme.Id}.json");
                File.Copy(sourcePath, destPath, overwrite: true);

                _logger.LogInformation("[ThemeManager] Thème installé : {Name}", theme.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ThemeManager] Erreur installation thème");
                return false;
            }
        }

        /// <summary>
        /// Liste tous les thèmes installés.
        /// </summary>
        public IReadOnlyList<ThemeDefinition> GetInstalledThemes()
        {
            var themes = new List<ThemeDefinition> { GetDefaultTheme() };

            foreach (var file in Directory.GetFiles(_themesDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var theme = JsonSerializer.Deserialize<ThemeDefinition>(json);
                    if (theme != null) themes.Add(theme);
                }
                catch { }
            }

            return themes;
        }

        /// <summary>
        /// Génère des thèmes prédéfinis.
        /// </summary>
        public IReadOnlyList<ThemeDefinition> GeneratePresetThemes()
        {
            return new List<ThemeDefinition>
            {
                new()
                {
                    Id = "dark-default",
                    Name = "Dark Default",
                    Author = "MOTO Team",
                    Description = "Thème sombre par défaut",
                    Colors = new ThemeColors
                    {
                        Background = "#1E1F24",
                        BackgroundSide = "#2A2C31",
                        Accent = "#D97757"
                    }
                },
                new()
                {
                    Id = "light-default",
                    Name = "Light Default",
                    Author = "MOTO Team",
                    Description = "Thème clair par défaut",
                    Colors = new ThemeColors
                    {
                        Background = "#FFFFFF",
                        BackgroundSide = "#F3F4F6",
                        BackgroundHover = "#E5E7EB",
                        Text1 = "#111827",
                        Text2 = "#6B7280",
                        Accent = "#D97757"
                    }
                },
                new()
                {
                    Id = "monokai",
                    Name = "Monokai",
                    Author = "Community",
                    Description = "Inspiré de Sublime Text",
                    Colors = new ThemeColors
                    {
                        Background = "#272822",
                        BackgroundSide = "#1E1F1C",
                        Accent = "#F92672"
                    }
                },
                new()
                {
                    Id = "dracula",
                    Name = "Dracula",
                    Author = "Community",
                    Description = "Thème Dracula populaire",
                    Colors = new ThemeColors
                    {
                        Background = "#282A36",
                        BackgroundSide = "#21222C",
                        Accent = "#BD93F9"
                    }
                },
                new()
                {
                    Id = "solarized-dark",
                    Name = "Solarized Dark",
                    Author = "Community",
                    Description = "Solarized en mode sombre",
                    Colors = new ThemeColors
                    {
                        Background = "#002B36",
                        BackgroundSide = "#073642",
                        Accent = "#B58900"
                    }
                },
                new()
                {
                    Id = "nord",
                    Name = "Nord",
                    Author = "Community",
                    Description = "Palette Nord apaisante",
                    Colors = new ThemeColors
                    {
                        Background = "#2E3440",
                        BackgroundSide = "#3B4252",
                        Accent = "#88C0D0"
                    }
                }
            };
        }

        private ThemeDefinition GetDefaultTheme()
        {
            return new ThemeDefinition
            {
                Id = "default",
                Name = "Default",
                Author = "MOTO Team",
                Description = "Thème par défaut"
            };
        }

        private void LoadCurrentTheme()
        {
            try
            {
                if (File.Exists(_currentThemePath))
                {
                    var json = File.ReadAllText(_currentThemePath);
                    _currentTheme = JsonSerializer.Deserialize<ThemeDefinition>(json);
                }
            }
            catch { }
        }

        private void SaveCurrentTheme()
        {
            try
            {
                if (_currentTheme != null)
                {
                    var json = JsonSerializer.Serialize(_currentTheme, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_currentThemePath, json);
                }
            }
            catch { }
        }
    }
}
