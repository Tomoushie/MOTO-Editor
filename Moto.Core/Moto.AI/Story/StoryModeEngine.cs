// Moto.Core/AI/Story/StoryModeEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Moto.Core.AI.Builders;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Story
{
    public enum EntityKind { Hero, Enemy, Object, Place }

    public class StoryEntity
    {
        public string Name { get; set; } = string.Empty;
        public EntityKind Kind { get; set; }
    }

    public class StoryBehavior
    {
        public string Subject { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Follow, Attack...
        public string Target { get; set; } = string.Empty;
    }

    /// <summary>Carte mentale extraite de l'histoire.</summary>
    public class StoryMap
    {
        public string Title { get; set; } = string.Empty;
        public List<StoryEntity> Entities { get; } = new();
        public List<StoryBehavior> Behaviors { get; } = new();
        public List<string> Modules { get; } = new();
        public List<string> PlotSteps { get; } = new();
    }

    /// <summary>
    /// 26. AI Story Mode : l'utilisateur raconte une histoire,
    /// MOTO AI en extrait entités, comportements, lieux et intrigue,
    /// puis génère le code ECS correspondant.
    /// </summary>
    public class StoryModeEngine
    {
        private readonly BehaviorBuilderV2 _behaviors = new();
        private readonly CodeGenerationEngine _generation = new();

        // Lexiques français : nom → rôle dans le jeu.
        private static readonly Dictionary<string, EntityKind> Nouns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["chevalier"] = EntityKind.Hero, ["héros"] = EntityKind.Hero, ["joueur"] = EntityKind.Hero,
            ["princesse"] = EntityKind.Hero, ["sorcier"] = EntityKind.Hero,
            ["dragon"] = EntityKind.Enemy, ["ennemi"] = EntityKind.Enemy, ["monstre"] = EntityKind.Enemy,
            ["gobelin"] = EntityKind.Enemy, ["loup"] = EntityKind.Enemy,
            ["trésor"] = EntityKind.Object, ["épée"] = EntityKind.Object, ["potion"] = EntityKind.Object,
            ["clé"] = EntityKind.Object,
            ["forêt"] = EntityKind.Place, ["château"] = EntityKind.Place, ["village"] = EntityKind.Place,
            ["donjon"] = EntityKind.Place, ["montagne"] = EntityKind.Place, ["grotte"] = EntityKind.Place
        };

        // Verbes → comportements ECS.
        private static readonly Dictionary<string, string> Verbs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["suit"] = "Follow", ["poursuit"] = "Follow", ["suivre"] = "Follow",
            ["attaque"] = "Attack", ["combat"] = "Attack",
            ["fuit"] = "Flee", ["fuir"] = "Flee",
            ["protège"] = "Protect", ["garde"] = "Protect",
            ["cherche"] = "Seek", ["trouve"] = "Seek",
            ["prend"] = "Consume", ["mange"] = "Consume"
        };

        private static readonly Dictionary<string, string> PlaceModules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["forêt"] = "Forest", ["château"] = "Castle", ["village"] = "Village",
            ["donjon"] = "Dungeon", ["montagne"] = "Mountain", ["grotte"] = "Cave"
        };

        private static readonly HashSet<string> StopProper = new(StringComparer.OrdinalIgnoreCase)
        {
            "Le", "La", "Les", "Un", "Une", "Il", "Elle", "Et", "Mais", "Ou", "Donc",
            "Je", "Tu", "Nous", "Vous", "Ils", "Elles", "MOTO", "IA"
        };

        // ------------------------------------------------------------------
        // Lecture de l'histoire
        // ------------------------------------------------------------------

        public StoryMap ReadStory(string story)
        {
            var map = new StoryMap();
            story ??= string.Empty;

            // Titre = première phrase.
            var sentences = Regex.Split(story, @"(?<=[\.!\?])\s+");
            map.Title = sentences.FirstOrDefault() ?? "Mon histoire";
            if (map.Title.Length > 60) map.Title = map.Title.Substring(0, 60) + "…";

            // Intrigue = phrases (plafonnées), réutilisées comme étapes de jeu.
            map.PlotSteps.AddRange(sentences.Take(20));

            // 1. Entités du lexique.
            foreach (var noun in Nouns)
            {
                if (story.Contains(noun.Key, StringComparison.OrdinalIgnoreCase))
                {
                    map.Entities.Add(new StoryEntity
                    {
                        Name = Pascal(noun.Key),
                        Kind = noun.Value
                    });
                }
            }

            // 2. Noms propres (hors mots vides et doublons).
            foreach (Match m in Regex.Matches(story, @"\b([A-Z][a-z]{2,})\b"))
            {
                var name = m.Groups[1].Value;

                if (!StopProper.Contains(name) &&
                    !map.Entities.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    map.Entities.Add(new StoryEntity { Name = name, Kind = EntityKind.Hero });
                }
            }

            // 3. Comportements : verbe + sujet avant + cible après.
            var mentions = BuildMentions(story, map.Entities);

            foreach (var verb in Verbs)
            {
                int index = 0;

                while ((index = story.IndexOf(verb.Key, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var subject = mentions.Where(m => m.Index < index)
                                          .OrderByDescending(m => m.Index)
                                          .Select(m => m.Name).FirstOrDefault();

                    var target = mentions.Where(m => m.Index > index)
                                         .OrderBy(m => m.Index)
                                         .Select(m => m.Name).FirstOrDefault() ?? "Player";

                    if (subject != null)
                    {
                        var behavior = new StoryBehavior
                        {
                            Subject = subject,
                            Action = verb.Value,
                            Target = target
                        };

                        if (!map.Behaviors.Any(b => b.Subject == subject && b.Action == verb.Value && b.Target == target))
                        {
                            map.Behaviors.Add(behavior);
                        }
                    }

                    index += verb.Key.Length;
                }
            }

            // 4. Lieux → modules de monde.
            foreach (var place in PlaceModules)
            {
                if (story.Contains(place.Key, StringComparison.OrdinalIgnoreCase))
                {
                    map.Modules.Add(place.Value);
                }
            }

            return map;
        }

        private List<(int Index, string Name)> BuildMentions(string story, List<StoryEntity> entities)
        {
            var mentions = new List<(int, string)>();

            foreach (var entity in entities)
            {
                int i = 0;

                while ((i = story.IndexOf(entity.Name, i, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    mentions.Add((i, entity.Name));
                    i += entity.Name.Length;
                }
            }

            return mentions.OrderBy(m => m.Item1).ToList();
        }

        // ------------------------------------------------------------------
        // Génération du code
        // ------------------------------------------------------------------

        public List<AiFileChange> Generate(StoryMap map)
        {
            var changes = new List<AiFileChange>();

            // 1. Composants d'entités (héros/ennemis/objets).
            foreach (var entity in map.Entities.Where(e => e.Kind != EntityKind.Place))
            {
                changes.Add(new AiFileChange
                {
                    Path = $"Story/{entity.Name}Component.cs",
                    Reason = $"Entité de l'histoire : {entity.Name} ({entity.Kind}).",
                    ChangeType = FileChangeType.Create,
                    Content = GenerateEntityComponent(entity)
                });
            }

            // 2. Comportements via BehaviorBuilderV2.
            foreach (var behavior in map.Behaviors)
            {
                var files = _behaviors.Build(
                    $"{behavior.Subject} {behavior.Action.ToLower()} {behavior.Target}",
                    out _);

                foreach (var f in files)
                {
                    changes.Add(new AiFileChange
                    {
                        Path = f.RelativePath,
                        Content = f.Content,
                        Reason = $"Histoire : {behavior.Subject} {behavior.Action} {behavior.Target}.",
                        ChangeType = FileChangeType.Create
                    });
                }
            }

            // 3. Lieux → modules ECS.
            foreach (var module in map.Modules)
            {
                changes.AddRange(_generation.GenerateModule(new ProjectMap(), module));
            }

            // 4. Carte narrative en documentation.
            changes.Add(new AiFileChange
            {
                Path = "Story/STORY.md",
                Reason = "L'histoire et sa traduction en code.",
                ChangeType = FileChangeType.Create,
                Content = GenerateStoryDoc(map)
            });

            return changes;
        }

        private string GenerateEntityComponent(StoryEntity entity)
        {
            var props = entity.Kind switch
            {
                EntityKind.Hero => "public int Hp { get; set; } = 100;\n        public float Speed { get; set; } = 3f;",
                EntityKind.Enemy => "public int Hp { get; set; } = 50;\n        public float Damage { get; set; } = 5f;",
                _ => "public int Value { get; set; } = 1;"
            };

            return $@"using System;

namespace Story
{{
    /// <summary>Entité issue de l'histoire : {entity.Name}.</summary>
    public class {entity.Name}Component
    {{
        {props}
        public bool IsActive {{ get; set; }} = true;
    }}
}}";
        }

        private string GenerateStoryDoc(StoryMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# {map.Title}");
            sb.AppendLine();
            sb.AppendLine("## Intrigue");
            foreach (var step in map.PlotSteps) sb.AppendLine($"- {step}");
            sb.AppendLine();
            sb.AppendLine("## Personnages et objets");
            foreach (var e in map.Entities) sb.AppendLine($"- {e.Name} ({e.Kind})");
            sb.AppendLine();
            sb.AppendLine("## Comportements générés");
            foreach (var b in map.Behaviors) sb.AppendLine($"- {b.Subject} → {b.Action} → {b.Target}");
            sb.AppendLine();
            sb.AppendLine("## Mondes");
            foreach (var m in map.Modules) sb.AppendLine($"- {m}");

            return sb.ToString();
        }

        /// <summary>Explication pédagogique de la traduction histoire → code.</summary>
        public string Narrate(StoryMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("📖 J'ai lu ton histoire ! Voici comment je l'ai traduite en code :");
            sb.AppendLine();

            foreach (var e in map.Entities)
            {
                sb.AppendLine(e.Kind switch
                {
                    EntityKind.Hero => $"🧑 {e.Name} est un héros → je crée {e.Name}Component.",
                    EntityKind.Enemy => $"👹 {e.Name} est un ennemi → je crée {e.Name}Component.",
                    EntityKind.Object => $"🎁 {e.Name} est un objet → je crée {e.Name}Component.",
                    _ => $"🗺 {e.Name} est un lieu → je crée le module {e.Name}."
                });
            }

            foreach (var b in map.Behaviors)
            {
                sb.AppendLine($"⚡ « {b.Subject} {b.Action} {b.Target} » → je crée {b.Subject}{b.Action}System.");
            }

            sb.AppendLine();
            sb.AppendLine("Ton monde est prêt. Appuie sur ▶ pour le voir vivre !");

            return sb.ToString();
        }

        private static string Pascal(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
    }
}
