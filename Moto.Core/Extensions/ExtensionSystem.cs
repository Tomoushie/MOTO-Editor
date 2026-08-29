// Extensions/ExtensionSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Moto.Editor.Extensions
{
    /// <summary>
    /// Manifeste d'une extension MOTO.
    /// </summary>
    public class ExtensionManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public ExtensionPermissions Permissions { get; set; } = new ExtensionPermissions();
        public List<ExtensionCommand> Commands { get; set; } = new List<ExtensionCommand>();
    }

    /// <summary>
    /// Permissions demandées par une extension.
    /// </summary>
    public class ExtensionPermissions
    {
        public bool AllowShell { get; set; }
        public bool AllowFileWrite { get; set; }
    }

    /// <summary>
    /// Commande déclarée par une extension.
    /// </summary>
    public class ExtensionCommand
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Type de commande :
        /// - internal : appelle une action MOTO existante
        /// - shell : commande système contrôlée
        /// - theme : thème externe
        /// </summary>
        public string Type { get; set; } = "internal";

        public string Target { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contexte fourni aux commandes d'extension.
    /// </summary>
    public class ExtensionContext
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public string ActiveFile { get; set; } = string.Empty;
    }

    /// <summary>
    /// Charge les extensions locales depuis un dossier.
    /// </summary>
    public class ExtensionLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Découvre toutes les extensions dans un dossier.
        /// Chaque extension doit contenir un fichier extension.json.
        /// </summary>
        public IEnumerable<ExtensionManifest> Discover(string extensionsRoot)
        {
            if (!Directory.Exists(extensionsRoot))
            {
                yield break;
            }

            foreach (var folder in Directory.GetDirectories(extensionsRoot))
            {
                var manifestPath = Path.Combine(folder, "extension.json");

                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ExtensionManifest>(json, JsonOptions);

                if (manifest != null)
                {
                    yield return manifest;
                }
            }
        }
    }

    /// <summary>
    /// Runtime d'exécution des extensions.
    /// </summary>
    public class ExtensionRuntime
    {
        private readonly Dictionary<string, Action<ExtensionContext>> _actions =
            new Dictionary<string, Action<ExtensionContext>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enregistre une action interne MOTO.
        /// </summary>
        public void RegisterAction(string commandId, Action<ExtensionContext> handler)
        {
            _actions[commandId] = handler;
        }

        /// <summary>
        /// Active une extension.
        /// </summary>
        public void Activate(ExtensionManifest manifest)
        {
            foreach (var command in manifest.Commands)
            {
                if (command.Type == "internal")
                {
                    _actions[command.Id] = context =>
                    {
                        TryExecute(command.Target, context);
                    };
                }
                else if (command.Type == "shell")
                {
                    _actions[command.Id] = context =>
                    {
                        // Sécurité :
                        // Les commandes shell doivent être validées par l'utilisateur.
                        // Il faut ensuite vérifier les permissions et une allowlist.
                        if (!manifest.Permissions.AllowShell)
                        {
                            return;
                        }

                        // TODO : sandbox + confirmation + audit.
                    };
                }
            }
        }

        /// <summary>
        /// Exécute une commande enregistrée.
        /// </summary>
        public bool TryExecute(string commandId, ExtensionContext context)
        {
            if (_actions.TryGetValue(commandId, out var handler))
            {
                handler(context);
                return true;
            }

            return false;
        }
    }
}
