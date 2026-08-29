using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Snippets
{
    public sealed class Snippet
    {
        public string Id { get; init; } = string.Empty;
        public string Trigger { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Language { get; init; } = "csharp";
        public string Author { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = new();
        public SnippetMetadata Metadata { get; init; } = new();
    }

    public sealed class SnippetMetadata
    {
        public long UsageCount { get; set; }
        public double Rating { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class ProjectTemplate
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Language { get; init; } = "csharp";
        public List<TemplateFile> Files { get; init; } = new();
        public Dictionary<string, string> Variables { get; init; } = new();
    }

    public sealed class TemplateFile
    {
        public string Path { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Gestionnaire de snippets et templates avec marketplace.
    /// </summary>
    public sealed class SnippetManager
    {
        private readonly ILogger<SnippetManager> _logger;
        private readonly string _snippetsDirectory;
        private readonly string _templatesDirectory;

        public SnippetManager(ILogger<SnippetManager> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _snippetsDirectory = Path.Combine(appData, "MotoEditor", "snippets");
            _templatesDirectory = Path.Combine(appData, "MotoEditor", "templates");

            Directory.CreateDirectory(_snippetsDirectory);
            Directory.CreateDirectory(_templatesDirectory);
        }

        /// <summary>
        /// Récupère tous les snippets pour un langage.
        /// </summary>
        public IReadOnlyList<Snippet> GetSnippets(string language)
        {
            var snippets = new List<Snippet>();
            var langDir = Path.Combine(_snippetsDirectory, language);

            if (!Directory.Exists(langDir))
                return GetBuiltinSnippets(language);

            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var snippet = JsonSerializer.Deserialize<Snippet>(json);
                    if (snippet != null) snippets.Add(snippet);
                }
                catch { }
            }

            return snippets.Concat(GetBuiltinSnippets(language)).ToList();
        }

        /// <summary>
        /// Insère un snippet dans le code avec substitution des variables.
        /// </summary>
        public string InsertSnippet(Snippet snippet, Dictionary<string, string>? variables = null)
        {
            var result = snippet.Body;

            if (variables != null)
            {
                foreach (var (key, value) in variables)
                {
                    result = result.Replace($"${{{key}}}", value);
                }
            }

            return result;
        }

        /// <summary>
        /// Installe un snippet depuis un fichier.
        /// </summary>
        public bool InstallSnippet(Snippet snippet)
        {
            try
            {
                var langDir = Path.Combine(_snippetsDirectory, snippet.Language);
                Directory.CreateDirectory(langDir);

                var path = Path.Combine(langDir, $"{snippet.Id}.json");
                var json = JsonSerializer.Serialize(snippet, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(path, json);

                _logger.LogInformation("[SnippetManager] Snippet installé : {Trigger}", snippet.Trigger);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SnippetManager] Erreur installation snippet");
                return false;
            }
        }

        /// <summary>
        /// Génère des snippets prédéfinis.
        /// </summary>
        private IReadOnlyList<Snippet> GetBuiltinSnippets(string language)
        {
            if (language == "csharp")
            {
                return new List<Snippet>
                {
                    new()
                    {
                        Id = "prop",
                        Trigger = "prop",
                        Description = "Property with backing field",
                        Body = "private ${type} _${name};\n\npublic ${type} ${Name}\n{\n    get => _${name};\n    set => _${name} = value;\n}",
                        Language = "csharp",
                        Author = "MOTO Team"
                    },
                    new()
                    {
                        Id = "ctor",
                        Trigger = "ctor",
                        Description = "Constructor",
                        Body = "public ${ClassName}()\n{\n    ${cursor}\n}",
                        Language = "csharp",
                        Author = "MOTO Team"
                    },
                    new()
                    {
                        Id = "try",
                        Trigger = "try",
                        Description = "Try-catch block",
                        Body = "try\n{\n    ${cursor}\n}\ncatch (Exception ex)\n{\n    Console.WriteLine(ex.Message);\n}",
                        Language = "csharp",
                        Author = "MOTO Team"
                    },
                    new()
                    {
                        Id = "for",
                        Trigger = "for",
                        Description = "For loop",
                        Body = "for (int ${i} = 0; ${i} < ${length}; ${i}++)\n{\n    ${cursor}\n}",
                        Language = "csharp",
                        Author = "MOTO Team"
                    },
                    new()
                    {
                        Id = "foreach",
                        Trigger = "foreach",
                        Description = "Foreach loop",
                        Body = "foreach (var ${item} in ${collection})\n{\n    ${cursor}\n}",
                        Language = "csharp",
                        Author = "MOTO Team"
                    }
                };
            }

            return new List<Snippet>();
        }
    }
}
