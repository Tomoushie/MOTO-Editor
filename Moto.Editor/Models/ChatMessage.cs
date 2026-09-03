// Moto.Editor/Models/ChatMessage.cs
using System;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Models
{
    /// <summary>Côté de dockage du panneau chat.</summary>
    public enum DockSide { Left, Right }

    /// <summary>Message de la conversation IA.</summary>
    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // user | ai | system
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsUser => Role == "user";

        public string TimeLabel => Timestamp.ToString("HH:mm");

        /// <summary>Couleur de la bulle selon le rôle.</summary>
        public Color BubbleColor => IsUser
            ? Color.FromRgb(0, 122, 204)
            : Role == "system"
                ? Color.FromRgb(60, 50, 20)
                : Color.FromRgb(32, 33, 38);

        /// <summary>Alignement : utilisateur à droite, IA à gauche.</summary>
        public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;

        /// <summary>
        /// ★ AJOUT (03/09, bouton "copier" manquant sur le code, trouvé par Tom en
        /// testant le chantier précédent) : découpe Content en morceaux texte/code
        /// sur les balises ``` — PAS un vrai parseur Markdown/CommonMark, juste
        /// assez pour distinguer un bloc de code (à afficher en police mono, avec
        /// un bouton copier) du reste. Calculée à chaque accès plutôt que mise en
        /// cache : Content n'est jamais modifié après la création du message
        /// (ajouté une fois par ChatService, jamais réassigné).
        /// </summary>
        public IReadOnlyList<ChatContentSegment> Segments => ParseSegments(Content);

        private static IReadOnlyList<ChatContentSegment> ParseSegments(string content)
        {
            var result = new List<ChatContentSegment>();
            if (string.IsNullOrEmpty(content))
            {
                result.Add(new ChatContentSegment { IsCode = false, Text = string.Empty });
                return result;
            }

            var parts = content.Split("```");
            for (var i = 0; i < parts.Length; i++)
            {
                var isCode = i % 2 == 1; // indices impairs = à l'intérieur d'une paire de ```
                var text = parts[i];

                if (isCode)
                {
                    // 1re ligne éventuelle = nom de langage (ex. ```csharp), à retirer du code affiché.
                    var newlineIdx = text.IndexOf('\n');
                    var code = text;
                    if (newlineIdx >= 0)
                    {
                        var firstLine = text.Substring(0, newlineIdx).Trim();
                        if (firstLine.Length > 0 && !firstLine.Contains(' '))
                            code = text.Substring(newlineIdx + 1);
                    }
                    code = code.Trim('\n', '\r');
                    if (code.Length > 0) result.Add(new ChatContentSegment { IsCode = true, Text = code });
                }
                else
                {
                    var trimmed = text.Trim('\n', '\r');
                    if (trimmed.Length > 0) result.Add(new ChatContentSegment { IsCode = false, Text = trimmed });
                }
            }

            if (result.Count == 0) result.Add(new ChatContentSegment { IsCode = false, Text = content });
            return result;
        }
    }

    /// <summary>Un morceau de message : texte normal, ou bloc de code entre ``` .</summary>
    public sealed class ChatContentSegment
    {
        public bool IsCode { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>Élément de contexte attaché à la conversation.</summary>
    public class ChatContextItem
    {
        public string Kind { get; set; } = "file"; // file | folder | selection
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public string Label => Kind switch
        {
            "file" => $"📄 {System.IO.Path.GetFileName(Path)}",
            "folder" => $"📁 {System.IO.Path.GetFileName(Path)}",
            _ => "✂ sélection"
        };
    }
}
